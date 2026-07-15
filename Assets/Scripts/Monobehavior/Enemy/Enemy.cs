using UnityEngine;

/// <summary>
/// Coordinator: holds serialized config, the Patrol/Chase/Attack state machine and death/loot.
/// Capabilities live in sibling components (EntityStatusEffects, EntityStatusVisuals,
/// EntitySensor, EnemyMotor, EnemyCombat); public status methods stay here as facades
/// so external callers (Player.Hammer, PlayerArrow, PlayerBolt) are unaffected.
/// </summary>
public class Enemy : BaseEntity
{
    public enum State { Patrol, Chase, Attack, Dead }
    public enum EnemyType { Mage, Tanky, Warrior }

    [Header("Data Reference")]
    [SerializeField] private EnemyEntityStats stats;

    public override float MaxHealth => stats != null ? stats.maxHealth : 0f;
    public EnemyEntityStats Stats => stats;

    [Header("State Settings")]
    [SerializeField] private State currentState = State.Patrol;
    public EnemyType enemyType = EnemyType.Mage;
    public float detectionRange = 10f;
    public float expandedDetectionRange = 22f;
    [SerializeField] private CircleCollider2D detectionCollider;

    public State CurrentState => currentState;

    [Header("Knockback Settings")]
    public float lightKnockbackForce = 5f;
    public float heavyKnockbackForce = 12f;
    public float knockbackDuration = 0.2f;
    private Rigidbody2D _rb;

    [Header("Navigation (Tilemap + Colliders)")]
    public float sensorLength = 1.5f;
    [Tooltip("Layers with Collider2D that block LOS. Exclude Enemy; Player is ignored in code.")]
    public LayerMask blockingEnvironmentMask = Physics2D.DefaultRaycastLayers;

    [Header("Patrol Route (spawn-based)")]
    [Tooltip("Left leg from spawn point (world -X).")]
    public float patrolLegLeft = 2f;
    [Tooltip("Second leg: forward direction (default world +Y).")]
    public float patrolLegForward = 2f;
    [Tooltip("Third leg: right (world +X).")]
    public float patrolLegRight = 4f;
    public Vector2 patrolForwardWorld = Vector2.up;
    public float patrolWaypointReachDistance = 0.22f;

    [Header("Mage Ranged Attack")]
    [Tooltip("Prefab must have an EnemyProjectile script.")]
    public GameObject mageProjectilePrefab;
    [Tooltip("Time between projectiles while in Attack state (seconds).")]
    public float mageRangedFireInterval = 4f;
    public float mageProjectileSpeed = 10f;
    [Tooltip("Uses EnemyEntityStats.enemyAP as damage when enabled.")]
    public bool mageUseAttackPowerForProjectile = true;
    public float mageProjectileDamageOverride = 5f;
    public float mageProjectileMaxLifetime = 12f;
    [Tooltip("Chase → Attack when player is within this distance (units); Attack → Chase when farther.")]
    public float attackCloseMaxDistance = 5f;
    [Tooltip("Projectile spawns from this child transform (searches for 'projectilePivot' child if empty).")]
    public Transform mageProjectilePivot;
    public Transform mageProjectileSpawnPoint;
    public Vector3 mageProjectileSpawnOffset = Vector3.zero;
    public EnemyProjectilePooler mageProjectilePooler;
    [Tooltip("Speed multiplier when chasing the player.")]
    public float chaseApproachSpeedMultiplier = 1.6f;
    [Tooltip("Fire rate multiplier for projectiles. 1.5 => 50% faster fire rate.")]
    public float mageProjectileFireRateMultiplier = 1.5f;

    [Header("Melee Attack")]
    public float meleeAttackInterval = 2f;
    [Tooltip("Actual contact/proximity range for melee damage to be applied.")]
    public float meleeHitRange = 1.6f;

    [Header("Loot Prefabs")]
    public GameObject goldPrefab;
    public GameObject experiencePrefab;
    [Range(0f, 1f)] public float goldDropChance = 0.15f;

    public event System.Action<Enemy> Died;

    private EntityStatusEffects _status;
    private EntityStatusVisuals _visuals;
    private EntitySensor _sensor;
    private EnemyMotor _motor;
    private EnemyCombat _combat;

    public override bool IsDead => _isDead || _currentHealth <= 0f;
    public bool IsFrozen => _status.IsFrozen;

    protected GameObject player;
    private bool _isDead = false;
    private Vector2 _lastKnownPlayerWorld;
    private bool _hasLastKnownPlayerWorld;

    public GameObject PlayerObject => player;
    public Transform PlayerTransform => player != null ? player.transform : null;
    public bool HasLastKnownPlayerPosition => _hasLastKnownPlayerWorld;
    public Vector2 LastKnownPlayerPosition => _lastKnownPlayerWorld;

