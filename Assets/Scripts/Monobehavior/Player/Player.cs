using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using System.Collections;
using System;
using Unity.Cinemachine;

public class Player : BaseEntity, IPlayerContext
{
    [Header("External References")]
    public DungeonGenerator generator;

    [Header("Hammer Settings (Heavy)")]
    public float maxChargeTime = 0.5f;
    public float hammerAOE = 2.5f;
    public float hammerCooldown = 3f;
    [SerializeField] private float heavyImpactFallbackDelay = 0.2f;

    [Header("Bow / Arrow")]
    [Tooltip("Üzerinde PlayerArrow bileşeni olan ok prefabı.")]
    public GameObject arrowPrefab;
    public float arrowSpeed = 14f;
    public float arrowMaxLifetime = 8f;
    [Tooltip("Boş bırakılırsa Camera.main kullanılır; imleç dünya koordinatı için.")]
    public Camera aimCamera;
    [Tooltip("Yay animasyonu bittikten sonra ok instantiate edilir. Ok çıkarken imleç bu süre sonunda tekrar okunur (Time.timeScale ile).")]
    public float bowArrowReleaseDelay = 0.4f;
    [Tooltip("Tam şarjlı sağ-tık yayda ok hızı ve hasarı bu çarpanla çarpılır.")]
    public float bowChargedSpeedDamageMultiplier = 3f;
    [Tooltip("Sağ tık basılı tutma süresi (saniye); dolunca yay tam şarj sayılır.")]
    public float maxBowChargeTime = 0.5f;
    [Tooltip("Boşsa yay şarj çubuğu gösterilmez; Inspector'dan atayabilirsin.")]
    public Slider bowChargeMeter;
    [Tooltip("Boşsa yay şarj UI kanvası açılmaz.")]
    public GameObject bowMeterCanvas;

    [Header("Bow radial mutation (auto)")]
    [Tooltip("Beş farklı ok augment sonrası, ek input olmadan kaç saniyede bir salvı.")]
    [SerializeField] private float radialBowAutoVolleyIntervalSeconds = 1f;
    [SerializeField] private int autoArrowVolleyCount = 8;
    [SerializeField] private float autoArrowVolleyAngleStepDegrees = 45f;
    [SerializeField] private float radialBowSpawnInset = 0.2f;
    [Tooltip("Otomatik salvıda hedef mesafe (imleç yok).")]
    [SerializeField] private float radialBowAutoVolleyTravelDistance = 8f;

    [Header("Light Attack Settings (Spammable)")]
    public float lightAttackRate = 0.2f;
    public float lightAttackDuration = 0.1f;
    [SerializeField] private float lightImpactFallbackDelay = 0.08f;
    private float _nextAttackTime = 0f;
    private float _lastLightAttackResolveTime = -999f;
    private bool _lightAttackInProgress = false;
    private float _lightFallbackExecuteAt = -1f;
    private float _lastHeavyResolveTime = -999f;
    private bool _heavyAttackInProgress = false;
    private float _heavyFallbackExecuteAt = -1f;

    [Header("Damage")]
    [SerializeField] private float damageInvulnerabilityDuration = 0.2f;

    [Header("References")]
    public Transform attackPoint;
    [SerializeField] private Animator animator;
    public LayerMask enemyLayers;
    private PlayerLevel playerLevel;
    private PlayerCurrency playerCurrency;
    private PlayerAugmentController playerAugmentController;
    private WallLootHandler wallLootHandler;

    [Header("Dash Settings")]
    [Tooltip("Dash mesafesi (birim). Duvara çarparsa öncesinde durur.")]
    public float dashDistance = 4f;
    public float dashCooldown = 10f;
    [SerializeField] private float dashAlphaFlashDuration = 0.1f;
    [Tooltip("Boşsa root/child'dan otomatik aranır.")]
    [SerializeField] private SpriteRenderer dashFlashTarget;

