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
    public float maxChargeTime = 1.5f;
    public float hammerAOE = 2.5f;
    public float hammerCooldown = 3f;
    [SerializeField] private float heavyImpactFallbackDelay = 0.2f;

    [Header("Longbow / Arrow")]
    [Tooltip("Üzerinde PlayerArrow bileşeni olan ok prefabı.")]
    public GameObject arrowPrefab;
    public float arrowSpeed = 14f;
    public float arrowMaxLifetime = 8f;
    [Tooltip("Boş bırakılırsa Camera.main kullanılır; imleç dünya koordinatı için.")]
    public Camera aimCamera;
    [Tooltip("Yay animasyonu bittikten sonra ok instantiate edilir. Ok çıkarken imleç bu süre sonunda tekrar okunur (Time.timeScale ile).")]
    public float longbowArrowReleaseDelay = 0.4f;
    [Tooltip("Tam şarjlı sağ-tık yayda ok hızı ve hasarı bu çarpanla çarpılır.")]
    public float longbowChargedSpeedDamageMultiplier = 3f;
    [Tooltip("Sağ tık basılı tutma süresi (saniye); dolunca yay tam şarj sayılır.")]
    public float maxLongbowChargeTime = 0.5f;
    [Tooltip("Boşsa yay şarj çubuğu gösterilmez; Inspector'dan atayabilirsin.")]
    public Slider longbowChargeMeter;
    [Tooltip("Boşsa yay şarj UI kanvası açılmaz.")]
    public GameObject longbowMeterCanvas;

    [Header("─── CROSSBOW ───────────────────────────")]
    [Tooltip("Crossbow bolt prefab (PlayerBolt component'i olmali).")]
    public GameObject crossbowBoltPrefab;

    [Header("Crossbow — Atış")]
    [Tooltip("Iki atis arasindaki sure (saniye). Animasyon suresiyle esit tutulmasi onerilir.")]
    public float crossbowAttackRate = 0.5f;
    [Tooltip("Trigger'dan bolt spawn'a kadar gecen sure (saniye). Genellikle: animasyonSuresi - 0.2")]
    public float crossbowBoltReleaseDelay = 0.3f;

    [Header("Crossbow — Bolt İstatistikleri")]
    [Tooltip("Bolt hizi = arrowSpeed x bu carpan.")]
    public float crossbowBoltSpeedMultiplier = 2f;
    [Tooltip("Bolt hasari = lightAttackDamage x bu carpan.")]
    public float crossbowBoltDamageMultiplier = 2f;
    [Tooltip("Bolt sahnede kaldigi maksimum sure (saniye).")]
    public float crossbowBoltMaxLifetime = 5f;

    [Header("Longbow radial mutation (auto)")]
    [Tooltip("6 longbow cevher augmenti ile Obsidyen'e ulaşınca, ek input olmadan kaç saniyede bir salvı.")]
    [SerializeField] private float radialLongbowAutoVolleyIntervalSeconds = 1f;
    [SerializeField] private int autoArrowVolleyCount = 8;
    [SerializeField] private float autoArrowVolleyAngleStepDegrees = 45f;
    [SerializeField] private float radialLongbowSpawnInset = 0.2f;
    [Tooltip("Otomatik salvıda hedef mesafe (imleç yok).")]
    [SerializeField] private float radialLongbowAutoVolleyTravelDistance = 8f;

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
    private bool _hadRadialLongbowMutationLastFrame;
    private float _nextRadialLongbowAutoVolleyTime;
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
    float IPlayerContext.MaxLongbowChargeTime => maxLongbowChargeTime;
    float IPlayerContext.LightAttackRate => lightAttackRate;
    float IPlayerContext.LightImpactFallbackDelay => lightImpactFallbackDelay;
    Slider IPlayerContext.LongbowChargeMeter => longbowChargeMeter;
    GameObject IPlayerContext.LongbowMeterCanvas => longbowMeterCanvas;
    float IPlayerContext.CrossbowBoltSpeedMultiplier => crossbowBoltSpeedMultiplier;
    float IPlayerContext.CrossbowBoltDamageMultiplier => crossbowBoltDamageMultiplier;
    float IPlayerContext.CrossbowAttackRate => crossbowAttackRate;
    float IPlayerContext.CrossbowBoltReleaseDelay => crossbowBoltReleaseDelay;
    GameObject IPlayerContext.CrossbowBoltPrefab => crossbowBoltPrefab;
    float IPlayerContext.CrossbowBoltMaxLifetime => crossbowBoltMaxLifetime;
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

    void IPlayerContext.ScheduleLongbowArrow(float damage, bool useBowChargedMultiplier, Vector2 aimWorldAtFireInput)
        => ScheduleLongbowArrow(damage, useBowChargedMultiplier, aimWorldAtFireInput);

    void IPlayerContext.ScheduleCrossbowBolt(float damage, Vector2 aimWorldAtFireInput)
        => ScheduleCrossbowBolt(damage, aimWorldAtFireInput);

    Vector2 IPlayerContext.GetLongbowAimWorldPointAtCurrentMouse()
        => GetLongbowAimWorldPointAtCurrentMouse();

    void IPlayerContext.TriggerHeavyAttack()
        => TriggerHeavyAttack();

    // ─── MaxHealth ────────────────────────────────────────────────────────────

    public override float MaxHealth
    {
        get
        {
            float baseMax = stats != null ? stats.maxHealth : 0f;
            float mult = playerAugmentController != null ? playerAugmentController.MaxHealthMultiplier : 1f;
            float flat = playerAugmentController != null ? playerAugmentController.FlatMaxHealthBonus : 0f;
            return baseMax * mult + flat;
        }
    }

    public void OnMaxHealthMultiplierChanged(float previousMultiplier, float newMultiplier)
    {
        if (stats == null) return;
        float baseMax = stats.maxHealth;
        float flat = playerAugmentController != null ? playerAugmentController.FlatMaxHealthBonus : 0f;
        float oldMax = baseMax * previousMultiplier + flat;
        float newMax = baseMax * newMultiplier + flat;
        if (oldMax <= 0.001f || newMax <= 0f) return;
        _currentHealth *= newMax / oldMax;
        _currentHealth = Mathf.Clamp(_currentHealth, 1f, newMax);
        NotifyHealthChanged();
    }

    public void OnFlatMaxHealthBonusChanged(float addedAmount)
    {
        if (addedAmount <= 0f) return;
        _currentHealth = Mathf.Clamp(_currentHealth + addedAmount, 1f, MaxHealth);
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
        CancelPendingLongbowArrow();
    }

    void Update()
    {
        _currentState.Handle(this);
        HandleLightImpactFallback();
        HandleHeavyImpactFallback();
        HandleDash();
        UpdateHammerCooldownUI();
        UpdateDashCooldownUI();
        UpdateRadialLongbowAutoVolley();
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

    // ─── Longbow / arrow ──────────────────────────────────────────────────────────

    private Vector2 GetLongbowAimWorldPointAtCurrentMouse()
    {
        if (attackPoint == null) return Vector2.zero;
        Camera cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null) return attackPoint.position;

        Vector3 mouse = Input.mousePosition;
        float planeZ = attackPoint.position.z;
        mouse.z = Mathf.Abs(cam.transform.position.z - planeZ);
        return cam.ScreenToWorldPoint(mouse);
    }

    private void ScheduleLongbowArrow(float damage, bool useBowChargedMultiplier, Vector2 aimWorldAtFireInput)
    {
        float delay = Mathf.Max(0f, longbowArrowReleaseDelay);
        if (delay <= 0f)
        {
            SpawnArrowTowardWorld(damage, useBowChargedMultiplier, aimWorldAtFireInput);
            return;
        }
        StartCoroutine(LongbowArrowSpawnAfterDelay(delay, damage, useBowChargedMultiplier, aimWorldAtFireInput));
    }

    private IEnumerator LongbowArrowSpawnAfterDelay(float delaySeconds, float damage, bool useBowChargedMultiplier, Vector2 aimWorldAtFireInput)
    {
        yield return new WaitForSeconds(delaySeconds);
        SpawnArrowTowardWorld(damage, useBowChargedMultiplier, aimWorldAtFireInput);
    }

    private void SpawnArrowTowardWorld(float damage, bool useBowChargedMultiplier, Vector2 targetWorld)
    {
        if (arrowPrefab == null || attackPoint == null) return;

        bool chargedExplosionEnabled = useBowChargedMultiplier &&
                                       playerAugmentController != null &&
                                       playerAugmentController.HasChargedLongbowAoe;
        float m = useBowChargedMultiplier ? Mathf.Max(1f, longbowChargedSpeedDamageMultiplier) : 1f;
        float dmgMult = playerAugmentController != null ? playerAugmentController.OutgoingDamageMultiplier : 1f;
        float arrowSpdMult = playerAugmentController != null ? playerAugmentController.ArrowProjectileSpeedMultiplier : 1f;
        float useSpeed = arrowSpeed * m * arrowSpdMult;
        float useDamage = damage * m * dmgMult;

        Vector2 origin = attackPoint.position;
        Vector2 offset = targetWorld - origin;
        if (offset.sqrMagnitude < 0.0001f) offset = Vector2.right * 0.01f;
        Vector2 dir = offset.normalized;
        float targetDistance = Mathf.Max(0.5f, offset.magnitude);
        int arrowCount = playerAugmentController != null ? Mathf.Max(1, playerAugmentController.ProjectileShotMultiplier) : 1;
        float spreadStepDegrees = GetArrowSpreadStepDegrees(arrowCount);
        float centerOffset = (arrowCount - 1) * 0.5f;

        for (int i = 0; i < arrowCount; i++)
        {
            float angleOffset = (i - centerOffset) * spreadStepDegrees;
            Vector2 shotDir = Quaternion.Euler(0f, 0f, angleOffset) * dir;
            Vector2 spawnPos = origin + shotDir * radialLongbowSpawnInset;
            TrySpawnSinglePlayerArrow(spawnPos, origin + shotDir * targetDistance, useSpeed, useDamage, chargedExplosionEnabled);
        }
    }

    private void UpdateRadialLongbowAutoVolley()
    {
        if (arrowPrefab == null) return;

        bool active = playerAugmentController != null &&
                      playerAugmentController.ShouldUseRadialLongbowVolleyMutation(this);
        if (!active)
        {
            _hadRadialLongbowMutationLastFrame = false;
            return;
        }

        if (!_hadRadialLongbowMutationLastFrame)
            _nextRadialLongbowAutoVolleyTime = Time.time;

        _hadRadialLongbowMutationLastFrame = true;

        if (Time.time < _nextRadialLongbowAutoVolleyTime) return;

        float baseDamage = stats != null ? stats.lightAttackDamage : 0f;
        FireRadialLongbowMutationAutoVolley(baseDamage);
        _nextRadialLongbowAutoVolleyTime = Time.time + Mathf.Max(0.05f, radialLongbowAutoVolleyIntervalSeconds);
    }

    private void FireRadialLongbowMutationAutoVolley(float lightDamage)
    {
        float dmgMult = playerAugmentController != null ? playerAugmentController.OutgoingDamageMultiplier : 1f;
        float arrowSpdMult = playerAugmentController != null ? playerAugmentController.ArrowProjectileSpeedMultiplier : 1f;
        float useSpeed = arrowSpeed * arrowSpdMult;
        float useDamage = lightDamage * dmgMult;
        bool chargedExplosion = playerAugmentController != null && playerAugmentController.HasChargedLongbowAoe;

        Vector2 radialOrigin = transform.position;
        float targetDistance = Mathf.Max(0.5f, radialLongbowAutoVolleyTravelDistance);
        int volleyCount = Mathf.Clamp(autoArrowVolleyCount, 1, 64);
        float step = Mathf.Max(1f, autoArrowVolleyAngleStepDegrees);

        for (int i = 0; i < volleyCount; i++)
        {
            Vector2 radialDir = RadialBowDirFromAngleDegrees(i * step);
            float forward = Mathf.Max(0f, radialLongbowSpawnInset);
            Vector2 spawnPos = radialOrigin + radialDir * forward;
            Vector2 shotTarget = radialOrigin + radialDir * Mathf.Max(forward + 0.3f, targetDistance);
            TrySpawnSinglePlayerArrow(spawnPos, shotTarget, useSpeed, useDamage, chargedExplosion);
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
        float explosionRadius = chargedExplosionEnabled && playerAugmentController != null
            ? playerAugmentController.ChargedLongbowAoeRadius
            : 0f;

        PlayerArrow mover = null;
        if (PlayerArrowPooler.Instance != null)
            PlayerArrowPooler.Instance.GetArrow(spawnWorldPosition, Quaternion.identity, a => mover = a);

        if (mover == null)
        {
            if (arrowPrefab == null) return;
            GameObject go = Instantiate(arrowPrefab, spawnWorldPosition, Quaternion.identity);
            mover = go.GetComponent<PlayerArrow>();
            if (mover == null) { Destroy(go); return; }
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
            playerAugmentController != null ? playerAugmentController.LongbowFreezeDuration : 0f,
            playerAugmentController != null && playerAugmentController.HasLongbowFreezeUnlock,  // hasIceArrow
            playerAugmentController != null && playerAugmentController.HasFireArrowUnlock,
            playerAugmentController != null ? playerAugmentController.FireDotDuration : 0f,
            playerAugmentController != null ? playerAugmentController.FireDotDamagePerSecond : 0f,
            playerAugmentController != null && playerAugmentController.HasPoisonArrowUnlock,
            playerAugmentController != null ? playerAugmentController.PoisonDotDuration : 0f,
            playerAugmentController != null ? playerAugmentController.PoisonDotDamagePerSecond : 0f);
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

    private void CancelPendingLongbowArrow()
    {
        StopAllCoroutines();
    }

    // ─── Crossbow / bolt ─────────────────────────────────────────────────────

    private void ScheduleCrossbowBolt(float damage, Vector2 aimWorld)
    {
        float delay = Mathf.Max(0f, crossbowBoltReleaseDelay);
        if (delay <= 0f) { SpawnCrossbowBolt(damage, aimWorld); return; }
        StartCoroutine(CrossbowBoltSpawnAfterDelay(delay, damage, aimWorld));
    }

    private IEnumerator CrossbowBoltSpawnAfterDelay(float delay, float damage, Vector2 aimWorld)
    {
        yield return new WaitForSeconds(delay);
        SpawnCrossbowBolt(damage, aimWorld);
    }

    private void SpawnCrossbowBolt(float damage, Vector2 aimWorld)
    {
        if (attackPoint == null) return;

        PlayerAugmentController aug = playerAugmentController;
        float dmgMult   = aug != null ? aug.OutgoingDamageMultiplier : 1f;
        float useSpeed  = arrowSpeed * crossbowBoltSpeedMultiplier;
        float useDamage = damage * dmgMult;

        Vector2 origin = attackPoint.position;
        Vector2 offset = aimWorld - origin;
        if (offset.sqrMagnitude < 0.0001f) offset = Vector2.right * 0.01f;
        Vector2 dir = offset.normalized;
        float targetDistance = Mathf.Max(0.5f, offset.magnitude);
        int boltCount = aug != null ? Mathf.Max(1, aug.ProjectileShotMultiplier) : 1;
        float spreadStepDegrees = GetArrowSpreadStepDegrees(boltCount);
        float centerOffset = (boltCount - 1) * 0.5f;

        for (int i = 0; i < boltCount; i++)
        {
            float angleOffset = (i - centerOffset) * spreadStepDegrees;
            Vector2 shotDir = Quaternion.Euler(0f, 0f, angleOffset) * dir;
            TrySpawnSingleCrossbowBolt(origin, origin + shotDir * targetDistance, useSpeed, useDamage);
        }
    }

    private void TrySpawnSingleCrossbowBolt(Vector2 origin, Vector2 aimWorld, float useSpeed, float useDamage)
    {
        PlayerAugmentController aug = playerAugmentController;
        PlayerBolt bolt = null;
        if (PlayerArrowPooler.Instance != null)
            PlayerArrowPooler.Instance.GetBolt(origin, Quaternion.identity, b => bolt = b);

        if (bolt == null && crossbowBoltPrefab != null)
        {
            GameObject boltGO = Instantiate(crossbowBoltPrefab, origin, Quaternion.identity);
            bolt = boltGO.GetComponent<PlayerBolt>();
            if (bolt == null) { Destroy(boltGO); return; }
        }

        if (bolt == null) return;

        bolt.Initialize(
            aimWorld,
            useSpeed,
            useDamage,
            crossbowBoltMaxLifetime,
            enemyLayers,
            transform,
            hasPierce:                aug != null && aug.HasCrossbowBoltPierce,
            pierceFalloff:            aug != null ? aug.CrossbowPierceDamageFalloff    : 0.20f,
            pierceFloor:              aug != null ? aug.CrossbowPierceDamageFloor      : 0.30f,
            pierceFalloffCount:       aug != null ? aug.CrossbowPierceFalloffCount     : 3,
            hasBleed:                 aug != null && aug.HasCrossbowBoltBleed,
            bleedDamageRatioPerStack: aug != null ? aug.CrossbowBleedDamageRatioPerStack : 0.01f,
            bleedMaxStacks:           aug != null ? aug.CrossbowBleedMaxStacks          : 5,
            bleedExpireSeconds:       aug != null ? aug.CrossbowBleedExpireSeconds      : 5f
        );
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

    public void ResetForDungeonExit()
    {
        playerAugmentController?.ResetAll();
        playerLevel?.Reset();
        _currentHealth = MaxHealth;
        NotifyHealthChanged();
        if (playerCurrency != null)
            playerCurrency.NotifyGoldChanged();
    }

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
