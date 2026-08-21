using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using Unity.Cinemachine;

public partial class Player : BaseEntity, IPlayerContext
{
    [Header("Data Reference")]
    [SerializeField] private EntityStats stats;

    [Header("Sound")]
    private AudioSource _audioSource;
    public AudioClip hurtSFX;
    public AudioClip deadSFX;
    public AudioClip arrowAttackSFX;
    public AudioClip hammerAttackSFX;

    [Header("Damage")]
    [SerializeField] private float damageInvulnerabilityDuration = 0.2f;
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private float damageFlashDuration = 0.5f;
    [Tooltip("If empty, auto-detected from root or children.")]
    [SerializeField] private SpriteRenderer flashTarget;

    [Header("Death")]
    [Tooltip("Death animasyonunun bitmesini bekleyip UI'i o sekilde tetiklemek icin sure (saniye).")]
    [SerializeField] private float deathAnimationDuration = 0.4f;

    [Header("References")]
    public Transform attackPoint;
    [SerializeField] private Animator animator;
    public LayerMask enemyLayers;
    private PlayerCurrency playerCurrency;
    private PlayerAugmentController playerAugmentController;
    private PlayerImpactFeedback impactFeedback;

    private Rigidbody2D _rb;
    private Collider2D _collider;
    private CinemachineImpulseSource _defaultImpulseSource;
    private float _invulnerableUntil = 0f;
    private PlayerState _currentState;
    private Color _flashOriginalColor;
    private Coroutine _damageFlashCoroutine;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    public event Action<float, float> HealthChanged;
    public event Action Died;

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
    float IPlayerContext.LightImpactFallbackDelay => lightImpactFallbackDelay;
    Slider IPlayerContext.LongbowChargeMeter => longbowChargeMeter;
    GameObject IPlayerContext.LongbowMeterCanvas => longbowMeterCanvas;
    float IPlayerContext.ArrowShotCooldown => arrowShotCooldown;

    float IPlayerContext.NextArrowShotTime
    {
        get => _nextArrowShotTime;
        set => _nextArrowShotTime = value;
    }
    float IPlayerContext.CrossbowBoltSpeedMultiplier => crossbowBoltSpeedMultiplier;
    float IPlayerContext.CrossbowAttackRate => crossbowAttackRate;
    float IPlayerContext.CrossbowBoltReleaseDelay => crossbowBoltReleaseDelay;
    GameObject IPlayerContext.CrossbowBoltPrefab => crossbowBoltPrefab;
    float IPlayerContext.CrossbowBoltMaxLifetime => crossbowBoltMaxLifetime;
    Animator IPlayerContext.Animator => animator;

    float IPlayerContext.NextCrossbowAttackTime
    {
        get => _nextCrossbowAttackTime;
        set => _nextCrossbowAttackTime = value;
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

    void IPlayerContext.ScheduleLongbowArrow(float chargeFraction, Vector2 aimWorldAtFireInput)
        => ScheduleLongbowArrow(chargeFraction, aimWorldAtFireInput);

    void IPlayerContext.ScheduleCrossbowBolt(Vector2 aimWorldAtFireInput)
        => ScheduleCrossbowBolt(aimWorldAtFireInput);

    Vector2 IPlayerContext.GetLongbowAimWorldPointAtCurrentMouse()
        => GetLongbowAimWorldPointAtCurrentMouse();

    void IPlayerContext.TriggerHeavyAttack(float chargeFraction)
        => TriggerHeavyAttack(chargeFraction);

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
        if (Mathf.Approximately(addedAmount, 0f)) return;
        _currentHealth = Mathf.Clamp(_currentHealth + addedAmount, 1f, MaxHealth);
        NotifyHealthChanged();
    }

    // ─── Unity lifecycle ──────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody2D>();
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _collider = GetComponent<Collider2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _defaultImpulseSource = GetComponent<CinemachineImpulseSource>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (playerCurrency == null)
            playerCurrency = GetComponent<PlayerCurrency>();
        if (playerAugmentController == null)
            playerAugmentController = GetComponent<PlayerAugmentController>()
                ?? GetComponentInChildren<PlayerAugmentController>(true);
        if (impactFeedback == null)
            impactFeedback = GetComponent<PlayerImpactFeedback>();
        if (flashTarget == null)
            flashTarget = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        if (flashTarget != null)
            _flashOriginalColor = flashTarget.color;

        _currentState = new IdleState();
        _currentState.Enter(this);

        NotifyHealthChanged();
        if (playerCurrency != null)
            playerCurrency.NotifyGoldChanged();
    }

    void OnDisable()
    {
        CancelPendingLongbowArrow();
    }

    void Update()
    {
        if (IsDead) return;

        _currentState.Handle(this);
        HandleLightImpactFallback();
        HandleHeavyImpactFallback();
        UpdateRadialLongbowAutoVolley();
        UpdateHammerMagnet();
    }

    void FixedUpdate()
    {
        if (IsDead) return;

        Move();
    }

    // ─── Damage / health ──────────────────────────────────────────────────────

    public override void TakeDamage(float amount, bool isHeavy)
    {
        if (Time.time < _invulnerableUntil) return;

        PlaySFX(hurtSFX);

        if (_currentState.IsChargeMeterFull(this) && playerAugmentController != null && playerAugmentController.HasHammerChargeDamageReductionUnlock)
            amount *= 0.75f;

        if (playerAugmentController != null && playerAugmentController.IncomingDamageReduction > 0f)
            amount *= 1f - playerAugmentController.IncomingDamageReduction;

        base.TakeDamage(amount, isHeavy);
        NotifyHealthChanged();
        _invulnerableUntil = Time.time + Mathf.Max(0f, damageInvulnerabilityDuration);
        if (_damageFlashCoroutine != null) StopCoroutine(_damageFlashCoroutine);
        _damageFlashCoroutine = StartCoroutine(DamageFlashRoutine());
        
    }

    private IEnumerator DamageFlashRoutine()
    {
        if (flashTarget == null) yield break;
        flashTarget.color = damageFlashColor;
        yield return new WaitForSeconds(damageFlashDuration);
        flashTarget.color = _flashOriginalColor;
        _damageFlashCoroutine = null;
    }

    protected override void Die()
    {
        PlaySFX(deadSFX);
        _currentHealth = 0f;
        _rb.linearVelocity = Vector2.zero;
        if (_collider != null) _collider.enabled = false;
        SetState(new DiedState());
        StartCoroutine(DeathSequenceRoutine());
        
    }

    private IEnumerator DeathSequenceRoutine()
    {
        yield return new WaitForSeconds(deathAnimationDuration);
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

    // ─── Misc ─────────────────────────────────────────────────────────────────

    public void ResetForDungeonExit()
    {
        playerAugmentController?.ResetAll();
        _currentHealth = MaxHealth;
        NotifyHealthChanged();
        if (playerCurrency != null)
            playerCurrency.NotifyGoldChanged();
    }

    public void PlaySFX(AudioClip audioclip)
    {
        if (audioclip == null || _audioSource == null) return;
        _audioSource.PlayOneShot(audioclip);
    }
}