    [Header("Dash Cooldown UI")]
    public Slider dashCooldownBar;
    public GameObject dashCooldownCanvas;
    [SerializeField] private bool showDashCooldownBarWhenReady = true;

    [Header("Hammer Cooldown UI")]
    public Slider hammerCooldownBar;
    public GameObject hammerCooldownCanvas;
    [SerializeField] private bool showCooldownBarWhenReady = true;

    [Header("Hammer Charge UI")]
    public Slider chargeMeter;
    public GameObject meterCanvas;

    [Header("Impact Feedback")]
    private PlayerImpactFeedback impactFeedback;

    private float _nextHammerUseTime = 0f;
    private float _nextDashTime = 0f;
    private bool _hadRadialBowMutationLastFrame;
    private float _nextRadialBowAutoVolleyTime;
    private Rigidbody2D _rb;
    private CinemachineImpulseSource _defaultImpulseSource;
    private float _invulnerableUntil = 0f;
    private PlayerState _currentState;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    public event Action<float, float> HealthChanged;
    public event Action Died;

    public PlayerLevel PlayerLevel => playerLevel;
    public PlayerCurrency PlayerCurrency => playerCurrency;
    public PlayerAugmentController PlayerAugmentController => playerAugmentController;

    // ─── IPlayerContext ───────────────────────────────────────────────────────

    public void SetState(PlayerState newState)
    {
        _currentState?.Exit(this);
        _currentState = newState;
        _currentState.Enter(this);
    }

    EntityStats IPlayerContext.Stats => stats;
    PlayerAugmentController IPlayerContext.AugmentController => playerAugmentController;
    float IPlayerContext.MaxChargeTime => maxChargeTime;
    Slider IPlayerContext.ChargeMeter => chargeMeter;
    GameObject IPlayerContext.MeterCanvas => meterCanvas;
    float IPlayerContext.MaxBowChargeTime => maxBowChargeTime;
    float IPlayerContext.LightAttackRate => lightAttackRate;
    float IPlayerContext.LightImpactFallbackDelay => lightImpactFallbackDelay;
    Slider IPlayerContext.BowChargeMeter => bowChargeMeter;
    GameObject IPlayerContext.BowMeterCanvas => bowMeterCanvas;
    Animator IPlayerContext.Animator => animator;

    float IPlayerContext.NextHammerUseTime
    {
        get => _nextHammerUseTime;
        set => _nextHammerUseTime = value;
    }
    float IPlayerContext.NextAttackTime
    {
        get => _nextAttackTime;
        set => _nextAttackTime = value;
    }
    bool IPlayerContext.LightAttackInProgress
    {
        get => _lightAttackInProgress;
        set => _lightAttackInProgress = value;
    }
    float IPlayerContext.LightFallbackExecuteAt
    {
        get => _lightFallbackExecuteAt;
        set => _lightFallbackExecuteAt = value;
    }

    void IPlayerContext.ScheduleBowArrow(float damage, bool useBowChargedMultiplier, Vector2 aimWorldAtFireInput)
        => ScheduleBowArrow(damage, useBowChargedMultiplier, aimWorldAtFireInput);

    Vector2 IPlayerContext.GetBowAimWorldPointAtCurrentMouse()
        => GetBowAimWorldPointAtCurrentMouse();

    void IPlayerContext.TriggerHeavyAttack()
        => TriggerHeavyAttack();

    // ─── MaxHealth ────────────────────────────────────────────────────────────

    public override float MaxHealth
    {
        get
        {
            float baseMax = stats != null ? stats.maxHealth : 0f;
            float mult = playerAugmentController != null ? playerAugmentController.MaxHealthMultiplier : 1f;
            return baseMax * mult;
        }
    }

