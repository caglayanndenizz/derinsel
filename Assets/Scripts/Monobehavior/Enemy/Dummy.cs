using UnityEngine;

/// <summary>
/// Stationary damage-testing target: reacts to every weapon like a real enemy would
/// (correct per-hit damage rolls, floating damage numbers, knockback) but never loses
/// health, never dies, and has no attack/AI behaviour of its own.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Dummy : MonoBehaviour, IDamageable
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("If empty, auto-resolved from the GameObject tagged Player.")]
    [SerializeField] private Transform playerTransform;

    [Header("Knockback")]
    [Tooltip("Impulse force applied away from the player on every hit.")]
    [SerializeField] private float knockbackForce = 5f;
    [Tooltip("Layers treated as walls for the bounce reaction.")]
    [SerializeField] private LayerMask wallLayerMask;
    [Tooltip("Wall bounce force = knockbackForce * this multiplier.")]
    [SerializeField] private float wallBounceForceMultiplier = 0.5f;

    private Rigidbody2D _rb;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
    }

    public void TakeDamage(float amount, bool isHeavy)
    {
        DamageNumberPooler.SpawnDamageNumber(transform.position, amount, isHeavy);
        if (spriteRenderer != null)
            spriteRenderer.flipX = true;
        ApplyKnockbackFromPlayer();
    }

    private void ApplyKnockbackFromPlayer()
    {
        Vector2 dir = playerTransform != null
            ? (Vector2)transform.position - (Vector2)playerTransform.position
            : Vector2.zero;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();

        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & wallLayerMask) == 0) return;

        Vector2 incomingDir = collision.relativeVelocity.normalized;
        Vector2 normal = collision.GetContact(0).normal;
        Vector2 bounceDir = Vector2.Reflect(incomingDir, normal);
        if (bounceDir.sqrMagnitude < 0.0001f) bounceDir = normal;

        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(bounceDir * (knockbackForce * wallBounceForceMultiplier), ForceMode2D.Impulse);
    }
}
