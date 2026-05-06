using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using System.Collections;
using System;
using Unity.Cinemachine;

public class Player : BaseEntity
{
    [Header("External References")]
    public DungeonGenerator generator;
    
    [Header("Hammer Settings (Heavy)")]
    public float maxChargeTime = 0.5f;
    public float hammerAOE = 2.5f;
    public float hammerCooldown = 3f;
    [Tooltip("Sol tık (çekiç) şarj çubuğu tam dolu (charge slider = 1) iken gelen hasardan düşürülecek oran. 0 = azaltma yok, 0.85 = %85 azaltma (cana gelen ≈ %15).")]
    [Range(0f, 1f)]
    [FormerlySerializedAs("chargeFullDamageReduction")]
    public float heavyChargeFullDamageReduction = 0.85f;
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
    [Tooltip("Tam şarjlı ok isabet ettiğinde duvar kırma ve IDamageable yok etme yarıçapı (dünya birimi).")]
    public float bowChargedExplosionRadius = 3f;
    [Tooltip("Sağ tık basılı tutma süresi (saniye); dolunca yay tam şarj sayılır.")]
    public float maxBowChargeTime = 0.5f;
    [Tooltip("Boşsa yay şarj çubuğu gösterilmez; Inspector'dan atayabilirsin.")]
    public Slider bowChargeMeter;
    [Tooltip("Boşsa yay şarj UI kanvası açılmaz.")]
    public GameObject bowMeterCanvas;

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
    private Coroutine _bowArrowSpawnCoroutine;

    [Header("Damage")]
    [SerializeField] private float damageInvulnerabilityDuration = 0.2f;

    [Header("References")]
    public Transform attackPoint;
    [SerializeField] private Animator animator;
    public LayerMask enemyLayers;
    private PlayerLevel playerLevel;
    private PlayerCurrency playerCurrency;
    private PlayerAugmentController playerAugmentController;

    [Header("Hammer Cooldown UI")]
    public Slider hammerCooldownBar;
    public GameObject hammerCooldownCanvas;
    [SerializeField] private bool showCooldownBarWhenReady = true;
    
    [Header("Hammer Charge UI")]
    public Slider chargeMeter;
    public GameObject meterCanvas;

    [Header("Impact Feedback")]
    private PlayerImpactFeedback impactFeedback;

    private float _currentCharge = 0f;
    private bool _isHammerCharging = false;
    private float _bowCharge = 0f;
    private bool _isBowCharging = false;
    private float _nextHammerUseTime = 0f;
    private Rigidbody2D _rb; // FİZİK İÇİN ŞART
    private CinemachineImpulseSource _defaultImpulseSource;
    private float _invulnerableUntil = 0f;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
    private static readonly int BowChargeHash = Animator.StringToHash("BowCharge");
    private static readonly int LightAttackHash = Animator.StringToHash("LightAttack");
    private static readonly int HeavyAttackHash = Animator.StringToHash("HeavyAttack");

    public event Action<float, float> HealthChanged;

    public PlayerLevel PlayerLevel => playerLevel;
    public PlayerCurrency PlayerCurrency => playerCurrency;
    public PlayerAugmentController PlayerAugmentController => playerAugmentController;

    protected override void Awake()
    {
        base.Awake();
        // Rigidbody referansını al ve ayarla
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f; // 2D Top-down olduğu için yerçekimini kapat
        _rb.freezeRotation = true; // Karakterin devrilmesini engelle
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // Duvar delmeyi engeller
        _defaultImpulseSource = GetComponent<CinemachineImpulseSource>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (playerLevel == null)
            playerLevel = GetComponent<PlayerLevel>();
        if (playerCurrency == null)
            playerCurrency = GetComponent<PlayerCurrency>();
        if (playerAugmentController == null)
            playerAugmentController = GetComponent<PlayerAugmentController>();
        if (impactFeedback == null)
            impactFeedback = GetComponent<PlayerImpactFeedback>();

        InitializeHammerCooldownUI();
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
        HandleHammerCharge(); // Sol tık (çekiç şarjı)
        HandleBowChargeAndRelease(); // Sağ tık (yay şarjı + bırakınca ok)
        HandleLightImpactFallback();
        HandleHeavyImpactFallback();
        UpdateHammerCooldownUI();
    }

