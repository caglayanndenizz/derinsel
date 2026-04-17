using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;

public class Enemy : BaseEntity
{
    public enum State { Patrol, Chase, Attack, Dead }

    [Header("State Settings")]
    public State currentState = State.Patrol;
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

    [Header("Ranged Attack")]
    [Tooltip("Bu prefab üzerinde EnemyProjectile scripti olmalı.")]
    public GameObject projectilePrefab;
    [Tooltip("Attack state iken iki projectile arası süre (saniye).")]
    public float rangedFireInterval = 4f;
    public float projectileSpeed = 10f;
    [Tooltip("EntityStats.attackPower ile aynı anlamda kullanılır.")]
    public bool useAttackPowerForProjectile = true;
    public float projectileDamageOverride = 5f;
    public float projectileMaxLifetime = 12f;
    [Tooltip("Player bu kadar yakınsa (birim) Chase → Attack; daha uzaktaysa Attack → Chase.")]
    public float attackCloseMaxDistance = 5f;
    [Tooltip("Projectile bu child empty'den çıkar (boşsa child adı projectilePivot aranır).")]
    public Transform projectilePivot;
    public Transform projectileSpawnPoint;
    public Vector3 projectileSpawnOffset = Vector3.zero;
    public EnemyProjectilePooler projectilePooler;
    [Tooltip("Chase sırasında player'a yaklaşırken hız çarpanı.")]
    public float chaseApproachSpeedMultiplier = 1.6f;
    [Tooltip("Projectile atış hız çarpanı. 1.5 => %50 daha hızlı atış.")]
    public float projectileFireRateMultiplier = 1.5f;

    [Header("Loot Prefabs")]
    public GameObject goldPrefab;
    public GameObject experiencePrefab;

    [Header("Visual Effects")]
    public Color flashColor = Color.white;
    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;

    protected GameObject player;
    private bool _isDead = false;
    private Vector2 _lastKnownPlayerWorld;
    private bool _hasLastKnownPlayerWorld;
    private float _nextRangedFireTime;
    private float AdjustedRangedFireInterval => rangedFireInterval / Mathf.Max(0.01f, projectileFireRateMultiplier);

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player");
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _generator = Object.FindAnyObjectByType<DungeonGenerator>(); // Unity 6 için güncel arama
        
        _originalColor = _spriteRenderer.color;

        // Fizik Ayarları
        if (_rb != null)
        {
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
        }

        if (projectilePivot == null)
            projectilePivot = transform.Find("projectilePivot");

        _patrolAnchor = GetEnemyReferencePosition();
        _patrolLegIndex = 0;

        _nextRangedFireTime = Time.time + AdjustedRangedFireInterval;
    }

    void Update()
    {
        if (_isDead || _isKnockedBack) return;
        CheckState();
        UpdateLastKnownPlayerPosition();
        TryFireRangedProjectile();
    }

    void FixedUpdate()
    {
        if (_isDead || _isKnockedBack) return;
        Move();
    }

    private Vector2 GetEnemyReferencePosition()
    {
        return _rb != null ? _rb.position : (Vector2)transform.position;
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
        if (_isDead || _rb == null) return;

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
                transform.localScale = new Vector3(velocity.x >= 0f ? 4f : -4f, 4f, 1f);
        }
        else if (player != null)
            transform.localScale = new Vector3(player.transform.position.x > transform.position.x ? 4f : -4f, 4f, 1f);
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
        if (projectilePivot != null) return projectilePivot.position;
        if (projectileSpawnPoint != null) return projectileSpawnPoint.position;
        return transform.position + projectileSpawnOffset;
    }

    private void CheckState()
    {
        if (player == null) return;
        float dist = Vector2.Distance(GetEnemyReferencePosition(), player.transform.position);

        if (currentState == State.Patrol)
        {
            if (dist <= detectionRange && HasLineOfSight())
            {
                currentState = State.Chase;
                _nextRangedFireTime = Time.time + AdjustedRangedFireInterval;
            }
        }
        else if (currentState == State.Chase)
        {
            if (dist > expandedDetectionRange)
            {
                currentState = State.Patrol;
                _hasLastKnownPlayerWorld = false;
                _nextRangedFireTime = Time.time + AdjustedRangedFireInterval;
                ResetPatrolRoute();
            }
            else if (dist <= attackCloseMaxDistance)
            {
                currentState = State.Attack;
                _nextRangedFireTime = Time.time + AdjustedRangedFireInterval;
            }
        }
        else if (currentState == State.Attack)
        {
            if (dist > expandedDetectionRange)
            {
                currentState = State.Patrol;
                _hasLastKnownPlayerWorld = false;
                _nextRangedFireTime = Time.time + AdjustedRangedFireInterval;
                ResetPatrolRoute();
            }
            else if (dist > attackCloseMaxDistance)
            {
                currentState = State.Chase;
                _nextRangedFireTime = Time.time + AdjustedRangedFireInterval;
            }
        }
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
        if (currentState != State.Attack || player == null || projectilePrefab == null) return;
        if (!HasLineOfSight()) return;
        if (Time.time < _nextRangedFireTime) return;

        Vector2 aim = _hasLastKnownPlayerWorld ? _lastKnownPlayerWorld : (Vector2)player.transform.position;
        Vector3 spawnPos = GetProjectileSpawnPosition();

        if (projectilePooler == null)
            projectilePooler = EnemyProjectilePooler.Instance;

        GameObject proj = projectilePooler != null
            ? projectilePooler.GetProjectile(spawnPos, Quaternion.identity)
            : Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

        if (proj == null)
        {
            _nextRangedFireTime = Time.time + AdjustedRangedFireInterval;
            return;
        }

        var mover = proj.GetComponent<EnemyProjectile>();
        float dmg = projectileDamageOverride;
        if (useAttackPowerForProjectile && stats != null) dmg = stats.attackPower;
        if (mover != null)
            mover.Initialize(aim, projectileSpeed, dmg, projectileMaxLifetime);

        _nextRangedFireTime = Time.time + AdjustedRangedFireInterval;
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
        Instantiate(goldPrefab, transform.position, Quaternion.identity); 
        Instantiate(experiencePrefab, transform.position + new Vector3(0.3f, 0, 0), Quaternion.identity); 
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
        yield return new WaitForSeconds(knockbackDuration);
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
        _nextRangedFireTime = Time.time + AdjustedRangedFireInterval;
        currentState = State.Patrol;
        _hasLastKnownPlayerWorld = false;
        _patrolAnchor = GetEnemyReferencePosition();
        ResetPatrolRoute();
    }
}   