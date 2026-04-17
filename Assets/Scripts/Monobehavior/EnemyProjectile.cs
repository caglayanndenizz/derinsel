using UnityEngine;

/// <summary>
/// Spawn sonrası Initialize çağrılır. Hedef dünya noktasına doğru sabit hızda düz gider.
/// Prefab: Rigidbody2D (Kinematic) + Collider2D (Is Trigger) önerilir — yok olma sadece player tetikleyicisiyle.
/// </summary>
public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] float defaultMaxLifetime = 12f;

    float _speed;
    float _damage;
    Vector2 _target;
    Vector2 _direction;
    float _maxLifetime;
    float _spawnTime;
    bool _initialized;

    private EnemyProjectilePooler _pooler;

    void Awake()
    {
        ConfigureNoPushThroughPlayer();
    }

    /// <summary>
    /// Player'ı itmesin: kinematic RB + trigger collider. Player ile IgnoreCollision kullanma — yoksa OnTriggerEnter çalışmaz.
    /// </summary>
    void ConfigureNoPushThroughPlayer()
    {
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        foreach (var col in GetComponentsInChildren<Collider2D>(true))
            col.isTrigger = true;
    }

    public void Initialize(Vector2 targetWorldPosition, float speed, float damage, float maxLifetime = -1f)
    {
        ConfigureNoPushThroughPlayer();

        _target = targetWorldPosition;
        _speed = speed;
        _damage = damage;
        _maxLifetime = maxLifetime > 0f ? maxLifetime : defaultMaxLifetime;
        _spawnTime = Time.time;

        Vector2 origin = transform.position;
        Vector2 to = targetWorldPosition - origin;
        if (to.sqrMagnitude < 0.0001f)
        {
            ReturnToPool();
            return;
        }

        _direction = to.normalized;
        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        _initialized = true;
    }

    void Update()
    {
        if (!_initialized || _speed <= 0f) return;

        transform.position += (Vector3)(_direction * _speed * Time.deltaTime);

        if (Time.time - _spawnTime >= _maxLifetime)
        {
            ReturnToPool();
            return;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!_initialized || other == null) return;

        bool hitPlayer = other.CompareTag("Player") || other.GetComponentInParent<Player>() != null;
        if (!hitPlayer) return;

        IDamageable dmg = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
        if (dmg != null) dmg.TakeDamage(_damage, false);

        ReturnToPool();
    }

    void OnEnable()
    {
        if (_pooler == null) _pooler = EnemyProjectilePooler.Instance;

        _initialized = false;
        _speed = 0f;
        _damage = 0f;
        _target = Vector2.zero;
        _direction = Vector2.zero;
        _maxLifetime = defaultMaxLifetime;
        _spawnTime = Time.time;

        transform.rotation = Quaternion.identity;
        ConfigureNoPushThroughPlayer();
    }

    private void ReturnToPool()
    {
        if (_pooler == null) _pooler = EnemyProjectilePooler.Instance;

        if (_pooler != null)
        {
            _pooler.ReturnProjectile(gameObject);
            return;
        }

        gameObject.SetActive(false);
    }
}
