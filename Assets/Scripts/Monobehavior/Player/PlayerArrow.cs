using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;

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
    float _freezeDuration;
    DungeonGenerator _dungeonGenerator;
    CinemachineImpulseSource _hitCameraImpulse;
    Vector2 _previousFramePosition;
    WallLootHandler _wallLootHandler;

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
        DungeonGenerator dungeonGenerator = null,
        CinemachineImpulseSource hitCameraImpulse = null,
        float freezeDuration = 0f)
    {
        _enemyMask = enemyMask;
        _speed = speed;
        _damage = damage;
        _maxLifetime = maxLifetime > 0f ? maxLifetime : defaultMaxLifetime;
        _spawnTime = Time.time;
        _fullyChargedBowExplosion = fullyChargedBowExplosion;
        _explosionRadius = chargedExplosionRadius;
        _freezeDuration = freezeDuration;
        _dungeonGenerator = dungeonGenerator;
        _hitCameraImpulse = hitCameraImpulse;

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
            _wallLootHandler = ownerRoot.GetComponent<WallLootHandler>();

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

        Vector2 prev = _previousFramePosition;
        Vector2 next = (Vector2)transform.position + _direction * _speed * Time.deltaTime;

        if (TryResolveMovementHit(prev, next, out RaycastHit2D hit))
        {
            PlayHitCameraShake();

            if (_fullyChargedBowExplosion && _explosionRadius > 0f)
            {
                Vector2 p = hit.point;
                List<Vector3> brokenWalls = null;
                if (_dungeonGenerator != null)
                    brokenWalls = _dungeonGenerator.BreakWallsInArea(p, _explosionRadius);
                ApplyChargedExplosionDamage(p);
                _wallLootHandler?.TrySpawnWallLootForBrokenWalls(brokenWalls);
            }
            else
                TryApplyDirectArrowDamage(hit.collider);

            Destroy(gameObject);
            return;
        }

        _previousFramePosition = (Vector2)transform.position;
        transform.position = new Vector3(next.x, next.y, transform.position.z);
    }

    void PlayHitCameraShake()
    {
        if (_hitCameraImpulse != null)
            _hitCameraImpulse.GenerateImpulse();
    }

    bool IsOwnCollider(Collider2D c)
    {
        if (c == null) return false;
        return c.transform == transform || c.transform.IsChildOf(transform);
    }

    bool TryResolveMovementHit(Vector2 from, Vector2 to, out RaycastHit2D hit)
    {
        hit = default;
        RaycastHit2D[] hits = Physics2D.LinecastAll(from, to);
        if (hits == null || hits.Length == 0) return false;

        foreach (RaycastHit2D h in hits.OrderBy(x => x.distance))
        {
            if (h.collider == null) continue;
            if (IsOwnCollider(h.collider)) continue;
            if (h.collider.GetComponentInParent<Player>() != null) continue;
            if (h.collider.GetComponentInParent<PlayerArrow>() != null) continue;

            hit = h;
            return true;
        }

        return false;
    }

    void TryApplyDirectArrowDamage(Collider2D other)
    {
        if (other == null) return;
        if (((1 << other.gameObject.layer) & _enemyMask) == 0) return;

        IDamageable dmg = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
        if (dmg == null) return;

        dmg.TakeDamage(_damage, false);

        if (_freezeDuration > 0f)
        {
            BaseEntity entity = other.GetComponent<BaseEntity>() ?? other.GetComponentInParent<BaseEntity>();
            if (entity != null && entity.CurrentHealth > 0f)
            {
                Enemy enemyComp = entity as Enemy ?? other.GetComponentInParent<Enemy>();
                if (enemyComp != null)
                    enemyComp.Freeze(_freezeDuration);
            }
        }
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