    public void OnMaxHealthMultiplierChanged(float previousMultiplier, float newMultiplier)
    {
        if (stats == null) return;
        float baseMax = stats.maxHealth;
        float oldMax = baseMax * previousMultiplier;
        float newMax = baseMax * newMultiplier;
        if (oldMax <= 0.001f || newMax <= 0f) return;
        _currentHealth *= newMax / oldMax;
        _currentHealth = Mathf.Clamp(_currentHealth, 1f, newMax);
        NotifyHealthChanged();
    }

    // ─── Unity lifecycle ──────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _defaultImpulseSource = GetComponent<CinemachineImpulseSource>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (playerLevel == null)
            playerLevel = GetComponent<PlayerLevel>();
        if (playerCurrency == null)
            playerCurrency = GetComponent<PlayerCurrency>();
        if (playerAugmentController == null)
            playerAugmentController = GetComponent<PlayerAugmentController>();
        if (wallLootHandler == null)
            wallLootHandler = GetComponent<WallLootHandler>();
        if (impactFeedback == null)
            impactFeedback = GetComponent<PlayerImpactFeedback>();
        if (dashFlashTarget == null)
            dashFlashTarget = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();

        _currentState = new IdleState();
        _currentState.Enter(this);

        InitializeHammerCooldownUI();
        InitializeDashCooldownUI();
        NotifyHealthChanged();
        if (playerCurrency != null)
            playerCurrency.NotifyGoldChanged();
    }

    private void OnEnable()
    {
        if (playerLevel != null)
            playerLevel.LevelUp += HandleLevelUp;
    }

    void OnDisable()
    {
        if (playerLevel != null)
            playerLevel.LevelUp -= HandleLevelUp;
        CancelPendingBowArrow();
    }

    void Update()
    {
        _currentState.Handle(this);
        HandleLightImpactFallback();
        HandleHeavyImpactFallback();
        HandleDash();
        UpdateHammerCooldownUI();
        UpdateDashCooldownUI();
        UpdateRadialBowAutoVolley();
    }

    void FixedUpdate()
    {
        Move();
    }

    // ─── Damage / health ──────────────────────────────────────────────────────

    public override void TakeDamage(float amount, bool isHeavy)
    {
        if (Time.time < _invulnerableUntil) return;

        if (_currentState.IsChargeMeterFull(this) && playerAugmentController != null && playerAugmentController.HasHammerChargeDamageReductionUnlock)
            amount *= 0.75f;

        if (playerAugmentController != null && playerAugmentController.IncomingDamageReduction > 0f)
            amount *= 1f - playerAugmentController.IncomingDamageReduction;

        base.TakeDamage(amount, isHeavy);
        NotifyHealthChanged();
        _invulnerableUntil = Time.time + Mathf.Max(0f, damageInvulnerabilityDuration);
    }

    protected override void Die()
    {
        _currentHealth = 0f;
        SetState(new DiedState());
        Died?.Invoke();
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        ICollectable collectable = col.GetComponent<ICollectable>()
            ?? col.GetComponentInParent<ICollectable>();
        collectable?.Collect(this);
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        _currentHealth = Mathf.Clamp(_currentHealth + amount, 0f, MaxHealth);
        NotifyHealthChanged();
    }

    public void NotifyHealthChanged()
    {
        HealthChanged?.Invoke(CurrentHealth, Mathf.Max(1f, MaxHealth));
    }

    public void AddExperience(float amount)
    {
        if (amount <= 0f || playerLevel == null) return;
        playerLevel.AddExperience(amount);
    }

    // ─── Movement ─────────────────────────────────────────────────────────────

    protected override void Move()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        Vector2 direction = new Vector2(moveX, moveY).normalized;

        float augmentSpeedBonus = playerAugmentController != null ? playerAugmentController.MovementSpeedBonus : 0f;
        float chargeMultiplier = _currentState.IsChargingForMovement ? 0.3f : 1f;
        float currentSpeed = stats.moveSpeed * (1f + augmentSpeedBonus) * chargeMultiplier;

        _rb.linearVelocity = direction * currentSpeed;
        if (animator != null)
            animator.SetFloat(SpeedHash, direction.magnitude);

        if (moveX > 0) transform.localScale = new Vector3(1f, 1f, 1f);
        else if (moveX < 0) transform.localScale = new Vector3(-1f, 1f, 1f);
    }

    // ─── Attack resolution (animation events + fallbacks) ────────────────────

    public void LightAttack()
    {
        ClearLightAttackPendingState();
    }

    private void ClearLightAttackPendingState()
    {
        if (Time.time - _lastLightAttackResolveTime < Mathf.Max(0.01f, lightAttackDuration * 0.5f))
            return;
        _lastLightAttackResolveTime = Time.time;
        _lightAttackInProgress = false;
        _lightFallbackExecuteAt = -1f;
    }

    private void HandleLightImpactFallback()
    {
        if (!_lightAttackInProgress) return;
        if (Time.time < _lightFallbackExecuteAt) return;
        ClearLightAttackPendingState();
    }

    private void HandleHeavyImpactFallback()
    {
        if (!_heavyAttackInProgress) return;
        if (Time.time < _heavyFallbackExecuteAt) return;
        HammerSlam();
    }

    private void TriggerHeavyAttack()
    {
        _nextHammerUseTime = Time.time + hammerCooldown;
        UpdateHammerCooldownUI();
        if (animator != null)
            animator.SetTrigger(Animator.StringToHash("HeavyAttack"));
        _heavyAttackInProgress = true;
        _heavyFallbackExecuteAt = Time.time + Mathf.Max(0.05f, heavyImpactFallbackDelay);
    }

    public void HammerSlam()
    {
        if (Time.time - _lastHeavyResolveTime < 0.05f) return;

        _lastHeavyResolveTime = Time.time;
        _heavyAttackInProgress = false;
        _heavyFallbackExecuteAt = -1f;

        if (_defaultImpulseSource != null) _defaultImpulseSource.GenerateImpulse();
        float effectiveHammerAoe = hammerAOE * (playerAugmentController != null ? playerAugmentController.HammerAoeRadiusMultiplier : 1f);
        float hammerFreezeDuration = playerAugmentController != null ? playerAugmentController.HammerFreezeDuration : 0f;

        if (generator != null)
        {
            var brokenWalls = generator.BreakWallsInArea(attackPoint.position, effectiveHammerAoe);
            wallLootHandler?.TrySpawnWallLootForBrokenWalls(brokenWalls);
        }

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, effectiveHammerAoe, enemyLayers);
        int successfulHits = 0;
        Vector3 firstHitPosition = attackPoint.position;
        foreach (Collider2D enemy in hitEnemies)
        {
            IDamageable target = enemy.GetComponent<IDamageable>() ?? enemy.GetComponentInParent<IDamageable>();
            if (target == null) continue;
            BaseEntity targetEntity = enemy.GetComponent<BaseEntity>() ?? enemy.GetComponentInParent<BaseEntity>();
            float heavyDamage = targetEntity != null ? targetEntity.CurrentHealth : stats.heavyAttackDamage;
            float dmgMult = playerAugmentController != null ? playerAugmentController.OutgoingDamageMultiplier : 1f;
            target.TakeDamage(heavyDamage * dmgMult, true);
            if (hammerFreezeDuration > 0f && targetEntity != null && targetEntity.CurrentHealth > 0f)
            {
                Enemy enemyComp = targetEntity as Enemy ?? enemy.GetComponentInParent<Enemy>();
                if (enemyComp != null)
                    enemyComp.Freeze(hammerFreezeDuration);
            }
            if (successfulHits == 0)
                firstHitPosition = enemy.ClosestPoint(attackPoint.position);
            successfulHits++;
        }

        if (successfulHits > 0)
            impactFeedback?.PlayHeavyHit(firstHitPosition, _defaultImpulseSource);
    }

    // ─── Bow / arrow ──────────────────────────────────────────────────────────

    private Vector2 GetBowAimWorldPointAtCurrentMouse()
    {
        if (attackPoint == null) return Vector2.zero;
        Camera cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null) return attackPoint.position;

        Vector3 mouse = Input.mousePosition;
        float planeZ = attackPoint.position.z;
        mouse.z = Mathf.Abs(cam.transform.position.z - planeZ);
        return cam.ScreenToWorldPoint(mouse);
    }

    private void ScheduleBowArrow(float damage, bool useBowChargedMultiplier, Vector2 aimWorldAtFireInput)
    {
        float delay = Mathf.Max(0f, bowArrowReleaseDelay);
        if (delay <= 0f)
        {
            SpawnArrowTowardWorld(damage, useBowChargedMultiplier, aimWorldAtFireInput);
            return;
        }
        StartCoroutine(BowArrowSpawnAfterDelay(delay, damage, useBowChargedMultiplier, aimWorldAtFireInput));
    }

    private IEnumerator BowArrowSpawnAfterDelay(float delaySeconds, float damage, bool useBowChargedMultiplier, Vector2 aimWorldAtFireInput)
    {
        yield return new WaitForSeconds(delaySeconds);
        SpawnArrowTowardWorld(damage, useBowChargedMultiplier, aimWorldAtFireInput);
    }

    private void SpawnArrowTowardWorld(float damage, bool useBowChargedMultiplier, Vector2 targetWorld)
    {
        if (arrowPrefab == null || attackPoint == null) return;

        bool chargedExplosionEnabled = useBowChargedMultiplier &&
                                       playerAugmentController != null &&
                                       playerAugmentController.HasChargedBowAoe;
        float m = useBowChargedMultiplier ? Mathf.Max(1f, bowChargedSpeedDamageMultiplier) : 1f;
        float dmgMult = playerAugmentController != null ? playerAugmentController.OutgoingDamageMultiplier : 1f;
        float arrowSpdMult = playerAugmentController != null ? playerAugmentController.ArrowProjectileSpeedMultiplier : 1f;
        float useSpeed = arrowSpeed * m * arrowSpdMult;
        float useDamage = damage * m * dmgMult;

        Vector2 origin = attackPoint.position;
        Vector2 offset = targetWorld - origin;
        if (offset.sqrMagnitude < 0.0001f) offset = Vector2.right * 0.01f;
        Vector2 dir = offset.normalized;
        float targetDistance = Mathf.Max(0.5f, offset.magnitude);
        int arrowCount = playerAugmentController != null ? Mathf.Max(1, playerAugmentController.ArrowShotMultiplier) : 1;
        float spreadStepDegrees = GetArrowSpreadStepDegrees(arrowCount);
        float centerOffset = (arrowCount - 1) * 0.5f;

        for (int i = 0; i < arrowCount; i++)
        {
            float angleOffset = (i - centerOffset) * spreadStepDegrees;
            Vector2 shotDir = Quaternion.Euler(0f, 0f, angleOffset) * dir;
            Vector2 spawnPos = origin + shotDir * radialBowSpawnInset;
            TrySpawnSinglePlayerArrow(spawnPos, origin + shotDir * targetDistance, useSpeed, useDamage, chargedExplosionEnabled);
        }
    }

    private void UpdateRadialBowAutoVolley()
    {
        if (arrowPrefab == null) return;

        bool active = playerAugmentController != null &&
                      playerAugmentController.ShouldUseRadialBowVolleyMutation(this);
        if (!active)
        {
            _hadRadialBowMutationLastFrame = false;
            return;
        }

        if (!_hadRadialBowMutationLastFrame)
            _nextRadialBowAutoVolleyTime = Time.time;

        _hadRadialBowMutationLastFrame = true;

        if (Time.time < _nextRadialBowAutoVolleyTime) return;

        float baseDamage = stats != null ? stats.lightAttackDamage : 0f;
        FireRadialBowMutationAutoVolley(baseDamage);
        _nextRadialBowAutoVolleyTime = Time.time + Mathf.Max(0.05f, radialBowAutoVolleyIntervalSeconds);
    }

    private void FireRadialBowMutationAutoVolley(float lightDamage)
    {
        float dmgMult = playerAugmentController != null ? playerAugmentController.OutgoingDamageMultiplier : 1f;
        float arrowSpdMult = playerAugmentController != null ? playerAugmentController.ArrowProjectileSpeedMultiplier : 1f;
        float useSpeed = arrowSpeed * arrowSpdMult;
        float useDamage = lightDamage * dmgMult;

        Vector2 radialOrigin = transform.position;
        float targetDistance = Mathf.Max(0.5f, radialBowAutoVolleyTravelDistance);
        int volleyCount = Mathf.Clamp(autoArrowVolleyCount, 1, 64);
        float step = Mathf.Max(1f, autoArrowVolleyAngleStepDegrees);

        for (int i = 0; i < volleyCount; i++)
        {
            Vector2 radialDir = RadialBowDirFromAngleDegrees(i * step);
            float forward = Mathf.Max(0f, radialBowSpawnInset);
            Vector2 spawnPos = radialOrigin + radialDir * forward;
            Vector2 shotTarget = radialOrigin + radialDir * Mathf.Max(forward + 0.3f, targetDistance);
            TrySpawnSinglePlayerArrow(spawnPos, shotTarget, useSpeed, useDamage, false);
        }
    }

    private static Vector2 RadialBowDirFromAngleDegrees(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
    }

    private void TrySpawnSinglePlayerArrow(
        Vector2 spawnWorldPosition,
        Vector2 targetWorldPoint,
        float useSpeed,
        float useDamage,
        bool chargedExplosionEnabled)
    {
        if (arrowPrefab == null) return;

        float explosionRadius = chargedExplosionEnabled && playerAugmentController != null
            ? playerAugmentController.ChargedBowAoeRadius
            : 0f;

        GameObject arrow = Instantiate(arrowPrefab, spawnWorldPosition, Quaternion.identity);
        PlayerArrow mover = arrow.GetComponent<PlayerArrow>();
        if (mover == null)
        {
            Destroy(arrow);
            return;
        }

        mover.Initialize(
            targetWorldPoint,
            useSpeed,
            useDamage,
            arrowMaxLifetime,
            enemyLayers,
            transform,
            chargedExplosionEnabled,
            explosionRadius,
            generator,
            chargedExplosionEnabled ? _defaultImpulseSource : null,
            playerAugmentController != null ? playerAugmentController.BowFreezeDuration : 0f);
    }

    private static float GetArrowSpreadStepDegrees(int arrowCount)
    {
        switch (arrowCount)
        {
            case 2: return 15f;
            case 3: return 10f;
            case 4: return 8f;
            default: return arrowCount > 4 ? 8f : 0f;
        }
    }

    private void CancelPendingBowArrow()
    {
        StopAllCoroutines();
    }

    // ─── Dash ────────────────────────────────────────────────────────────────

    private void HandleDash()
    {
        if (!Input.GetKeyDown(KeyCode.Space)) return;
        if (playerAugmentController == null || !playerAugmentController.HasDashUnluck) return;
        if (Time.time < _nextDashTime) return;

        Vector2 dashDir = GetDashDirection();
        if (dashDir.sqrMagnitude < 0.0001f) return;

        Vector2 from = _rb.position;
        float closestBlockingDist = Mathf.Max(0f, dashDistance * (playerAugmentController != null ? playerAugmentController.DashDistanceMultiplier : 1f));

        RaycastHit2D[] hits = Physics2D.RaycastAll(from, dashDir, closestBlockingDist);
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D h = hits[i];
            if (h.collider == null || h.collider.isTrigger) continue;
            if (h.collider.GetComponentInParent<Player>() != null) continue;
            if (h.collider.GetComponentInParent<Enemy>() != null) continue;
            if (h.distance < closestBlockingDist)
                closestBlockingDist = h.distance;
        }

        float actualDistance = Mathf.Max(0f, closestBlockingDist - 0.3f);
        _rb.position = from + dashDir * actualDistance;
        float effectiveCooldown = dashCooldown * (playerAugmentController != null ? playerAugmentController.DashCooldownMultiplier : 1f);
        _nextDashTime = Time.time + Mathf.Max(0f, effectiveCooldown);
        StartCoroutine(DashAlphaFlash());
    }

    private Vector2 GetDashDirection()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        Vector2 dir = new Vector2(moveX, moveY).normalized;
        if (dir.sqrMagnitude > 0.0001f) return dir;
        return transform.localScale.x >= 0f ? Vector2.right : Vector2.left;
    }

    private IEnumerator DashAlphaFlash()
    {
        if (dashFlashTarget == null) yield break;
        Color original = dashFlashTarget.color;
        Color invisible = original;
        invisible.a = 0f;
        dashFlashTarget.color = invisible;
        yield return new WaitForSeconds(Mathf.Max(0.01f, dashAlphaFlashDuration));
        dashFlashTarget.color = original;
    }

    // ─── Level / misc ─────────────────────────────────────────────────────────

    private void HandleLevelUp()
    {
        _currentHealth = MaxHealth;
        NotifyHealthChanged();
        SetState(new IdleState());
    }

    // ─── UI ──────────────────────────────────────────────────────────────────

    private void InitializeDashCooldownUI()
    {
        if (dashCooldownBar == null) return;
        dashCooldownBar.minValue = 0f;
        dashCooldownBar.maxValue = 1f;
        dashCooldownBar.value = 1f;
        if (dashCooldownCanvas != null)
            dashCooldownCanvas.SetActive(showDashCooldownBarWhenReady);
    }

    private void UpdateDashCooldownUI()
    {
        if (dashCooldownBar == null) return;
        float cooldown = Mathf.Max(0f, dashCooldown * (playerAugmentController != null ? playerAugmentController.DashCooldownMultiplier : 1f));
        if (cooldown <= 0f)
        {
            dashCooldownBar.value = 1f;
            if (dashCooldownCanvas != null) dashCooldownCanvas.SetActive(showDashCooldownBarWhenReady);
            return;
        }
        float remaining = Mathf.Max(0f, _nextDashTime - Time.time);
        dashCooldownBar.value = Mathf.Clamp01(1f - (remaining / cooldown));
        if (dashCooldownCanvas != null)
        {
            bool isReady = remaining <= 0f;
            dashCooldownCanvas.SetActive(!isReady || showDashCooldownBarWhenReady);
        }
    }

    private void InitializeHammerCooldownUI()
    {
        if (hammerCooldownBar == null) return;
        hammerCooldownBar.minValue = 0f;
        hammerCooldownBar.maxValue = 1f;
        hammerCooldownBar.value = 1f;
        if (hammerCooldownCanvas != null)
            hammerCooldownCanvas.SetActive(showCooldownBarWhenReady);
    }

    private void UpdateHammerCooldownUI()
    {
        if (hammerCooldownBar == null) return;
        float cooldown = Mathf.Max(0f, hammerCooldown);
        if (cooldown <= 0f)
        {
            hammerCooldownBar.value = 1f;
            if (hammerCooldownCanvas != null) hammerCooldownCanvas.SetActive(showCooldownBarWhenReady);
            return;
        }
        float remaining = Mathf.Max(0f, _nextHammerUseTime - Time.time);
        hammerCooldownBar.value = Mathf.Clamp01(1f - (remaining / cooldown));
        if (hammerCooldownCanvas != null)
        {
            bool isReady = remaining <= 0f;
            hammerCooldownCanvas.SetActive(!isReady || showCooldownBarWhenReady);
        }
    }
}
