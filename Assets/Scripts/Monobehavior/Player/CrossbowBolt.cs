using System.Collections.Generic;
using UnityEngine;

public class CrossbowBolt : MonoBehaviour
{
    [SerializeField] float defaultMaxLifetime = 6f;

    float   _speed;
    float   _damage;
    Vector2 _direction;
    float   _maxLifetime;
    float   _spawnTime;
    bool    _initialized;
    LayerMask _enemyMask;

    bool  _hasPierce;
    float _pierceFalloff;     // hasar düşüşü her düşman başına (örn. 0.20)
    float _pierceFloor;       // minimum hasar çarpanı (örn. 0.30)
    int   _pierceFalloffCount; // kaç düşman sonrası floor'a düşülür (örn. 3)
    int   _pierceCount;
    readonly HashSet<int> _hitEnemies = new();

    float _freezeDuration;
    bool  _hasFireArrow;
    float _fireDotDuration;
    float _fireDotDps;
    bool  _hasPoisonArrow;
    float _poisonDotDuration;
    float _poisonDotDps;

    bool  _hasBleed;
    float _bleedDamageRatioPerStack; // bolt hasarının yüzdesi (örn. 0.01 = %1)
    int   _bleedMaxStacks;
    float _bleedExpireSeconds;

    Vector2 _previousFramePosition;

    void Awake()
    {
        foreach (var col in GetComponentsInChildren<Collider2D>(true))
            col.isTrigger = true;
    }

    public void Initialize(
        Vector2   targetWorldPosition,
        float     speed,
        float     damage,
        float     maxLifetime,
        LayerMask enemyMask,
        Transform ownerRoot,
        bool      hasPierce          = false,
        float     pierceFalloff      = 0.20f,
        float     pierceFloor        = 0.30f,
        int       pierceFalloffCount = 3,
        float     freezeDuration     = 0f,
        bool      hasFireArrow       = false,
        float     fireDotDuration    = 0f,
        float     fireDotDps         = 0f,
        bool      hasPoisonArrow     = false,
        float     poisonDotDuration  = 0f,
        float     poisonDotDps       = 0f,
        bool      hasBleed               = false,
        float     bleedDamageRatioPerStack = 0.01f,
        int       bleedMaxStacks         = 5,
        float     bleedExpireSeconds     = 5f)
    {
        _enemyMask          = enemyMask;
        _speed              = speed;
        _damage             = damage;
        _maxLifetime        = maxLifetime > 0f ? maxLifetime : defaultMaxLifetime;
        _spawnTime          = Time.time;
        _hasPierce          = hasPierce;
        _pierceFalloff      = pierceFalloff;
        _pierceFloor        = pierceFloor;
        _pierceFalloffCount = Mathf.Max(1, pierceFalloffCount);
        _pierceCount        = 0;
        _freezeDuration     = freezeDuration;
        _hasFireArrow       = hasFireArrow;
        _fireDotDuration    = fireDotDuration;
        _fireDotDps         = fireDotDps;
        _hasPoisonArrow          = hasPoisonArrow;
        _poisonDotDuration       = poisonDotDuration;
        _poisonDotDps            = poisonDotDps;
        _hasBleed                = hasBleed;
        _bleedDamageRatioPerStack = bleedDamageRatioPerStack;
        _bleedMaxStacks          = bleedMaxStacks;
        _bleedExpireSeconds      = bleedExpireSeconds;

        Vector2 origin = transform.position;
        Vector2 to = targetWorldPosition - origin;
        if (to.sqrMagnitude < 0.0001f) { Destroy(gameObject); return; }

        _direction = to.normalized;
        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
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

        if (Time.time - _spawnTime >= _maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        Vector2 from = _previousFramePosition;
        Vector2 to   = (Vector2)transform.position + _direction * _speed * Time.deltaTime;

        RaycastHit2D[] hits = Physics2D.LinecastAll(from, to);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool destroyBolt = false;
        foreach (RaycastHit2D h in hits)
        {
            if (h.collider == null)                                     continue;
            if (IsOwnCollider(h.collider))                              continue;
            if (h.collider.GetComponentInParent<Player>() != null)      continue;
            if (h.collider.GetComponentInParent<CrossbowBolt>() != null) continue;
            if (h.collider.isTrigger)                                   continue;

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

        if (destroyBolt) { Destroy(gameObject); return; }

        _previousFramePosition = (Vector2)transform.position;
        transform.position = new Vector3(to.x, to.y, transform.position.z);
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

        // Pierce hasar hesabı:
        // - 0..falloffCount-1. düşman: 1 - pierceCount * falloff
        // - falloffCount. düşman ve sonrası: floor
        float mult = _hasPierce
            ? (_pierceCount >= _pierceFalloffCount
                ? _pierceFloor
                : Mathf.Max(1f - _pierceCount * _pierceFalloff, _pierceFloor))
            : 1f;

        dmg.TakeDamage(_damage * mult, false);
        _pierceCount++;

        if (enemy.CurrentHealth > 0f)
        {
            if (_freezeDuration  > 0f)                enemy.Freeze(_freezeDuration);
            if (_hasFireArrow   && _fireDotDps   > 0f) enemy.ApplyFireDoT(_fireDotDuration, _fireDotDps);
            if (_hasPoisonArrow && _poisonDotDps  > 0f) enemy.ApplyPoisonDoT(_poisonDotDuration, _poisonDotDps);
            if (_hasBleed)
            {
                float damagePerStack = _damage * _bleedDamageRatioPerStack;
                enemy.ApplyBleedStack(damagePerStack, _bleedMaxStacks, _bleedExpireSeconds);
            }
        }
    }
}
