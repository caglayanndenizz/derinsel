using UnityEngine;

/// <summary>
/// Coordinator: holds serialized config, the Patrol/Chase/Attack state machine and death/loot.
/// Capabilities live in sibling components (EntityStatusEffects, EntitySensor, EnemyMotor,
/// IAttackBehavior); public status methods stay here as facades so external callers
/// (Player.Hammer, PlayerArrow, PlayerBolt) are unaffected. Status-effect coloring (hit flash,
/// freeze, fire, poison) is applied directly in LateUpdate here so it always wins over any
/// Animator-driven color curve.
/// Attack style (ranged/melee) comes from the IAttackBehavior component on the prefab.
/// </summary>
public class Enemy : BaseEntity
{
    public enum State { Patrol, Chase, Attack, Dead }

    [Header("Data Reference")]
    [SerializeField] private EnemyEntityStats stats;

    public override float MaxHealth => stats != null ? stats.maxHealth : 0f;
    public EnemyEntityStats Stats => stats;

    [Header("State Settings")]
    [SerializeField] private State currentState = State.Patrol;
    public float detectionRange = 10f;
    public float expandedDetectionRange = 22f;

    public State CurrentState => currentState;

    [Header("Knockback Settings")]
    public float lightKnockbackForce = 5f;
    public float heavyKnockbackForce = 12f;
    public float knockbackDuration = 0.2f;
    [Tooltip("Corpse time after the Die animation finishes, before pool return.")]
    public float corpseLingerSeconds = 0.75f;
    private Rigidbody2D _rb;

    [Header("Navigation (Tilemap + Colliders)")]
    public float sensorLength = 1.5f;
    [Tooltip("Layers with Collider2D that block LOS. Exclude Enemy; Player is ignored in code.")]
    public LayerMask blockingEnvironmentMask = Physics2D.DefaultRaycastLayers;

    [Header("Patrol Route (spawn-based)")]
    [Tooltip("Ping-pong distance to each side of the spawn point (world X units).")]
    public float patrolLegUnits = 2f;
    [Tooltip("Idle wait (zero velocity) at each patrol endpoint before heading back, in seconds.")]
    public float patrolWaitSeconds = 1f;
    public float patrolWaypointReachDistance = 0.22f;

    [Header("Loot Prefabs")]
    public GameObject goldPrefab;
    [Range(0f, 1f)] public float goldDropChance = 0.15f;
    [Tooltip("Optional reward chest for miniboss/boss enemies. Dropped on death when assigned; left empty, normal enemies drop nothing extra.")]
    public GameObject chestPrefab;

    public event System.Action<Enemy> Died;
    /// <summary>Fired every time damage is applied — including the killing blow.</summary>
    public event System.Action Damaged;

    [Header("Sound")]
    private AudioSource _audioSource;
    public AudioClip hitSFX;

    [Header("Status Colors")]
    [Tooltip("Applied for hitFlashDuration seconds whenever this enemy takes damage.")]
    public Color hitFlashColor = Color.red;
    public float hitFlashDuration = 0.1f;
    [Tooltip("Applied for as long as IsFrozen is true.")]
    public Color freezeColor = new Color(0.3f, 0.6f, 1f);
    [Tooltip("Applied for as long as IsOnFire is true.")]
    public Color onFireColor = new Color(1f, 0.45f, 0.1f);
    [Tooltip("Applied for as long as IsPoisoned is true.")]
    public Color poisonedColor = new Color(0.2f, 0.8f, 0.2f);

    private SpriteRenderer _bodySpriteRenderer;
    private Color _bodyOriginalColor;
    private float _hitFlashUntil = -1f;

    private EntityStatusEffects _status;
    private EntitySensor _sensor;
    private EnemyMotor _motor;
    private IAttackBehavior _attack;
    private EnemyAnimator _animatorBridge; // optional; death timing falls back gracefully without it

    public IAttackBehavior AttackBehavior => _attack;

