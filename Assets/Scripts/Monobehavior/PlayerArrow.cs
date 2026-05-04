using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Oyuncu oku: Initialize sonrası hedef dünya noktasına doğru sabit hızla gider.
/// Prefab: Collider2D (Is Trigger) önerilir; oyuncu colliderlarıyla IgnoreCollision uygulanır.
/// </summary>
public class PlayerArrow : MonoBehaviour
{
    [SerializeField] float defaultMaxLifetime = 8f;

    float _speed;
    float _damage;
    Vector2 _direction;
    float _maxLifetime;
    float _spawnTime;
    bool _initialized;
    LayerMask _enemyMask;
    bool _fullyChargedBowExplosion;
    float _explosionRadius;
    DungeonGenerator _dungeonGenerator;
    Vector2 _previousFramePosition;

    void Awake()
    {
        foreach (var col in GetComponentsInChildren<Collider2D>(true))
            col.isTrigger = true;
    }

    public void Initialize(
        Vector2 targetWorldPosition,
        float speed,
        float damage,
        float maxLifetime,
        LayerMask enemyMask,
        Transform ownerRoot,
        bool fullyChargedBowExplosion = false,
        float chargedExplosionRadius = 0f,
        DungeonGenerator dungeonGenerator = null)
    {
        _enemyMask = enemyMask;
        _speed = speed;
        _damage = damage;
        _maxLifetime = maxLifetime > 0f ? maxLifetime : defaultMaxLifetime;
        _spawnTime = Time.time;
        _fullyChargedBowExplosion = fullyChargedBowExplosion;
        _explosionRadius = chargedExplosionRadius;
        _dungeonGenerator = dungeonGenerator;

        Vector2 origin = transform.position;
        Vector2 to = targetWorldPosition - origin;
        if (to.sqrMagnitude < 0.0001f)
        {
            Destroy(gameObject);
            return;
        }

        _direction = to.normalized;
        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        _initialized = true;
        _previousFramePosition = transform.position;

        if (ownerRoot != null)
        {
            var arrowCols = GetComponentsInChildren<Collider2D>(true);
            var ownerCols = ownerRoot.GetComponentsInChildren<Collider2D>(true);
            foreach (var ac in arrowCols)
            {
                if (ac == null) continue;
                foreach (var oc in ownerCols)
                {
                    if (oc == null) continue;
                    Physics2D.IgnoreCollision(ac, oc, true);
                }
            }
        }
    }

    void Update()
    {
        if (!_initialized || _speed <= 0f) return;

        if (Time.time - _spawnTime >= _maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (_fullyChargedBowExplosion && _explosionRadius > 0f)
        {
            Vector2 prev = _previousFramePosition;
            Vector2 next = (Vector2)transform.position + _direction * _speed * Time.deltaTime;

            if (TryResolveChargedMovementHit(prev, next, out Vector2 hitPoint))
            {
                if (_dungeonGenerator != null)
                    _dungeonGenerator.BreakWallsInArea(hitPoint, _explosionRadius);
                ApplyChargedExplosionDamage(hitPoint);
                Destroy(gameObject);
                return;
            }

            _previousFramePosition = (Vector2)transform.position;
            transform.position = new Vector3(next.x, next.y, transform.position.z);
            return;
        }

        transform.position += (Vector3)(_direction * _speed * Time.deltaTime);
    }

    bool IsColliderOnDungeonWallTilemap(Collider2D other)
    {
        if (_dungeonGenerator == null || _dungeonGenerator.wallTilemap == null) return false;
        Tilemap tm = other.GetComponent<Tilemap>() ?? other.GetComponentInParent<Tilemap>();
        return tm != null && tm == _dungeonGenerator.wallTilemap;
    }

    /// <summary>
    /// Duvarlar genelde enemy maskesinde değildir; transform ile hareket tetikleri kaçırabilir.
    /// Tam şarjlı ok için segment üzerinde ilk duvar tilemap veya düşman layer isabeti.
    /// </summary>
    bool TryResolveChargedMovementHit(Vector2 from, Vector2 to, out Vector2 hitWorld)
    {
        hitWorld = default;
        RaycastHit2D[] hits = Physics2D.LinecastAll(from, to);
        if (hits == null || hits.Length == 0) return false;

        foreach (RaycastHit2D h in hits.OrderBy(x => x.distance))
        {
            if (h.collider == null) continue;
            if (h.collider.GetComponentInParent<Player>() != null) continue;

            if (IsColliderOnDungeonWallTilemap(h.collider))
            {
                hitWorld = h.point;
                return true;
            }

            if (((1 << h.collider.gameObject.layer) & _enemyMask) != 0)
            {
                hitWorld = h.point;
                return true;
            }
        }

        return false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!_initialized || other == null) return;
        if (other.GetComponentInParent<Player>() != null) return;

        // Tam şarjlı patlama: çarpışma Update içinde Linecast ile (duvar + düşman); tetikleyici ile çift işlemeyi önle.
        if (_fullyChargedBowExplosion && _explosionRadius > 0f)
            return;

        if (((1 << other.gameObject.layer) & _enemyMask) == 0) return;

        IDamageable dmg = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
        if (dmg != null)
            dmg.TakeDamage(_damage, false);

        Destroy(gameObject);
    }

    static float GetLethalDamageForTarget(IDamageable target)
    {
        if (target is BaseEntity entity)
            return Mathf.Max(0f, entity.CurrentHealth);
        if (target is DestructibleObject destructible)
            return Mathf.Max(0f, destructible.health);
        return 1e6f;
    }

    void ApplyChargedExplosionDamage(Vector2 center)
    {
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(center, _explosionRadius);
        if (overlaps == null || overlaps.Length == 0) return;

        var processedDamageables = new HashSet<int>();

        foreach (Collider2D col in overlaps)
        {
            if (col == null) continue;
            if (col.GetComponentInParent<Player>() != null) continue;

            IDamageable dmg = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
            if (dmg == null) continue;

            var mb = dmg as MonoBehaviour;
            if (mb == null) continue;

            int id = mb.gameObject.GetInstanceID();
            if (!processedDamageables.Add(id)) continue;

            float amount = GetLethalDamageForTarget(dmg);
            if (amount <= 0f) continue;
            dmg.TakeDamage(amount, false);
        }
    }
}
