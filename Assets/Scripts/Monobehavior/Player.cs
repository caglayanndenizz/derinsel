using UnityEngine;
using UnityEngine.UI;
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

    [Header("Light Attack Settings (Spammable)")]
    public float lightAttackRate = 0.2f; 
    public float lightAttackRange = 1.5f; 
    public float lightAttackDuration = 0.1f; 
    private float _nextAttackTime = 0f;

    [Header("Movement Modifiers")]
    [SerializeField] private float dungeonEntryBaseSpeedMultiplier = 1.25f;
    [Header("Damage")]
    [SerializeField] private float damageInvulnerabilityDuration = 0.2f;

    [Header("References")]
    public Transform hammerPivot;
    public Transform attackPoint;
    public LayerMask enemyLayers;
    [SerializeField] private float goldCount;
    [SerializeField] private float experienceCount;
    [SerializeField] private float requiredExperienceForNextLevel = 100f;

    [Header("Hammer Cooldown UI")]
    public Slider hammerCooldownBar;
    public GameObject hammerCooldownCanvas;
    [SerializeField] private bool showCooldownBarWhenReady = true;

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

    public event Action<float, float> HealthChanged;
    public event Action<float, float> ExperienceChanged;

    public float GoldCount => goldCount;
    public float ExperienceCount => experienceCount;

    protected override void Awake()
    {
        base.Awake();
        // Rigidbody referansını al ve ayarla
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f; // 2D Top-down olduğu için yerçekimini kapat
        _rb.freezeRotation = true; // Karakterin devrilmesini engelle
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // Duvar delmeyi engeller
        _defaultImpulseSource = GetComponent<CinemachineImpulseSource>();

        if (hitStopManager == null)
            hitStopManager = HitStopManager.Instance;

        InitializeHammerCooldownUI();
        NotifyHealthChanged();
        NotifyExperienceChanged();
    }

    void Update()
    {
        HandleHammerCharge(); // Sağ Tık
        HandleLightAttack();  // Sol Tık
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

        base.TakeDamage(amount, isHeavy);
        NotifyHealthChanged();
        _invulnerableUntil = Time.time + Mathf.Max(0f, damageInvulnerabilityDuration);
    }

    public void AddGold(float amount)
    {
        if (amount <= 0f) return;
        goldCount += amount;
    }

    public void AddExperience(float amount)
    {
        if (amount <= 0f) return;
        experienceCount += amount;
        ExperienceChanged?.Invoke(experienceCount, Mathf.Max(1f, requiredExperienceForNextLevel));
    }

    public void NotifyExperienceChanged()
    {
        ExperienceChanged?.Invoke(experienceCount, Mathf.Max(1f, requiredExperienceForNextLevel));
    }

    public void NotifyHealthChanged()
    {
        HealthChanged?.Invoke(CurrentHealth, Mathf.Max(1f, MaxHealth));
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

    private void HandleLightAttack()
    {
        if (_isCharging) return;
        if (Input.GetButtonDown("Fire1") && Time.time >= _nextAttackTime)
        {
            LightAttack();
            _nextAttackTime = Time.time + lightAttackRate;
        }
    }

    private void LightAttack()
    {
        StartCoroutine(LightSwingRoutine());
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, lightAttackRange, enemyLayers);
        int successfulHits = 0;
        Vector3 firstHitPosition = attackPoint.position;
        foreach (Collider2D enemy in hitEnemies)
        {
            IDamageable target = enemy.GetComponent<IDamageable>();
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

    private IEnumerator LightSwingRoutine()
    {
        hammerPivot.localRotation = Quaternion.Euler(0, 0, -45f);
        yield return new WaitForSeconds(lightAttackDuration);
        hammerPivot.localRotation = Quaternion.identity;
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
            _currentCharge += Time.deltaTime;
            _currentCharge = Mathf.Clamp(_currentCharge, 0f, maxChargeTime);
            hammerPivot.localRotation = Quaternion.Euler(0, 0, (_currentCharge / maxChargeTime) * 90f);
        }

        if (Input.GetButtonUp("Fire2"))
        {
            if (_currentCharge >= maxChargeTime)
            {
                hammerPivot.localRotation = Quaternion.identity;
                HammerSlam(); 
            }
            ResetCharge();
        }
    }

    private void HammerSlam()
    {
        _nextHammerUseTime = Time.time + hammerCooldown;
        UpdateHammerCooldownUI();

        CinemachineImpulseSource source = _defaultImpulseSource;
        if (source != null) source.GenerateImpulse(); 
        
        if (generator != null) generator.BreakWallsInArea(attackPoint.position, hammerAOE);

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, hammerAOE, enemyLayers);
        int successfulHits = 0;
        Vector3 firstHitPosition = attackPoint.position;
        foreach (Collider2D enemy in hitEnemies)
        {
            IDamageable target = enemy.GetComponent<IDamageable>();
            if (target == null) continue;
            BaseEntity targetEntity = enemy.GetComponent<BaseEntity>();
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
        hammerPivot.localRotation = Quaternion.identity;
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