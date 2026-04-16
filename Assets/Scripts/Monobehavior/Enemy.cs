using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;

public class Enemy : BaseEntity
{
    public enum State { Patrol, Chase, Dead }

    [Header("State Settings")]
    public State currentState = State.Patrol;
    public float detectionRange = 10f; 
    public float expandedDetectionRange = 22f; 
    public float attackRange;
    public CircleCollider2D cd;

    [Header("Knockback Settings")]
    public float lightKnockbackForce = 5f;  
    public float heavyKnockbackForce = 12f; 
    public float knockbackDuration = 0.2f;
    private bool _isKnockedBack = false;
    private Rigidbody2D _rb;

    [Header("Patrol Settings")]
    public float patrolDistance = 5f;
    private Vector3 _startPosition;
    private int _patrolDirection = 1;

    [Header("Navigation (Tilemap + Colliders)")]
    public float sensorLength = 1.5f;
    [Tooltip("Layers with Collider2D that block movement and LOS. Exclude Enemy; Player is ignored in code.")]
    public LayerMask blockingEnvironmentMask = Physics2D.DefaultRaycastLayers;

    private DungeonGenerator _generator;

    [Header("Loot Prefabs")]
    public GameObject goldPrefab;
    public GameObject experiencePrefab;

    [Header("Visual Effects")]
    public Color flashColor = Color.white;
    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;

    protected GameObject player;
    private bool _isDead = false;

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player");
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _generator = Object.FindAnyObjectByType<DungeonGenerator>(); // Unity 6 için güncel arama
        
        _startPosition = transform.position;
        attackRange = (cd != null) ? cd.radius : 1f;
        _originalColor = _spriteRenderer.color;

        // Fizik Ayarları
        if (_rb != null)
        {
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
        }
    }

    void Update()
    {
        if (_isDead || _isKnockedBack || _generator == null) return;
        CheckState();
    }

    void FixedUpdate()
    {
        if (_isDead || _isKnockedBack || _generator == null) return;
        Move();
    }

    protected override void Move() 
    {
        if (player == null || _isDead) return;

        float currentSpeed = (currentState == State.Patrol) ? stats.moveSpeed : stats.moveSpeed * 1.6f;
        Vector2 velocity;

        if (currentState == State.Patrol)
        {
            velocity = new Vector2(_patrolDirection * currentSpeed, 0);
            
            // Önünde duvar varsa geri dön
            Vector2 patrolOrigin = _rb != null ? _rb.position : (Vector2)transform.position;
            if (IsNavigationBlockedAt(patrolOrigin + velocity.normalized * 1f))
            {
                _patrolDirection *= -1;
            }
            
            if (Vector2.Distance(_startPosition, transform.position) >= patrolDistance)
            {
                _patrolDirection *= -1;
                _startPosition = transform.position; 
            }
        }
        else // CHASE STATE
        {
            Vector2 targetDir = (player.transform.position - transform.position).normalized;
            float dist = Vector2.Distance(transform.position, player.transform.position);

            if (dist <= attackRange && HasLineOfSight())
                velocity = Vector2.zero;
            else
                velocity = GetAvoidanceDirection(targetDir) * currentSpeed;
        }

        // UNITY 6: linearVelocity kullanıyoruz
        _rb.linearVelocity = velocity;

        // Görsel Yön
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

    private void CheckState()
    {
        if (player == null) return;
        float dist = Vector2.Distance(transform.position, player.transform.position);

        if (currentState == State.Patrol)
        {
            if (dist <= detectionRange && HasLineOfSight())
            {
                currentState = State.Chase;
            }
        }
        else if (currentState == State.Chase)
        {
            if (dist > expandedDetectionRange)
            {   
                currentState = State.Patrol;
                _startPosition = transform.position;
            }
        }
    }

    private bool HasLineOfSight()
    {
        Vector2 start = _rb != null ? _rb.position : (Vector2)transform.position;
        Vector2 end = player.transform.position;
        float dist = Vector2.Distance(start, end);
        Vector2 dir = (end - start).normalized;

        for (float i = 0.25f; i < dist; i += 0.5f)
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
        Destroy(gameObject); 
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

    private Vector2 RotateVector(Vector2 v, float deg) {
        float s = Mathf.Sin(deg * Mathf.Deg2Rad); float c = Mathf.Cos(deg * Mathf.Deg2Rad);
        return new Vector2((c * v.x) - (s * v.y), (s * v.x) + (c * v.y));
    }

    private void OnDrawGizmos() {
        if (player == null) return;
        Gizmos.color = HasLineOfSight() ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position, player.transform.position);
    }
}