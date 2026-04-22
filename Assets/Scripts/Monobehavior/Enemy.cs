using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;

public class Enemy : BaseEntity
{
    public enum State { Patrol, Chase, Attack, Dead }
    public enum EnemyType { Mage, Tanky, Warrior }

    [Header("State Settings")]
    public State currentState = State.Patrol;
    public EnemyType enemyType = EnemyType.Mage;
    public float detectionRange = 10f; 
    public float expandedDetectionRange = 22f; 
    public CircleCollider2D cd;

    [Header("Knockback Settings")]
    public float lightKnockbackForce = 5f;  
    public float heavyKnockbackForce = 12f; 
    public float knockbackDuration = 0.2f;
    private bool _isKnockedBack = false;
    private Rigidbody2D _rb;

    [Header("Navigation (Tilemap + Colliders)")]
    public float sensorLength = 1.5f;
    [Tooltip("Layers with Collider2D that block LOS. Exclude Enemy; Player is ignored in code.")]
    public LayerMask blockingEnvironmentMask = Physics2D.DefaultRaycastLayers;

    [Header("Patrol Route (spawn tabanlı)")]
    [Tooltip("Spawn noktasından sola (world -X).")]
    public float patrolLegLeft = 2f;
    [Tooltip("İkinci bacak: ileri yön (varsayılan world +Y).")]
    public float patrolLegForward = 2f;
    [Tooltip("Üçüncü bacak: sağa (world +X).")]
    public float patrolLegRight = 4f;
    public Vector2 patrolForwardWorld = Vector2.up;
    public float patrolWaypointReachDistance = 0.22f;

    private DungeonGenerator _generator;
    private Vector2 _patrolAnchor;
    private int _patrolLegIndex;

    [Header("Mage Ranged Attack")]
    [Tooltip("Bu prefab üzerinde EnemyProjectile scripti olmalı.")]
    public GameObject mageProjectilePrefab;
    [Tooltip("Attack state iken iki projectile arası süre (saniye).")]
    public float mageRangedFireInterval = 4f;
    public float mageProjectileSpeed = 10f;
    [Tooltip("EntityStats.attackPower ile aynı anlamda kullanılır.")]
    public bool mageUseAttackPowerForProjectile = true;
    public float mageProjectileDamageOverride = 5f;
    public float mageProjectileMaxLifetime = 12f;
    [Tooltip("Player bu kadar yakınsa (birim) Chase → Attack; daha uzaktaysa Attack → Chase.")]
    public float attackCloseMaxDistance = 5f;
    [Tooltip("Projectile bu child empty'den çıkar (boşsa child adı projectilePivot aranır).")]
    public Transform mageProjectilePivot;
    public Transform mageProjectileSpawnPoint;
    public Vector3 mageProjectileSpawnOffset = Vector3.zero;
    public EnemyProjectilePooler mageProjectilePooler;
    public GoldLootPooler goldPooler;
    public ExperienceLootPooler experiencePooler;
    [Tooltip("Chase sırasında player'a yaklaşırken hız çarpanı.")]
    public float chaseApproachSpeedMultiplier = 1.6f;
    [Tooltip("Projectile atış hız çarpanı. 1.5 => %50 daha hızlı atış.")]
    public float mageProjectileFireRateMultiplier = 1.5f;
    
    [Header("Melee Attack")]
    public float meleeAttackInterval = 2f;

    [Header("Loot Prefabs")]
    public GameObject goldPrefab;
    public GameObject experiencePrefab;
    [Range(0f, 1f)] public float goldDropChance = 0.15f;

    [Header("Visual Effects")]
    public Color flashColor = Color.white;
    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;

    protected GameObject player;
    private bool _isDead = false;
    private Vector2 _lastKnownPlayerWorld;
    private bool _hasLastKnownPlayerWorld;
    private float _nextRangedFireTime;
    private float _nextTypeAttackTime;
    private Vector2 _lastSafeWorldPosition;
    private float AdjustedMageRangedFireInterval => mageRangedFireInterval / Mathf.Max(0.01f, mageProjectileFireRateMultiplier);

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player");
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _generator = Object.FindAnyObjectByType<DungeonGenerator>(); // Unity 6 için güncel arama
        if (goldPooler == null) goldPooler = GoldLootPooler.Instance;
        if (experiencePooler == null) experiencePooler = ExperienceLootPooler.Instance;
        
        _originalColor = _spriteRenderer.color;

