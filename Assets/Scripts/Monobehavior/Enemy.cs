using UnityEngine;
using System.Collections;

public class Enemy : BaseEntity
{
    public enum State { Patrol, Chase }

    [Header("State Settings")]
    public State currentState = State.Patrol;
    public float detectionRange = 3f; 
    public float expandedDetectionRange = 12f; 
    public float attackRange;
    public CircleCollider2D cd;

    [Header("Knockback Settings")]
    public float knockbackDuration = 0.2f;
    private bool _isKnockedBack = false;
    private Rigidbody2D _rb;

    [Header("Patrol Settings")]
    public float patrolDistance = 2f;
    private Vector3 startPosition;
    private int patrolDirection = 1;

    [Header("Movement Speed")]
    private float patrolSpeed;
    private float originalDetectionRange;

    [Header("Loot")]
    public GameObject goldPrefab;
    public GameObject experiencePrefab;

    [Header("Visual Effects")]
    public Color flashColor = Color.white;
    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;

    protected GameObject player;

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player");
        _spriteRenderer = GetComponent<SpriteRenderer>();
        startPosition = transform.position;
        patrolSpeed = stats.moveSpeed;
        originalDetectionRange = detectionRange;
        attackRange = cd.radius;
        _originalColor = _spriteRenderer.color;
    }

    void Update()
    {
        // Eğer savruluyorsa (Knockback), AI ve hareket devre dışı
        if (_isKnockedBack) return;

        CheckState();
        Move();
    }

    // --- PLAYER SALDIRISIYLA TETİKLENEN HASAR ---
    public override void TakeDamage(float amount, bool isHeavy)
    {
        base.TakeDamage(amount, isHeavy);

        if (_currentHealth > 0)
        {
            StartCoroutine(HitFlashRoutine());
            // Ağır saldırı (Fire2) 15 birim, Hafif (Fire1) 5 birim savurur
            float force = isHeavy ? 15f : 5f;
            StartCoroutine(KnockbackRoutine(force));
        }
    }

    private IEnumerator HitFlashRoutine()
    {
    // 1. Sprite'ı belirlediğimiz renge (Flash Color) boya
        _spriteRenderer.color = flashColor;

    // 2. Çok kısa bir süre bekle (Gözün algılayacağı kadar: 0.1 saniye)
        yield return new WaitForSeconds(0.2f);

    // 3. Rengi eski haline döndür
        _spriteRenderer.color = _originalColor;
    }

      private IEnumerator KnockbackRoutine(float force)
    {
        _isKnockedBack = true;

        if (player != null)
        {
            Vector2 knockbackDirection = (transform.position - player.transform.position).normalized;
            _rb.linearVelocity = Vector2.zero; 
            _rb.AddForce(knockbackDirection * force, ForceMode2D.Impulse);
        }

        yield return new WaitForSeconds(knockbackDuration);

        _rb.linearVelocity = Vector2.zero;
        _isKnockedBack = false;
    }

    protected override void Move() 
    {
        if (player == null) return;

        float currentSpeed = (currentState == State.Patrol) ? patrolSpeed : stats.moveSpeed * 1.75f;

        if (currentState == State.Patrol)
        {
            transform.Translate(Vector3.right * patrolDirection * currentSpeed * Time.deltaTime);
            if (Vector2.Distance(startPosition, transform.position) >= patrolDistance)
                patrolDirection *= -1;
        }
        else if (currentState == State.Chase)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.transform.position);
            if (distanceToPlayer > attackRange)
            {
                Vector3 direction = (player.transform.position - transform.position).normalized;
                transform.Translate(direction * currentSpeed * Time.deltaTime, Space.World);
            }
        }

        // --- 4x4 FLIP ---
        if (player.transform.position.x > transform.position.x)
            transform.localScale = new Vector3(4f, 4f, 1f);
        else
            transform.localScale = new Vector3(-4f, 4f, 1f);
    }

    private void CheckState()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.transform.position);

        if (currentState == State.Patrol && distanceToPlayer <= detectionRange)
        {
            currentState = State.Chase;
            detectionRange = expandedDetectionRange;
        }
        else if (currentState == State.Chase && distanceToPlayer > detectionRange)
        {
            currentState = State.Patrol;
            startPosition = transform.position;
            detectionRange = originalDetectionRange;
        }
    }

    protected override void Die() 
    {
        base.Die();
        Instantiate(goldPrefab, transform.position, Quaternion.identity);
        Instantiate(experiencePrefab, transform.position + new Vector3(0.5f, 0, 0), Quaternion.identity);
        Destroy(gameObject);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        if(cd != null) Gizmos.DrawWireSphere(transform.position, cd.radius + 0.1f);
    }
}