    // FİZİK HAREKETİ BURADA OLMALI
    void FixedUpdate()
    {
        Move();
    }

    public override void TakeDamage(float amount, bool isHeavy)
    {
        if (Time.time < _invulnerableUntil) return;

        if (IsChargeMeterFullWhileCharging())
            amount *= Mathf.Clamp01(1f - heavyChargeFullDamageReduction);

        base.TakeDamage(amount, isHeavy);
        NotifyHealthChanged();
        _invulnerableUntil = Time.time + Mathf.Max(0f, damageInvulnerabilityDuration);
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

    protected override void Move()
    {
        float moveX = Input.GetAxisRaw("Horizontal"); 
        float moveY = Input.GetAxisRaw("Vertical");   

        Vector2 direction = new Vector2(moveX, moveY).normalized;
        
        float augmentSpeedBonus = playerAugmentController != null ? playerAugmentController.MovementSpeedBonus : 0f;
        float chargeMultiplier = (_isHammerCharging || _isBowCharging) ? 0.3f : 1f;
        float currentSpeed = stats.moveSpeed * (1f + augmentSpeedBonus) * chargeMultiplier;
        
        // KRİTİK DÜZELTME: transform.Translate SİLİNDİ, Rigidbody Velocity GELDİ!
        _rb.linearVelocity = direction * currentSpeed;
        if (animator != null)
            animator.SetFloat(SpeedHash, direction.magnitude);

        // --- FLIP MANTIĞI ---
        if (moveX > 0) transform.localScale = new Vector3(1f, 1f, 1f);
        else if (moveX < 0) transform.localScale = new Vector3(-1f, 1f, 1f);
    }

    private bool IsChargeMeterFullWhileCharging()
    {
        if (!_isHammerCharging) return false;
        if (chargeMeter != null)
            return chargeMeter.value >= 1f - 0.0001f;
        return _currentCharge >= maxChargeTime - 0.0001f;
    }

    private void HandleBowChargeAndRelease()
    {
        if (Input.GetButtonUp("Fire2"))
        {
            bool wasFullBow = _bowCharge >= maxBowChargeTime - 0.0001f;
            ResetBowChargeState();

            if (Time.time >= _nextAttackTime)
            {
                if (animator != null)
                    animator.SetTrigger(LightAttackHash);
                Vector2 aimAtRelease = GetBowAimWorldPointAtCurrentMouse();
                ScheduleBowArrow(stats != null ? stats.lightAttackDamage : 0f, wasFullBow, aimAtRelease);
                _lightAttackInProgress = true;
                _lightFallbackExecuteAt = Time.time + Mathf.Max(0.03f, lightImpactFallbackDelay);
                _nextAttackTime = Time.time + lightAttackRate;
            }
        }

        // BowCharge anim / yürüme yavaşlatma: sağ tık basılıyken
        _isBowCharging = Input.GetButton("Fire2");

        if (_isBowCharging)
        {
            if (bowMeterCanvas != null)
                bowMeterCanvas.SetActive(true);
            _bowCharge += Time.deltaTime;
            _bowCharge = Mathf.Clamp(_bowCharge, 0f, maxBowChargeTime);
            if (bowChargeMeter != null)
                bowChargeMeter.value = _bowCharge / Mathf.Max(0.0001f, maxBowChargeTime);
        }
        else
        {
            if (bowMeterCanvas != null)
                bowMeterCanvas.SetActive(false);
            if (_bowCharge > 0f)
            {
                _bowCharge = 0f;
                if (bowChargeMeter != null)
                    bowChargeMeter.value = 0f;
            }
        }

        UpdateChargingAnimator();
    }

    /// <summary>
    /// O anki imleç ekran pozisyonunu attackPoint Z düzlemine projekte eder (ateş anında çağır).
    /// </summary>
    private Vector2 GetBowAimWorldPointAtCurrentMouse()
    {
        if (attackPoint == null)
            return Vector2.zero;
        Camera cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null)
            return attackPoint.position;

        Vector3 mouse = Input.mousePosition;
        float planeZ = attackPoint.position.z;
        mouse.z = Mathf.Abs(cam.transform.position.z - planeZ);
        return cam.ScreenToWorldPoint(mouse);
    }