        // Fizik Ayarları
        if (_rb != null)
        {
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
        }

        if (enemyType == EnemyType.Mage && mageProjectilePivot == null)
            mageProjectilePivot = transform.Find("projectilePivot");

        _patrolAnchor = GetEnemyReferencePosition();
        _patrolLegIndex = 0;

        _nextRangedFireTime = enemyType == EnemyType.Mage
            ? Time.time + AdjustedMageRangedFireInterval
            : 0f;
        _nextTypeAttackTime = Time.time;
        _lastSafeWorldPosition = GetEnemyReferencePosition();
    }

    void Update()
    {
        if (_isDead || _isKnockedBack) return;
        CheckState();
        UpdateLastKnownPlayerPosition();
        ExecuteTypeAttack();
    }

    void FixedUpdate()
    {
        if (_isDead || _isKnockedBack) return;
        TrackLastSafePosition();
        Move();
    }

    private Vector2 GetEnemyReferencePosition()
    {
        if (enemyType != EnemyType.Mage)
            return transform.position;
        return _rb != null ? _rb.position : (Vector2)transform.position;
    }

    private void TrackLastSafePosition()
    {
        Vector2 currentPos = GetEnemyReferencePosition();
        if (!IsNavigationBlockedAt(currentPos))
            _lastSafeWorldPosition = currentPos;
    }

    private Vector2 GetPatrolWaypointWorld(int leg)
    {
        Vector2 left = Vector2.left * patrolLegLeft;
        Vector2 fwd = patrolForwardWorld.sqrMagnitude > 0.0001f
            ? patrolForwardWorld.normalized * patrolLegForward
            : Vector2.up * patrolLegForward;
        Vector2 right = Vector2.right * patrolLegRight;
        switch (leg)
        {
            case 0: return _patrolAnchor + left;
            case 1: return _patrolAnchor + left + fwd;
            case 2: return _patrolAnchor + left + fwd + right;
            case 3: return _patrolAnchor;
            default: return _patrolAnchor;
        }
    }

    private void AdvancePatrolLegIfReached(Vector2 pos)
    {
        Vector2 wp = GetPatrolWaypointWorld(_patrolLegIndex);
        if (Vector2.Distance(pos, wp) < patrolWaypointReachDistance)
            _patrolLegIndex = (_patrolLegIndex + 1) % 4;
    }

    private void ResetPatrolRoute()
    {
        _patrolLegIndex = 0;
    }

    protected override void Move() 
    {
        if (_isDead) return;

        if (enemyType != EnemyType.Mage)
        {
            MoveMeleeTypeWithTranslate();
            return;
        }

        if (_rb == null) return;

        Vector2 velocity = Vector2.zero;
        float baseSpeed = stats != null ? stats.moveSpeed : 4f;
        Vector2 origin = GetEnemyReferencePosition();

        if (currentState == State.Patrol)
        {
            AdvancePatrolLegIfReached(origin);
            Vector2 targetWp = GetPatrolWaypointWorld(_patrolLegIndex);
            Vector2 toWp = targetWp - origin;
            if (toWp.sqrMagnitude > 0.0001f)
            {
                Vector2 dir = toWp.normalized;
                velocity = GetAvoidanceDirection(dir) * baseSpeed;
            }
        }
        else if (player != null && currentState == State.Chase)
        {
            float dist = Vector2.Distance(origin, player.transform.position);
            if (dist > attackCloseMaxDistance)
            {
                Vector2 targetDir = ((Vector2)player.transform.position - origin).normalized;
                velocity = GetAvoidanceDirection(targetDir) * baseSpeed * chaseApproachSpeedMultiplier;
            }
        }

        _rb.linearVelocity = velocity;

        if (currentState == State.Patrol)
        {
            if (velocity.sqrMagnitude > 0.0001f)
                FaceByHorizontal(velocity.x);
        }
        else if (player != null)
            FaceByHorizontal(player.transform.position.x - transform.position.x);
    }

    private void MoveMeleeTypeWithTranslate()
    {
        if (player == null) return;
        if (currentState == State.Patrol) return;

        if (_rb != null)
            _rb.linearVelocity = Vector2.zero;

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.z = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return;

        float speed = stats != null ? stats.moveSpeed : 4f;
        Vector3 dir = toPlayer.normalized;
        transform.Translate(dir * speed * Time.fixedDeltaTime, Space.World);
        FaceByHorizontal(dir.x);
    }

    private void FaceByHorizontal(float horizontal)
    {
        if (Mathf.Abs(horizontal) < 0.0001f) return;

        Vector3 currentScale = transform.localScale;
        float xMagnitude = Mathf.Abs(currentScale.x);
        if (xMagnitude < 0.0001f) xMagnitude = 1f;

        currentScale.x = horizontal >= 0f ? xMagnitude : -xMagnitude;
        transform.localScale = currentScale;
    }

    private bool IsNavigationBlockedAt(Vector2 worldPos)
    {
        if (_generator != null && _generator.wallTilemap != null)
        {
            Vector3Int cellPos = _generator.wallTilemap.WorldToCell(worldPos);
            if (_generator.wallTilemap.HasTile(cellPos)) return true;
        }

        const float probeRadius = 0.12f;
        Collider2D hit = Physics2D.OverlapCircle(worldPos, probeRadius, blockingEnvironmentMask);
        if (hit == null) return false;
        if (hit.isTrigger) return false;
        if (_rb != null && hit.attachedRigidbody == _rb) return false;
        if (hit.gameObject == gameObject || hit.transform.IsChildOf(transform)) return false;
        if (hit.CompareTag("Player")) return false;
        return true;
    }

    private Vector2 GetValidGoldSpawnPosition(Vector2 desiredPosition)
    {
        if (_generator == null || _generator.floorTilemap == null) return desiredPosition;

        Tilemap floorTilemap = _generator.floorTilemap;
        Vector3Int centerCell = floorTilemap.WorldToCell(desiredPosition);

        if (floorTilemap.HasTile(centerCell))
            return floorTilemap.GetCellCenterWorld(centerCell);

        const int maxRadius = 2;
        for (int radius = 1; radius <= maxRadius; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;
                    Vector3Int candidateCell = centerCell + new Vector3Int(x, y, 0);
                    if (floorTilemap.HasTile(candidateCell))
                        return floorTilemap.GetCellCenterWorld(candidateCell);
                }
            }
        }

        return _lastSafeWorldPosition;
    }

    private Vector2 GetAvoidanceDirection(Vector2 currentDir)
    {
        Vector2 origin = _rb != null ? _rb.position : (Vector2)transform.position;
        if (IsNavigationBlockedAt(origin + currentDir * sensorLength))
        {
            Vector2 leftDir = RotateVector(currentDir, 45f);
            Vector2 rightDir = RotateVector(currentDir, -45f);

            if (!IsNavigationBlockedAt(origin + leftDir * sensorLength)) return leftDir;
            if (!IsNavigationBlockedAt(origin + rightDir * sensorLength)) return rightDir;
            return RotateVector(currentDir, 90f);
        }
        return currentDir;
    }

    private static Vector2 RotateVector(Vector2 v, float deg)
    {
        float s = Mathf.Sin(deg * Mathf.Deg2Rad);
        float c = Mathf.Cos(deg * Mathf.Deg2Rad);
        return new Vector2((c * v.x) - (s * v.y), (s * v.x) + (c * v.y));
    }

    private Vector3 GetProjectileSpawnPosition()
    {
        if (enemyType != EnemyType.Mage) return transform.position;
        if (mageProjectilePivot != null) return mageProjectilePivot.position;
        if (mageProjectileSpawnPoint != null) return mageProjectileSpawnPoint.position;
        return transform.position + mageProjectileSpawnOffset;
    }

    private void CheckState()
    {
        if (player == null) return;
        float dist = Vector2.Distance(GetEnemyReferencePosition(), player.transform.position);
        float attackDistance = attackCloseMaxDistance;

        if (currentState == State.Patrol)
        {
            if (dist <= detectionRange && HasLineOfSight())
            {
                currentState = State.Chase;
                if (enemyType == EnemyType.Mage)
                    _nextRangedFireTime = Time.time + AdjustedMageRangedFireInterval;
            }
        }
        else if (currentState == State.Chase)
        {
            if (dist > expandedDetectionRange)
            {
                currentState = State.Patrol;
                _hasLastKnownPlayerWorld = false;
                if (enemyType == EnemyType.Mage)
                    _nextRangedFireTime = Time.time + AdjustedMageRangedFireInterval;
                ResetPatrolRoute();
            }
            else if (dist <= attackDistance)
            {
                currentState = State.Attack;
                if (enemyType == EnemyType.Mage)
                    _nextRangedFireTime = Time.time + AdjustedMageRangedFireInterval;
            }
        }
        else if (currentState == State.Attack)
        {
            if (dist > expandedDetectionRange)
            {
                currentState = State.Patrol;
                _hasLastKnownPlayerWorld = false;
                if (enemyType == EnemyType.Mage)
                    _nextRangedFireTime = Time.time + AdjustedMageRangedFireInterval;
                ResetPatrolRoute();
            }
            else if (dist > attackDistance)
            {
                currentState = State.Chase;
                if (enemyType == EnemyType.Mage)
                    _nextRangedFireTime = Time.time + AdjustedMageRangedFireInterval;
            }
        }
    }

    private void ExecuteTypeAttack()
    {
        switch (enemyType)
        {
            case EnemyType.Mage:
                TryMageAttack();
                break;
            case EnemyType.Tanky:
                TryTankyAttack();
                break;
            case EnemyType.Warrior:
                TryWarriorAttack();
                break;
        }
    }

    private void TryMageAttack()
    {
        TryFireRangedProjectile();
    }

    private void TryTankyAttack()
    {
        if (currentState != State.Attack || player == null) return;
        if (Time.time < _nextTypeAttackTime) return;

        float dist = Vector2.Distance(GetEnemyReferencePosition(), player.transform.position);
        if (dist > attackCloseMaxDistance) return;

        IDamageable target = player.GetComponent<IDamageable>();
        if (target != null)
            target.TakeDamage(stats != null ? stats.attackPower : 0f, false);

        _nextTypeAttackTime = Time.time + meleeAttackInterval;
    }

    private void TryWarriorAttack()
    {
        if (currentState != State.Attack || player == null) return;
        if (Time.time < _nextTypeAttackTime) return;

        float dist = Vector2.Distance(GetEnemyReferencePosition(), player.transform.position);
        if (dist > attackCloseMaxDistance) return;

        IDamageable target = player.GetComponent<IDamageable>();
        if (target != null)
            target.TakeDamage(stats != null ? stats.attackPower : 0f, false);

        _nextTypeAttackTime = Time.time + meleeAttackInterval;
    }

    private float GetLineOfSightMaxDistance()
    {
        if (currentState == State.Patrol) return detectionRange;
        return expandedDetectionRange;
    }

    private bool HasLineOfSight()
    {
        Vector2 start = GetEnemyReferencePosition();
        Vector2 toPlayer = (Vector2)player.transform.position - start;
        float fullDist = toPlayer.magnitude;
        if (fullDist < 0.0001f) return true;

        Vector2 dir = toPlayer / fullDist;
        float maxRay = GetLineOfSightMaxDistance();
        if (fullDist > maxRay + 0.001f) return false;

        float checkDist = Mathf.Min(fullDist, maxRay);
        Vector2 end = start + dir * checkDist;

        for (float i = 0.25f; i < checkDist; i += 0.5f)
        {
            if (IsNavigationBlockedAt(start + dir * i)) return false;
        }

        RaycastHit2D[] hits = Physics2D.LinecastAll(start, end, blockingEnvironmentMask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.isTrigger) continue;
            if (_rb != null && hit.collider.attachedRigidbody == _rb) continue;
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.CompareTag("Player")) return true;
            return false;
        }

        return true;
    }

    private void UpdateLastKnownPlayerPosition()
    {
        if (player == null) return;
        if (HasLineOfSight())
        {
            _lastKnownPlayerWorld = player.transform.position;
            _hasLastKnownPlayerWorld = true;
        }
    }

    private void TryFireRangedProjectile()
    {
        if (enemyType != EnemyType.Mage) return;
        if (currentState != State.Attack || player == null || mageProjectilePrefab == null) return;
        if (!HasLineOfSight()) return;
        if (Time.time < _nextRangedFireTime) return;

        Vector2 aim = _hasLastKnownPlayerWorld ? _lastKnownPlayerWorld : (Vector2)player.transform.position;
        Vector3 spawnPos = GetProjectileSpawnPosition();

        if (mageProjectilePooler == null)
            mageProjectilePooler = EnemyProjectilePooler.Instance;

        GameObject proj = mageProjectilePooler != null
            ? mageProjectilePooler.GetProjectile(spawnPos, Quaternion.identity)
            : Instantiate(mageProjectilePrefab, spawnPos, Quaternion.identity);

        if (proj == null)
        {
            _nextRangedFireTime = Time.time + AdjustedMageRangedFireInterval;
            return;
        }

        var mover = proj.GetComponent<EnemyProjectile>();
        float dmg = mageProjectileDamageOverride;
        if (mageUseAttackPowerForProjectile && stats != null) dmg = stats.attackPower;
        if (mover != null)
            mover.Initialize(aim, mageProjectileSpeed, dmg, mageProjectileMaxLifetime);

        _nextRangedFireTime = Time.time + AdjustedMageRangedFireInterval;
    }

    public override void TakeDamage(float amount, bool isHeavy)
    {
        _currentHealth -= amount;
        StartCoroutine(HitFlashRoutine());
        float force = isHeavy ? heavyKnockbackForce : lightKnockbackForce;
        StartCoroutine(KnockbackRoutine(force));
        if (_currentHealth <= 0 && !_isDead) PrepareToDie();
    }

    private void PrepareToDie() { _isDead = true; if (cd != null) cd.enabled = false; Invoke("Die", knockbackDuration + 0.05f); }
    
    protected override void Die() { 
        if (goldPooler == null) goldPooler = GoldLootPooler.Instance;
        if (experiencePooler == null) experiencePooler = ExperienceLootPooler.Instance;
        Vector2 deathPosition = GetEnemyReferencePosition();

        if (Random.value <= goldDropChance)
        {
            Vector2 goldSpawnPosition = GetValidGoldSpawnPosition(deathPosition);
            if (goldPooler != null)
                goldPooler.GetGold(goldSpawnPosition, Quaternion.identity);
            else if (goldPrefab != null)
                Instantiate(goldPrefab, goldSpawnPosition, Quaternion.identity);
        }

        if (experiencePooler != null)
            experiencePooler.GetExperience((Vector3)deathPosition + new Vector3(0.3f, 0f, 0f), Quaternion.identity);
        else if (experiencePrefab != null)
            Instantiate(experiencePrefab, (Vector3)deathPosition + new Vector3(0.3f, 0f, 0f), Quaternion.identity);

        base.Die(); 
        if (EnemyObjectPooler.Instance != null)
        {
            EnemyObjectPooler.Instance.ReturnEnemy(gameObject);
            return;
        }
        gameObject.SetActive(false);
    }

    private IEnumerator HitFlashRoutine() { 
        _spriteRenderer.color = flashColor; 
        yield return new WaitForSeconds(0.1f); 
        _spriteRenderer.color = _originalColor; 
    }

    private IEnumerator KnockbackRoutine(float force) {
        _isKnockedBack = true;
        if (player != null) {
            Vector2 dir = (transform.position - player.transform.position).normalized;
            _rb.linearVelocity = Vector2.zero; 
            _rb.AddForce(dir * force, ForceMode2D.Impulse);
        }

        float elapsed = 0f;
        while (elapsed < knockbackDuration)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;

            Vector2 currentPos = GetEnemyReferencePosition();
            if (IsNavigationBlockedAt(currentPos))
            {
                if (_rb != null)
                {
                    _rb.position = _lastSafeWorldPosition;
                    _rb.linearVelocity = Vector2.zero;
                }
                else
                {
                    transform.position = _lastSafeWorldPosition;
                }
            }
            else
            {
                _lastSafeWorldPosition = currentPos;
            }
        }

        if (!_isDead) { _rb.linearVelocity = Vector2.zero; _isKnockedBack = false; }
    }

    private void OnDrawGizmos() {
        if (player == null) return;
        Vector2 start = GetEnemyReferencePosition();
        Vector2 toP = (Vector2)player.transform.position - start;
        float full = toP.magnitude;
        if (full < 0.0001f) return;
        Vector2 dir = toP / full;
        float maxRay = GetLineOfSightMaxDistance();
        Vector2 gizmoEnd = start + dir * Mathf.Min(full, maxRay);
        Gizmos.color = HasLineOfSight() ? Color.green : Color.red;
        Gizmos.DrawLine(start, gizmoEnd);
    }

    private void OnEnable()
    {
        _isDead = false;
        _isKnockedBack = false;
        if (cd != null) cd.enabled = true;
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        if (stats != null) _currentHealth = stats.maxHealth;
        _nextRangedFireTime = enemyType == EnemyType.Mage
            ? Time.time + AdjustedMageRangedFireInterval
            : 0f;
        _nextTypeAttackTime = Time.time;
        currentState = State.Patrol;
        _hasLastKnownPlayerWorld = false;
        _patrolAnchor = GetEnemyReferencePosition();
        _lastSafeWorldPosition = _patrolAnchor;
        ResetPatrolRoute();
    }
}   