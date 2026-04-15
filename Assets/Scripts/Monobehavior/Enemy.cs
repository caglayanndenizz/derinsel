using UnityEngine;
using System.Collections;

public class Enemy : BaseEntity
{
    public enum State { Patrol, Chase, Dead }

    [Header("State Settings")]
    public State currentState = State.Patrol;
    public float detectionRange = 8f; 
    public float expandedDetectionRange = 20f; 
    public float attackRange;
    public CircleCollider2D cd;

    [Header("Knockback Settings (Adjust for 3-4 units)")]
    public float lightKnockbackForce = 4f;  // Hafif vuruş için (Fire1)
    public float heavyKnockbackForce = 10f; // Ağır vuruş için (Fire2)
    public float knockbackDuration = 0.2f;
    private bool _isKnockedBack = false;
    private Rigidbody2D _rb;

    [Header("Patrol Settings")]
    public float patrolDistance = 4f;
    private Vector3 startPosition;
    private int patrolDirection = 1;

    [Header("Movement Speed")]
    private float patrolSpeed;

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
        
        startPosition = transform.position;
        patrolSpeed = stats.moveSpeed;
        
        // Collider varsa radius'u al, yoksa default 1f
        attackRange = (cd != null) ? cd.radius : 1f;
        _originalColor = _spriteRenderer.color;
    }

    void Update()
    {
        // Eğer savruluyorsa veya ölüyse hareket/AI çalışmasın
        if (_isDead || _isKnockedBack) return;

        CheckState();
        Move();
    }

    // --- HASAR SİSTEMİ (ÖLÜMDE SAVRULMA DAHİL) ---
    public override void TakeDamage(float amount, bool isHeavy)
    {
        _currentHealth -= amount;

        // Görsel flash efekti
        StartCoroutine(HitFlashRoutine());

        // Fiziksel savrulma
        float force = isHeavy ? heavyKnockbackForce : lightKnockbackForce;
        StartCoroutine(KnockbackRoutine(force));

        if (_currentHealth <= 0 && !_isDead)
        {
            PrepareToDie();
        }
    }

    private void PrepareToDie()
    {
        _isDead = true;
        currentState = State.Dead;

        // Savrulurken takılmaması için collider'ı kapat
        if (cd != null) cd.enabled = false;

        // Obje yok edilmeden önce savrulmanın bitmesini bekle
        Invoke("Die", knockbackDuration + 0.05f);
    }

    protected override void Die() 
    {
        // Loot saçılımı
        Instantiate(goldPrefab, transform.position, Quaternion.identity);
        Instantiate(experiencePrefab, transform.position + new Vector3(0.3f, 0, 0), Quaternion.identity);
        
        base.Die(); 
        Destroy(gameObject);
    }

    private IEnumerator HitFlashRoutine()
    {
        _spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(0.15f);
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

        if (!_isDead)
        {
            _rb.linearVelocity = Vector2.zero;
            _isKnockedBack = false;
        }
    }

    protected override void Move() 
    {
        if (player == null || _isDead) return;

        float currentSpeed = (currentState == State.Patrol) ? patrolSpeed : stats.moveSpeed * 1.6f;
        Vector2 targetVelocity = _rb.linearVelocity;

        if (currentState == State.Patrol)
        {
            targetVelocity = new Vector2(patrolDirection * currentSpeed, _rb.linearVelocity.y);
            
            if (Vector2.Distance(startPosition, transform.position) >= patrolDistance)
            {
                patrolDirection *= -1;
                startPosition = transform.position; 
            }
        }
        else if (currentState == State.Chase)
        {
            Vector2 direction = (player.transform.position - transform.position).normalized;
            float distanceToPlayer = Vector2.Distance(transform.position, player.transform.position);

            if (distanceToPlayer > attackRange)
            {
                targetVelocity = direction * currentSpeed;
            }
            else
            {
                targetVelocity = Vector2.zero;
            }
        }

        _rb.linearVelocity = targetVelocity;

        // --- GÖRSEL YÖN (FLIP) ---
        if (currentState == State.Chase)
        {
            if (player.transform.position.x > transform.position.x)
                transform.localScale = new Vector3(4f, 4f, 1f);
            else
                transform.localScale = new Vector3(-4f, 4f, 1f);
        }
        else
        {
            if (patrolDirection > 0)
                transform.localScale = new Vector3(4f, 4f, 1f);
            else
                transform.localScale = new Vector3(-4f, 4f, 1f);
        }
    }

    private void CheckState()
    {
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.transform.position);

        if (currentState == State.Patrol && distanceToPlayer <= detectionRange)
        {
            currentState = State.Chase;
        }
        else if (currentState == State.Chase && distanceToPlayer > expandedDetectionRange)
        {
            currentState = State.Patrol;
            startPosition = transform.position;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        Gizmos.color = Color.red;
        if(cd != null) Gizmos.DrawWireSphere(transform.position, cd.radius);
    }
}