    private void SpawnArrowTowardWorld(float damage, bool useBowChargedMultiplier, Vector2 targetWorld)
    {
        if (arrowPrefab == null || attackPoint == null) return;
        Camera cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null) return;

        bool chargedShot = useBowChargedMultiplier;
        float m = chargedShot ? Mathf.Max(1f, bowChargedSpeedDamageMultiplier) : 1f;
        float useSpeed = arrowSpeed * m;
        float useDamage = damage * m;

        Vector2 origin = attackPoint.position;
        Vector2 offset = targetWorld - origin;
        if (offset.sqrMagnitude < 0.0001f)
            offset = Vector2.right * 0.01f;
        Vector2 dir = offset.normalized;
        Vector2 spawnPos = origin + dir * 0.2f;

        GameObject arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);
        PlayerArrow mover = arrow.GetComponent<PlayerArrow>();
        if (mover != null)
        {
            mover.Initialize(
                targetWorld,
                useSpeed,
                useDamage,
                arrowMaxLifetime,
                enemyLayers,
                transform,
                chargedShot,
                chargedShot ? Mathf.Max(0f, bowChargedExplosionRadius) : 0f,
                generator,
                chargedShot ? _defaultImpulseSource : null);
        }
    }

    private void CancelPendingBowArrow()
    {
        if (_bowArrowSpawnCoroutine == null) return;
        StopCoroutine(_bowArrowSpawnCoroutine);
        _bowArrowSpawnCoroutine = null;
    }

    private void ScheduleBowArrow(float damage, bool useBowChargedMultiplier, Vector2 aimWorldAtFireInput)
    {
        CancelPendingBowArrow();
        float delay = Mathf.Max(0f, bowArrowReleaseDelay);
        if (delay <= 0f)
        {
            SpawnArrowTowardWorld(damage, useBowChargedMultiplier, aimWorldAtFireInput);
            return;
        }

        _bowArrowSpawnCoroutine = StartCoroutine(BowArrowSpawnAfterDelay(delay, damage, useBowChargedMultiplier, aimWorldAtFireInput));
    }

    private IEnumerator BowArrowSpawnAfterDelay(float delaySeconds, float damage, bool useBowChargedMultiplier, Vector2 aimWorldAtFireInput)
    {
        yield return new WaitForSeconds(delaySeconds);
        SpawnArrowTowardWorld(damage, useBowChargedMultiplier, aimWorldAtFireInput);
        _bowArrowSpawnCoroutine = null;
    }

    // Animation Event: yay animasyonu vuruş karesi — ok hasarı PlayerArrow'da; burada yalnızca zamanlama/fallback senkronu.
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

    private void HandleHammerCharge()
    {
        if (Time.time < _nextHammerUseTime)
        {
            if (_isHammerCharging) ResetHammerCharge();
            return;
        }

        if (Input.GetButton("Fire1"))
        {
            _isHammerCharging = true;
            if (meterCanvas != null)
                meterCanvas.SetActive(true);
            _currentCharge += Time.deltaTime;
            _currentCharge = Mathf.Clamp(_currentCharge, 0f, maxChargeTime);
            if (chargeMeter != null)
                chargeMeter.value = _currentCharge / Mathf.Max(0.0001f, maxChargeTime);
            UpdateChargingAnimator();
        }

        if (Input.GetButtonUp("Fire1"))
        {
            if (_currentCharge >= maxChargeTime)
                TriggerHeavyAttack();
            ResetHammerCharge();
        }
    }

    private void TriggerHeavyAttack()
    {
        _nextHammerUseTime = Time.time + hammerCooldown;
        UpdateHammerCooldownUI();
        if (animator != null)
            animator.SetTrigger(HeavyAttackHash);
        _heavyAttackInProgress = true;
        _heavyFallbackExecuteAt = Time.time + Mathf.Max(0.05f, heavyImpactFallbackDelay);
    }

    // Animation Event: HeavyAttack clip'inde darbe anına eklenmeli.
    public void HammerSlam()
    {
        if (Time.time - _lastHeavyResolveTime < 0.05f)
            return;

        _lastHeavyResolveTime = Time.time;
        _heavyAttackInProgress = false;
        _heavyFallbackExecuteAt = -1f;

        CinemachineImpulseSource source = _defaultImpulseSource;
        if (source != null) source.GenerateImpulse();
        if (generator != null) generator.BreakWallsInArea(attackPoint.position, hammerAOE);

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, hammerAOE, enemyLayers);
        int successfulHits = 0;
        Vector3 firstHitPosition = attackPoint.position;
        foreach (Collider2D enemy in hitEnemies)
        {
            IDamageable target = enemy.GetComponent<IDamageable>() ?? enemy.GetComponentInParent<IDamageable>();
            if (target == null) continue;
            BaseEntity targetEntity = enemy.GetComponent<BaseEntity>() ?? enemy.GetComponentInParent<BaseEntity>();
            float heavyDamage = targetEntity != null ? targetEntity.CurrentHealth : stats.heavyAttackDamage;
            target.TakeDamage(heavyDamage, true);
            if (successfulHits == 0)
                firstHitPosition = enemy.ClosestPoint(attackPoint.position);
            successfulHits++;
        }

        if (successfulHits > 0)
        {
            impactFeedback?.PlayHeavyHit(firstHitPosition, _defaultImpulseSource);
        }
    }

    private void HandleHeavyImpactFallback()
    {
        if (!_heavyAttackInProgress) return;
        if (Time.time < _heavyFallbackExecuteAt) return;
        HammerSlam();
    }

    private void HandleLightImpactFallback()
    {
        if (!_lightAttackInProgress) return;
        if (Time.time < _lightFallbackExecuteAt) return;
        ClearLightAttackPendingState();
    }

    private void UpdateChargingAnimator()
    {
        if (animator == null) return;
        animator.SetBool(IsChargingHash, _isHammerCharging);
        animator.SetBool(BowChargeHash, _isBowCharging);
    }

    private void ResetHammerCharge()
    {
        _isHammerCharging = false;
        _currentCharge = 0f;
        if (chargeMeter != null)
            chargeMeter.value = 0f;
        if (meterCanvas != null)
            meterCanvas.SetActive(false);
        UpdateChargingAnimator();
    }

    private void ResetBowChargeState()
    {
        _isBowCharging = false;
        _bowCharge = 0f;
        if (bowChargeMeter != null)
            bowChargeMeter.value = 0f;
        if (bowMeterCanvas != null)
            bowMeterCanvas.SetActive(false);
        UpdateChargingAnimator();
    }

    private void ResetCharge()
    {
        ResetHammerCharge();
        ResetBowChargeState();
    }

    private void HandleLevelUp()
    {
        _currentHealth = MaxHealth;
        NotifyHealthChanged();
        ResetCharge();
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
            if (hammerCooldownCanvas != null)
                hammerCooldownCanvas.SetActive(showCooldownBarWhenReady);
            return;
        }

        float remaining = Mathf.Max(0f, _nextHammerUseTime - Time.time);
        float normalized = 1f - (remaining / cooldown);
        hammerCooldownBar.value = Mathf.Clamp01(normalized);

        if (hammerCooldownCanvas != null)
        {
            bool isReady = remaining <= 0f;
            hammerCooldownCanvas.SetActive(!isReady || showCooldownBarWhenReady);
        }
    }

}