    public override bool IsDead => _isDead || _currentHealth <= 0f;
    public bool IsFrozen => _status.IsFrozen;

    protected GameObject player;
    private BaseEntity _playerEntity;
    private bool _isDead = false;
    private Vector2 _lastKnownPlayerWorld;
    private bool _hasLastKnownPlayerWorld;
    private Collider2D[] _allColliders;

    public GameObject PlayerObject => player;
    public Transform PlayerTransform => player != null ? player.transform : null;
    public bool HasLastKnownPlayerPosition => _hasLastKnownPlayerWorld;
    public Vector2 LastKnownPlayerPosition => _lastKnownPlayerWorld;

    public Vector2 ReferencePosition
        => _rb != null ? _rb.position : (Vector2)transform.position;

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody2D>();
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _allColliders = GetComponentsInChildren<Collider2D>(true);
        player = GameObject.FindGameObjectWithTag("Player");
        _playerEntity = player != null ? player.GetComponent<BaseEntity>() : null;

        _bodySpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_bodySpriteRenderer != null) _bodyOriginalColor = _bodySpriteRenderer.color;

        // Fallback: prefab'da yoksa runtime'da ekle (status önce — visuals ona abone oluyor)
        _status = GetComponent<EntityStatusEffects>();
        if (_status == null) _status = gameObject.AddComponent<EntityStatusEffects>();
        _sensor = GetComponent<EntitySensor>();
        if (_sensor == null) _sensor = gameObject.AddComponent<EntitySensor>();
        _motor = GetComponent<EnemyMotor>();
        if (_motor == null) _motor = gameObject.AddComponent<EnemyMotor>();
        _animatorBridge = GetComponent<EnemyAnimator>();

        // Saldırı stili prefab'daki component'ten gelir; eksikse melee'ye düş
        _attack = GetComponent<IAttackBehavior>();
        if (_attack == null)
        {
            Debug.LogWarning($"{name}: no IAttackBehavior component on prefab, defaulting to MeleeAttackBehavior.", this);
            _attack = gameObject.AddComponent<MeleeAttackBehavior>();
        }

        if (_rb != null)
        {
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        // Serialize edilen config Enemy'de kalır; component'lere buradan bağlanır
        _sensor.Configure(blockingEnvironmentMask, sensorLength);
        _motor.Configure(this, _sensor, _status);
        _attack.Configure(this);

        _sensor.SetLastSafePosition(ReferencePosition);
    }

    void Update()
    {
        if (_isDead || _motor.IsKnockedBack || _status.IsFrozen) return;
        CheckState();
        UpdateLastKnownPlayerPosition();
        _attack.TickAttack();
    }

    void FixedUpdate()
    {
        if (_isDead) return;

        if (_status.IsFrozen)
        {
            // Guaranteed stop: zero velocity every physics tick while frozen, regardless of
            // what else (knockback, magnet, attack lunge) might otherwise be pushing it.
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            return;
        }
        if (_motor.IsKnockedBack) return;

        _sensor.TrackLastSafePosition(ReferencePosition);

        if (_status.IsMagnetPulled)
        {
            _motor.ApplyMagnetMovement();
            return; // Normal hareketi override eder
        }

        Move();
    }

    protected override void Move() => _motor.MoveByState();

    /// <summary>
    /// Runs after Animator evaluation so it always wins, regardless of any color curve
    /// baked into an animation clip. Priority: frozen/on fire/poisoned (elemental arrow
    /// statuses) > hit flash > normal. Hit flash (red) only shows when the hit didn't also
    /// apply one of those statuses — e.g. a plain hammer/melee/unenchanted-arrow hit.
    /// </summary>
    void LateUpdate()
    {
        if (_bodySpriteRenderer == null) return;

        Color target;
        if (_status.IsFrozen)
            target = freezeColor;
        else if (_status.IsOnFire)
            target = onFireColor;
        else if (_status.IsPoisoned)
            target = poisonedColor;
        else if (Time.time < _hitFlashUntil)
            target = hitFlashColor;
        else
            target = _bodyOriginalColor;

        _bodySpriteRenderer.color = target;
    }

    private Vector2 GetValidGoldSpawnPosition(Vector2 desiredPosition)
    {
        return desiredPosition;
    }

    private float GetLineOfSightMaxDistance()
    {
        if (currentState == State.Patrol) return detectionRange;
        return expandedDetectionRange;
    }

    public bool HasLineOfSightToPlayer()
    {
        if (player == null) return false;
        return _sensor.HasLineOfSight(ReferencePosition, player.transform.position, GetLineOfSightMaxDistance());
    }

    private void CheckState()
    {
        if (player == null) return;

        if (_playerEntity != null && _playerEntity.IsDead)
        {
            if (currentState != State.Patrol)
            {
                currentState = State.Patrol;
                _hasLastKnownPlayerWorld = false;
                _attack.ResetAttackCooldown();
                _motor.ResetPatrolRoute();
            }
            return;
        }

        float dist = Vector2.Distance(ReferencePosition, player.transform.position);
        float attackDistance = _attack.AttackRange;

        if (currentState == State.Patrol)
        {
            if (dist <= detectionRange && HasLineOfSightToPlayer())
            {
                currentState = State.Chase;
                _attack.ResetAttackCooldown();
            }
        }
        else if (currentState == State.Chase)
        {
            if (dist > expandedDetectionRange)
            {
                currentState = State.Patrol;
                _hasLastKnownPlayerWorld = false;
                _attack.ResetAttackCooldown();
                _motor.ResetPatrolRoute();
            }
            else if (dist <= attackDistance)
            {
                currentState = State.Attack;
                _attack.ResetAttackCooldown();
            }
        }
        else if (currentState == State.Attack)
        {
            if (dist > expandedDetectionRange)
            {
                currentState = State.Patrol;
                _hasLastKnownPlayerWorld = false;
                _attack.ResetAttackCooldown();
                _motor.ResetPatrolRoute();
            }
            else if (dist > attackDistance)
            {
                currentState = State.Chase;
            }
        }
    }

    private void UpdateLastKnownPlayerPosition()
    {
        if (player == null) return;
        if (HasLineOfSightToPlayer())
        {
            _lastKnownPlayerWorld = player.transform.position;
            _hasLastKnownPlayerWorld = true;
        }
    }

    public override void TakeDamage(float amount, bool isHeavy)
    {
        if (IsDead) return;

        PlaySFX(hitSFX);

        // Resonance: frozen enemies are more vulnerable
        amount *= _status.DamageTakenMultiplier;

        _currentHealth -= amount;
        _hitFlashUntil = Time.time + hitFlashDuration;
        DamageNumberPooler.SpawnDamageNumber(ReferencePosition, amount, isHeavy);
        Damaged?.Invoke();
        Player playerComponent = player != null ? player.GetComponent<Player>() : null;
        float knockbackMult = playerComponent?.PlayerAugmentController?.KnockbackMultiplier ?? 1f;
        float force = (isHeavy ? heavyKnockbackForce : lightKnockbackForce) * knockbackMult;
        _motor.ApplyKnockback(force);
        if (_currentHealth <= 0 && !_isDead) PrepareToDie();
    }

    private void PrepareToDie()
    {
        _isDead = true;
        SetCollidersEnabled(false); // ceset artik vurulamaz/carpisamaz, animasyon beklenmez
        DropLoot(); // loot ölüm anında düşer, ceset süresini ve pool dönüşünü beklemez
        Died?.Invoke(this); // kill counter vb. olum anında tetiklenir, animasyon bitmesini beklemez

        // Sıra: knockback biter → Die animasyonu oynar (gerçek uzunluğu controller'dan okunur,
        // enemy başına hardcode yok) → ceset corpseLingerSeconds bekler → Die çalışır (pool'a dönüş).
        float dieAnimSeconds = _animatorBridge != null ? _animatorBridge.DieAnimationLength : 0f;
        Invoke("Die", knockbackDuration + 0.05f + dieAnimSeconds + corpseLingerSeconds);
    }

    private void SetCollidersEnabled(bool value)
    {
        if (_allColliders == null) return;
        for (int i = 0; i < _allColliders.Length; i++)
            if (_allColliders[i] != null) _allColliders[i].enabled = value;
    }

    private void DropLoot()
    {
        Vector2 deathPosition = ReferencePosition;

        Player playerComponent = player != null ? player.GetComponent<Player>() : null;
        float goldChanceMult = playerComponent?.PlayerAugmentController?.GoldDropChanceMultiplier ?? 1f;
        if (UnityEngine.Random.value <= goldDropChance * goldChanceMult)
        {
            Vector2 goldSpawnPosition = GetValidGoldSpawnPosition(deathPosition);
            if (GoldLootPooler.Instance != null)
                GoldLootPooler.Instance.GetGold(goldSpawnPosition, Quaternion.identity);
            else if (goldPrefab != null)
                Instantiate(goldPrefab, goldSpawnPosition, Quaternion.identity);
        }

        if (chestPrefab != null)
            Instantiate(chestPrefab, deathPosition, Quaternion.identity);
    }

    protected override void Die() {
        base.Die();
        if (EnemyObjectPooler.Instance != null)
        {
            EnemyObjectPooler.Instance.ReturnEnemy(gameObject);
            return;
        }
        gameObject.SetActive(false);
    }

    // Status efektleri EntityStatusEffects'te yaşar; buradaki metotlar dış çağıranlar
    // (Player.Hammer, PlayerArrow, PlayerBolt) için facade olarak korunur.
    public void Freeze(float duration, float vulnerabilityMultiplier = 1f)
        => _status.Freeze(duration, vulnerabilityMultiplier);

    public void SetMagnetPull(Vector2 targetPos, float speed, float expireDuration = 0.15f)
        => _status.SetMagnetPull(targetPos, speed, expireDuration);

    public void ApplyFireDoT(float duration, float dps)
        => _status.ApplyFireDoT(duration, dps);

    public void ApplyPoisonDoT(float duration, float dps)
        => _status.ApplyPoisonDoT(duration, dps);

    public void ApplyBleedStack(float damagePerStack, int maxStacks = 5, float expireSeconds = 5f)
        => _status.ApplyBleedStack(damagePerStack, maxStacks, expireSeconds);

    private void PlaySFX(AudioClip audioclip)
    {
        if (audioclip == null || _audioSource == null) return;
        _audioSource.PlayOneShot(audioclip);
    }

    private void OnDrawGizmos() {
        if (player == null || _sensor == null) return;
        Vector2 start = ReferencePosition;
        Vector2 toP = (Vector2)player.transform.position - start;
        float full = toP.magnitude;
        if (full < 0.0001f) return;
        Vector2 dir = toP / full;
        float maxRay = GetLineOfSightMaxDistance();
        Vector2 gizmoEnd = start + dir * Mathf.Min(full, maxRay);
        Gizmos.color = HasLineOfSightToPlayer() ? Color.green : Color.red;
        Gizmos.DrawLine(start, gizmoEnd);
    }

    private void OnEnable()
    {
        _isDead = false;
        // Status resetleri EntityStatusEffects'in kendi OnEnable'ında yapılır
        _hitFlashUntil = -1f;
        if (_bodySpriteRenderer != null) _bodySpriteRenderer.color = _bodyOriginalColor;
        SetCollidersEnabled(true);
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        if (stats != null) _currentHealth = stats.maxHealth;
        currentState = State.Patrol;
        _hasLastKnownPlayerWorld = false;
        _motor.ResetForSpawn();
        _attack.ResetForSpawn();
        _sensor.SetLastSafePosition(ReferencePosition);
    }
}