    /// <summary>Mage uses the rigidbody position, melee types the transform position.</summary>
    public Vector2 ReferencePosition
    {
        get
        {
            if (enemyType != EnemyType.Mage)
                return transform.position;
            return _rb != null ? _rb.position : (Vector2)transform.position;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player");

        // Fallback: prefab'da yoksa runtime'da ekle (status önce — visuals ona abone oluyor)
        _status = GetComponent<EntityStatusEffects>();
        if (_status == null) _status = gameObject.AddComponent<EntityStatusEffects>();
        _visuals = GetComponent<EntityStatusVisuals>();
        if (_visuals == null) _visuals = gameObject.AddComponent<EntityStatusVisuals>();
        _sensor = GetComponent<EntitySensor>();
        if (_sensor == null) _sensor = gameObject.AddComponent<EntitySensor>();
        _motor = GetComponent<EnemyMotor>();
        if (_motor == null) _motor = gameObject.AddComponent<EnemyMotor>();
        _combat = GetComponent<EnemyCombat>();
        if (_combat == null) _combat = gameObject.AddComponent<EnemyCombat>();

        if (_rb != null)
        {
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
        }

        if (enemyType == EnemyType.Mage && mageProjectilePivot == null)
            mageProjectilePivot = transform.Find("projectilePivot");

        // Serialize edilen config Enemy'de kalır; component'lere buradan bağlanır
        _sensor.Configure(blockingEnvironmentMask, sensorLength);
        _motor.Configure(this, _sensor, _status);
        _combat.Configure(this);

        _sensor.SetLastSafePosition(ReferencePosition);
    }

    void Update()
    {
        if (_isDead || _motor.IsKnockedBack || _status.IsFrozen) return;
        CheckState();
        UpdateLastKnownPlayerPosition();
        _combat.TickAttack();
    }

    void FixedUpdate()
    {
        if (_isDead || _motor.IsKnockedBack || _status.IsFrozen) return;

        _sensor.TrackLastSafePosition(ReferencePosition);

        if (_status.IsMagnetPulled)
        {
            _motor.ApplyMagnetMovement();
            return; // Normal hareketi override eder
        }

        Move();
    }

    protected override void Move() => _motor.MoveByState();

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
        float dist = Vector2.Distance(ReferencePosition, player.transform.position);
        float attackDistance = attackCloseMaxDistance;

        if (currentState == State.Patrol)
        {
            if (dist <= detectionRange && HasLineOfSightToPlayer())
            {
                currentState = State.Chase;
                _combat.ResetRangedFireCooldown();
            }
        }
        else if (currentState == State.Chase)
        {
            if (dist > expandedDetectionRange)
            {
                currentState = State.Patrol;
                _hasLastKnownPlayerWorld = false;
                _combat.ResetRangedFireCooldown();
                _motor.ResetPatrolRoute();
            }
            else if (dist <= attackDistance)
            {
                currentState = State.Attack;
            }
        }
        else if (currentState == State.Attack)
        {
            if (dist > expandedDetectionRange)
            {
                currentState = State.Patrol;
                _hasLastKnownPlayerWorld = false;
                _combat.ResetRangedFireCooldown();
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

        // Resonance: frozen enemies are more vulnerable
        amount *= _status.DamageTakenMultiplier;

        _currentHealth -= amount;
        _visuals.PlayHitFlash();
        float force = (isHeavy ? heavyKnockbackForce : lightKnockbackForce) * 0.5f;
        _motor.ApplyKnockback(force);
        if (_currentHealth <= 0 && !_isDead) PrepareToDie();
    }

    private void PrepareToDie() { _isDead = true; if (detectionCollider != null) detectionCollider.enabled = false; Invoke("Die", knockbackDuration + 0.05f); }

    protected override void Die() {
        Vector2 deathPosition = ReferencePosition;

        Player playerComponent = player != null ? player.GetComponent<Player>() : null;
        float luckMult = playerComponent?.PlayerAugmentController?.LuckMultiplier ?? 1f;
        if (UnityEngine.Random.value <= goldDropChance * luckMult)
        {
            Vector2 goldSpawnPosition = GetValidGoldSpawnPosition(deathPosition);
            if (GoldLootPooler.Instance != null)
                GoldLootPooler.Instance.GetGold(goldSpawnPosition, Quaternion.identity);
            else if (goldPrefab != null)
                Instantiate(goldPrefab, goldSpawnPosition, Quaternion.identity);
        }

        if (ExperienceLootPooler.Instance != null)
            ExperienceLootPooler.Instance.GetExperience((Vector3)deathPosition + new Vector3(0.3f, 0f, 0f), Quaternion.identity);
        else if (experiencePrefab != null)
            Instantiate(experiencePrefab, (Vector3)deathPosition + new Vector3(0.3f, 0f, 0f), Quaternion.identity);

        Died?.Invoke(this);
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
        // Status/renk resetleri EntityStatusEffects ve EntityStatusVisuals OnEnable'larında yapılır
        if (detectionCollider != null) detectionCollider.enabled = true;
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        if (stats != null) _currentHealth = stats.maxHealth;
        currentState = State.Patrol;
        _hasLastKnownPlayerWorld = false;
        _motor.ResetForSpawn();
        _combat.ResetForSpawn();
        _sensor.SetLastSafePosition(ReferencePosition);
    }
}
