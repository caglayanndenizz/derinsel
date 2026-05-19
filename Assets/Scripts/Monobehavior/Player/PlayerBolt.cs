using System.Collections.Generic;
using UnityEngine;

public class PlayerBolt : MonoBehaviour
{
    [SerializeField] float defaultMaxLifetime = 5f;
    [Tooltip("Sprite ileri yonu saga (0°) bakmiyorsa buradan duzeltin. Yukari bakiyorsa -90, asagi bakiyorsa 90 girin.")]
    [SerializeField] float spriteRotationOffset = 0f;

    float     _speed;
    float     _damage;
    Vector2   _direction;
    float     _maxLifetime;
    float     _spawnTime;
    bool      _initialized;
    bool      _wasVisibleSinceSpawn;
    LayerMask _enemyMask;

    bool  _hasPierce;
    float _pierceFalloff;
    float _pierceFloor;
    int   _pierceFalloffCount;
    int   _pierceCount;
    readonly HashSet<int> _hitEnemies = new();

    bool  _hasBleed;
    float _bleedDamageRatioPerStack;
    int   _bleedMaxStacks;
    float _bleedExpireSeconds;

    Vector2 _previousFramePosition;

    void Awake()
    {
        foreach (var col in GetComponentsInChildren<Collider2D>(true))
            col.isTrigger = true;
    }

    void OnEnable()  => _wasVisibleSinceSpawn = false;
    void OnBecameVisible()   { _wasVisibleSinceSpawn = true; }
    void OnBecameInvisible() { if (_initialized && _wasVisibleSinceSpawn) ReturnToPool(); }

    public void Initialize(
        Vector2   targetWorldPosition,
        float     speed,
        float     damage,
        float     maxLifetime,
        LayerMask enemyMask,
        Transform ownerRoot,
        bool      hasPierce               = false,
        float     pierceFalloff           = 0.20f,
        float     pierceFloor             = 0.30f,
        int       pierceFalloffCount      = 3,
        bool      hasBleed                = false,
        float     bleedDamageRatioPerStack = 0.01f,
        int       bleedMaxStacks          = 5,
        float     bleedExpireSeconds      = 5f)
    {
        _speed               = speed;
        _damage              = damage;
        _maxLifetime         = maxLifetime > 0f ? maxLifetime : defaultMaxLifetime;
        _spawnTime           = Time.time;
        _enemyMask           = enemyMask;
        _hasPierce           = hasPierce;
        _pierceFalloff       = pierceFalloff;
        _pierceFloor         = pierceFloor;
        _pierceFalloffCount  = Mathf.Max(1, pierceFalloffCount);
        _hasBleed            = hasBleed;
        _bleedDamageRatioPerStack = bleedDamageRatioPerStack;
        _bleedMaxStacks      = bleedMaxStacks;
        _bleedExpireSeconds  = bleedExpireSeconds;

        _pierceCount = 0;
        _hitEnemies.Clear();

        Vector2 origin = transform.position;
        Vector2 to     = targetWorldPosition - origin;
        if (to.sqrMagnitude < 0.0001f) { ReturnToPool(); return; }

        _direction = to.normalized;
        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + spriteRotationOffset);
        _wasVisibleSinceSpawn = true;
        _initialized = true;
        _previousFramePosition = transform.position;

        if (ownerRoot != null)
        {
            var boltCols  = GetComponentsInChildren<Collider2D>(true);
            var ownerCols = ownerRoot.GetComponentsInChildren<Collider2D>(true);
            foreach (var bc in boltCols)
                foreach (var oc in ownerCols)
                    if (bc != null && oc != null)
                        Physics2D.IgnoreCollision(bc, oc, true);
        }
    }

    void Update()
    {
        if (!_initialized || _speed <= 0f) return;

        if (Time.time - _spawnTime >= _maxLifetime) { ReturnToPool(); return; }

        Vector2 prev = _previousFramePosition;
        Vector2 next = (Vector2)transform.position + _direction * _speed * Time.deltaTime;

        bool destroyBolt = false;
        RaycastHit2D[] hits = Physics2D.LinecastAll(prev, next);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit2D h in hits)
        {
            if (h.collider == null)                                  continue;
            if (IsOwnCollider(h.collider))                           continue;
            if (h.collider.GetComponentInParent<Player>() != null)   continue;
            if (h.collider.GetComponentInParent<PlayerBolt>() != null) continue;
            if (h.collider.isTrigger)                                continue;

            bool isEnemy = ((1 << h.collider.gameObject.layer) & _enemyMask) != 0;

            if (isEnemy)
            {
                TryHitEnemy(h.collider);
                if (!_hasPierce) { destroyBolt = true; break; }
            }
            else
            {
                destroyBolt = true;
                break;
            }
        }

        if (destroyBolt) { ReturnToPool(); return; }

        _previousFramePosition = (Vector2)transform.position;
        transform.position = new Vector3(next.x, next.y, transform.position.z);
    }

    bool IsOwnCollider(Collider2D c) =>
        c != null && (c.transform == transform || c.transform.IsChildOf(transform));

    void TryHitEnemy(Collider2D col)
    {
        IDamageable dmg = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
        if (dmg == null) return;

        Enemy enemy = col.GetComponent<Enemy>() ?? col.GetComponentInParent<Enemy>();
        if (enemy == null) return;

        int id = enemy.gameObject.GetInstanceID();
        if (!_hitEnemies.Add(id)) return;

        float mult = _hasPierce
            ? (_pierceCount >= _pierceFalloffCount
                ? _pierceFloor
                : Mathf.Max(1f - _pierceCount * _pierceFalloff, _pierceFloor))
            : 1f;

        dmg.TakeDamage(_damage * mult, false);
        _pierceCount++;

        if (_hasBleed && enemy.CurrentHealth > 0f)
            enemy.ApplyBleedStack(_damage * _bleedDamageRatioPerStack, _bleedMaxStacks, _bleedExpireSeconds);
    }

    void ReturnToPool()
    {
        _initialized = false;
        if (PlayerArrowPooler.Instance != null)
            PlayerArrowPooler.Instance.ReturnBolt(gameObject);
        else
            Destroy(gameObject);
    }
}
