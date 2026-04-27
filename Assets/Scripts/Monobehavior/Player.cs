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
    [Tooltip("Sağ tık şarj çubuğu tam dolu (charge slider = 1) iken gelen hasardan düşürülecek oran. 0 = azaltma yok, 0.85 = %85 azaltma (cana gelen ≈ %15).")]
    [Range(0f, 1f)]
    [FormerlySerializedAs("chargeFullDamageReduction")]
    public float heavyChargeFullDamageReduction = 0.85f;
    [SerializeField] private float heavyImpactFallbackDelay = 0.2f;

    [Header("Light Attack Settings (Spammable)")]
    public float lightAttackRate = 0.2f; 
    public float lightAttackRange = 1.5f; 
    public float lightAttackDuration = 0.1f; 
    [SerializeField] private float lightImpactFallbackDelay = 0.08f;
    private float _nextAttackTime = 0f;
    private float _lastLightAttackResolveTime = -999f;
    private bool _lightAttackInProgress = false;
    private float _lightFallbackExecuteAt = -1f;
    private float _lastHeavyResolveTime = -999f;
    private bool _heavyAttackInProgress = false;
    private float _heavyFallbackExecuteAt = -1f;

    [Header("Movement Modifiers")]
    [SerializeField] private float dungeonEntryBaseSpeedMultiplier = 1.25f;
    [Header("Damage")]
    [SerializeField] private float damageInvulnerabilityDuration = 0.2f;

    [Header("References")]
    public Transform hammerPivot;
    public Transform attackPoint;
    [SerializeField] private Animator animator;
    public LayerMask enemyLayers;
    [SerializeField] private float goldCount;
    [SerializeField] private float experienceCount;
    [SerializeField] private float requiredExperienceForNextLevel = 100f;
    [SerializeField] private int currentLevel = 1;

    [Header("Hammer Cooldown UI")]
    public Slider hammerCooldownBar;
    public GameObject hammerCooldownCanvas;
    [SerializeField] private bool showCooldownBarWhenReady = true;
    
    [Header("Hammer Charge UI")]
    public Slider chargeMeter;
    public GameObject meterCanvas;

    [Header("Hit Stop")]
    public HitStopManager hitStopManager;
    [Range(0.01f, 1f)] public float lightHitStopTimeScale = 0.14f;
    public float lightHitStopDuration = 0.02f;
    [Range(0.01f, 1f)] public float heavyHitStopTimeScale = 0.08f;
    public float heavyHitStopDuration = 0.045f;

    [Header("Impact Feedback")]
    public CinemachineImpulseSource lightHitImpulse;
    public GameObject hitVfxPrefab;
    public AudioSource hitAudioSource;
    public AudioClip lightHitSfx;
    public AudioClip heavyHitSfx;
    [Range(0.5f, 1f)] public float lightHitPitch = 0.92f;
    [Range(0.5f, 1f)] public float heavyHitPitch = 0.82f;

    private float _currentCharge = 0f;
    private bool _isCharging = false;
    private float _nextHammerUseTime = 0f;
    private Rigidbody2D _rb; // FİZİK İÇİN ŞART
    private CinemachineImpulseSource _defaultImpulseSource;
    private float _baseSpeedMultiplier = 1f;
    private bool _hasAppliedDungeonEntryBoost = false;
    private float _invulnerableUntil = 0f;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
    private static readonly int LightAttackHash = Animator.StringToHash("LightAttack");
    private static readonly int HeavyAttackHash = Animator.StringToHash("HeavyAttack");

    public event Action<float, float> HealthChanged;
    public event Action<float, float> ExperienceChanged;
    public event Action<int> LevelChanged;
    public event Action<float> GoldChanged;

    public float GoldCount => goldCount;
    public float ExperienceCount => experienceCount;
    public int CurrentLevel => currentLevel;

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

        if (hitStopManager == null)
            hitStopManager = HitStopManager.Instance;

        InitializeHammerCooldownUI();
        NotifyHealthChanged();
        NotifyExperienceChanged();
        NotifyLevelChanged();
        NotifyGoldChanged();
    }

    void Update()
    {
        HandleHammerCharge(); // Sağ Tık
        HandleLightAttack();  // Sol Tık
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

    public void AddGold(float amount)
    {
        if (amount <= 0f) return;
        goldCount += amount;
        NotifyGoldChanged();
    }

    public void AddExperience(float amount)
    {
        if (amount <= 0f) return;

        experienceCount += amount;
        int levelUps = 0;
        float requiredExperience = Mathf.Max(1f, requiredExperienceForNextLevel);

        while (experienceCount >= requiredExperience)
        {
            experienceCount -= requiredExperience;
            levelUps++;
        }

        if (levelUps > 0)
        {
            currentLevel += levelUps;
            OnLevelUp();
            NotifyLevelChanged();
        }

        ExperienceChanged?.Invoke(experienceCount, requiredExperience);
    }

    public void NotifyExperienceChanged()
    {
        ExperienceChanged?.Invoke(experienceCount, Mathf.Max(1f, requiredExperienceForNextLevel));
    }

    public void NotifyHealthChanged()
    {
        HealthChanged?.Invoke(CurrentHealth, Mathf.Max(1f, MaxHealth));
    }

    public void NotifyLevelChanged()
    {
        LevelChanged?.Invoke(Mathf.Max(1, currentLevel));
    }

    public void NotifyGoldChanged()
    {
        GoldChanged?.Invoke(Mathf.Max(0f, goldCount));
    }

    protected override void Move()
    {
        float moveX = Input.GetAxisRaw("Horizontal"); 
        float moveY = Input.GetAxisRaw("Vertical");   

        Vector2 direction = new Vector2(moveX, moveY).normalized;
        
        // Tüm hız modifiyerleri dungeon girişindeki baz çarpanın üstüne uygulanır.
        float chargeMultiplier = _isCharging ? 0.3f : 1f;
        float currentSpeed = stats.moveSpeed * _baseSpeedMultiplier * chargeMultiplier;
        
        // KRİTİK DÜZELTME: transform.Translate SİLİNDİ, Rigidbody Velocity GELDİ!
        _rb.linearVelocity = direction * currentSpeed;
        if (animator != null)
            animator.SetFloat(SpeedHash, direction.magnitude);

        // --- FLIP MANTIĞI ---
        if (moveX > 0) transform.localScale = new Vector3(1f, 1f, 1f);
        else if (moveX < 0) transform.localScale = new Vector3(-1f, 1f, 1f);
    }

    public void ApplyDungeonEntrySpeedBoost()
    {
        if (_hasAppliedDungeonEntryBoost) return;
        _baseSpeedMultiplier *= Mathf.Max(1f, dungeonEntryBaseSpeedMultiplier);
        _hasAppliedDungeonEntryBoost = true;
    }

    private bool IsChargeMeterFullWhileCharging()
    {
        if (!_isCharging) return false;
        if (chargeMeter != null)
            return chargeMeter.value >= 1f - 0.0001f;
        return _currentCharge >= maxChargeTime - 0.0001f;
    }

    private void HandleLightAttack()
    {
        if (_isCharging) return;
        if (Input.GetButtonDown("Fire1") && Time.time >= _nextAttackTime)
        {
            if (animator != null)
                animator.SetTrigger(LightAttackHash);
            _lightAttackInProgress = true;
            _lightFallbackExecuteAt = Time.time + Mathf.Max(0.03f, lightImpactFallbackDelay);
            _nextAttackTime = Time.time + lightAttackRate;
        }
    }

    // Animation Event: LightAttack clip'inde hasar anına eklenmeli.
    public void LightAttack()
    {
        ResolveLightAttackDamage();
    }

    private void ResolveLightAttackDamage()
    {
        if (Time.time - _lastLightAttackResolveTime < Mathf.Max(0.01f, lightAttackDuration * 0.5f))
            return;
        _lastLightAttackResolveTime = Time.time;
        _lightAttackInProgress = false;
        _lightFallbackExecuteAt = -1f;
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, lightAttackRange, enemyLayers);
        int successfulHits = 0;
        Vector3 firstHitPosition = attackPoint.position;
        foreach (Collider2D enemy in hitEnemies)
        {
            IDamageable target = enemy.GetComponent<IDamageable>() ?? enemy.GetComponentInParent<IDamageable>();
            if (target == null) continue;
            target.TakeDamage(stats.lightAttackDamage, false);
            if (successfulHits == 0)
                firstHitPosition = enemy.ClosestPoint(attackPoint.position);
            successfulHits++;
        }

        if (successfulHits > 0)
        {
            SpawnHitVfx(firstHitPosition);
            PlayImpactFeedback(false);
        }
    }

    private void HandleHammerCharge()
    {
        if (Time.time < _nextHammerUseTime)
        {
            if (_isCharging) ResetCharge();
            return;
        }

        if (Input.GetButton("Fire2"))
        {
            _isCharging = true;
            if (animator != null)
                animator.SetBool(IsChargingHash, true);
            if (meterCanvas != null)
                meterCanvas.SetActive(true);
            _currentCharge += Time.deltaTime;
            _currentCharge = Mathf.Clamp(_currentCharge, 0f, maxChargeTime);
            if (chargeMeter != null)
                chargeMeter.value = _currentCharge / Mathf.Max(0.0001f, maxChargeTime);
        }

        if (Input.GetButtonUp("Fire2"))
        {
            if (_currentCharge >= maxChargeTime)
            {
                TriggerHeavyAttack();
            }
            ResetCharge();
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
            SpawnHitVfx(firstHitPosition);
            PlayImpactFeedback(true);
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
        ResolveLightAttackDamage();
    }

    private void PlayImpactFeedback(bool isHeavy)
    {
        if (hitStopManager == null)
            hitStopManager = HitStopManager.Instance != null
                ? HitStopManager.Instance
                : UnityEngine.Object.FindAnyObjectByType<HitStopManager>();

        if (hitStopManager == null)
        {
            GameObject managerObject = new GameObject("HitStopManager");
            hitStopManager = managerObject.AddComponent<HitStopManager>();
        }

        if (hitStopManager != null)
        {
            float rawTimeScale = isHeavy ? heavyHitStopTimeScale : lightHitStopTimeScale;
            float rawDuration = isHeavy ? heavyHitStopDuration : lightHitStopDuration;

            // Inspector'da agresif değer kalsa bile hissi "kasma"ya çevirmemek için güvenli aralık.
            float minimumSmoothScale = isHeavy ? 0.08f : 0.14f;
            float maximumSmoothDuration = isHeavy ? 0.045f : 0.02f;
            float timeScale = Mathf.Clamp(rawTimeScale, minimumSmoothScale, 1f);
            float duration = Mathf.Clamp(rawDuration, 0f, maximumSmoothDuration);
            hitStopManager.TriggerHitStop(timeScale, duration);
        }

        if (!isHeavy)
        {
            CinemachineImpulseSource lightImpulseSource = lightHitImpulse != null ? lightHitImpulse : _defaultImpulseSource;
            if (lightImpulseSource != null)
                lightImpulseSource.GenerateImpulse();
        }

        PlayHitSfx(isHeavy);
    }

    private void PlayHitSfx(bool isHeavy)
    {
        if (hitAudioSource == null) return;

        AudioClip clip = isHeavy ? heavyHitSfx : lightHitSfx;
        if (clip == null) return;

        float previousPitch = hitAudioSource.pitch;
        hitAudioSource.pitch = isHeavy ? heavyHitPitch : lightHitPitch;
        hitAudioSource.PlayOneShot(clip);
        hitAudioSource.pitch = previousPitch;
    }

    private void SpawnHitVfx(Vector3 worldPos)
    {
        if (hitVfxPrefab == null) return;
        Instantiate(hitVfxPrefab, worldPos, Quaternion.identity);
    }

    private void ResetCharge()
    {
        _isCharging = false;
        _currentCharge = 0f;
        if (chargeMeter != null)
            chargeMeter.value = 0f;
        if (meterCanvas != null)
            meterCanvas.SetActive(false);
        if (animator != null)
            animator.SetBool(IsChargingHash, false);
    }

    private void OnLevelUp()
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