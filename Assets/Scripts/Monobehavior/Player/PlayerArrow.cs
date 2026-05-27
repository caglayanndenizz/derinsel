using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class PlayerArrow : MonoBehaviour
{
    [SerializeField] float defaultMaxLifetime = 8f;

    [Header("Element Particle Effects")]
    [SerializeField] ParticleSystem fireParticles;
    [SerializeField] ParticleSystem poisonParticles;
    [SerializeField] ParticleSystem iceParticles;
    [SerializeField] ParticleSystem chaosParticles; // Active only when all three elements are unlocked

    float _speed;
    float _damage;
    Vector2 _direction;
    float _maxLifetime;
    float _spawnTime;
    bool _initialized;
    bool _wasVisibleSinceSpawn;
    LayerMask _enemyMask;
    bool _fullyChargedLongbowExplosion;
    float _explosionRadius;
    float _freezeDuration;
    float _frozenVulnerabilityMult; // Resonance: donmuş düşmana uygulanan hasar çarpanı
    bool  _hasIceArrow;   // true when the ice/freeze unlock is active — drives ice particles
    bool  _hasFireArrow;
    float _fireDotDuration;
    float _fireDotDps;
    bool  _hasPoisonArrow;
    float _poisonDotDuration;
    float _poisonDotDps;
    DungeonGenerator _dungeonGenerator;
    CinemachineImpulseSource _hitCameraImpulse;
    Vector2 _previousFramePosition;
    WallLootHandler _wallLootHandler;

    void Awake()
    {
        foreach (var col in GetComponentsInChildren<Collider2D>(true))
            col.isTrigger = true;
    }

    void OnEnable()
    {
        _wasVisibleSinceSpawn = false;
        StopAllElementParticles();
    }
    void OnBecameVisible()   { _wasVisibleSinceSpawn = true; }
    void OnBecameInvisible() { if (_initialized && _wasVisibleSinceSpawn) ReturnToPool(); }

    public void Initialize(
        Vector2 targetWorldPosition,
        float speed,
        float damage,
        float maxLifetime,
        LayerMask enemyMask,
        Transform ownerRoot,
        bool fullyChargedLongbowExplosion = false,
        float chargedExplosionRadius = 0f,
        DungeonGenerator dungeonGenerator = null,
        CinemachineImpulseSource hitCameraImpulse = null,
        float freezeDuration = 0f,
        bool hasIceArrow = false,
        bool hasFireArrow = false,
        float fireDotDuration = 0f,
        float fireDotDps = 0f,
        bool hasPoisonArrow = false,
        float poisonDotDuration = 0f,
        float poisonDotDps = 0f,
        float frozenVulnerabilityMult = 1f)
    {
        _enemyMask = enemyMask;
        _speed = speed;
        _damage = damage;
        _maxLifetime = maxLifetime > 0f ? maxLifetime : defaultMaxLifetime;
        _spawnTime = Time.time;
        _fullyChargedLongbowExplosion = fullyChargedLongbowExplosion;
        _explosionRadius   = chargedExplosionRadius;
        _freezeDuration           = freezeDuration;
        _frozenVulnerabilityMult  = Mathf.Max(1f, frozenVulnerabilityMult);
        _hasIceArrow              = hasIceArrow;
        _hasFireArrow      = hasFireArrow;
        _fireDotDuration   = fireDotDuration;
        _fireDotDps        = fireDotDps;
        _hasPoisonArrow    = hasPoisonArrow;
        _poisonDotDuration = poisonDotDuration;
        _poisonDotDps      = poisonDotDps;
        _dungeonGenerator  = dungeonGenerator;
        _hitCameraImpulse = hitCameraImpulse;

        RefreshElementParticles();
        _initialized = false;

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
        _wasVisibleSinceSpawn = true;
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
            ReturnToPool();
            return;
        }

        Vector2 prev = _previousFramePosition;
        Vector2 next = (Vector2)transform.position + _direction * _speed * Time.deltaTime;

        if (TryResolveMovementHit(prev, next, out RaycastHit2D hit))
        {
            PlayHitCameraShake();

            if (_fullyChargedLongbowExplosion && _explosionRadius > 0f)
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

            ReturnToPool();
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

    // Pre-allocated buffer — eliminates per-frame RaycastHit2D[] heap allocation
    private static readonly RaycastHit2D[]  _hitBuffer      = new RaycastHit2D[32];
    // ContactFilter2D.noFilter: tüm layer ve trigger'ları kapsar — LinecastNonAlloc ile birebir aynı davranış
    private static readonly ContactFilter2D _linecastFilter = ContactFilter2D.noFilter;

    bool TryResolveMovementHit(Vector2 from, Vector2 to, out RaycastHit2D hit)
    {
        hit = default;
        int count = Physics2D.Linecast(from, to, _linecastFilter, _hitBuffer);
        if (count == 0) return false;

        System.Array.Sort(_hitBuffer, 0, count, _raycastComparer);

        for (int i = 0; i < count; i++)
        {
            RaycastHit2D h = _hitBuffer[i];
            if (h.collider == null) continue;
            if (IsOwnCollider(h.collider)) continue;
            if (h.collider.GetComponentInParent<Player>() != null) continue;
            if (h.collider.GetComponentInParent<PlayerArrow>() != null) continue;
            if (h.collider.isTrigger) continue;

            hit = h;
            return true;
        }

        return false;
    }

    // Statik comparer — lambda yerine kullanılır, her frame allocation yaratmaz
    private static readonly RaycastDistanceComparer _raycastComparer = new RaycastDistanceComparer();
    private class RaycastDistanceComparer : System.Collections.Generic.IComparer<RaycastHit2D>
    {
        public int Compare(RaycastHit2D a, RaycastHit2D b) => a.distance.CompareTo(b.distance);
    }

    void TryApplyDirectArrowDamage(Collider2D other)
    {
        if (other == null) return;
        if (((1 << other.gameObject.layer) & _enemyMask) == 0) return;

        IDamageable dmg = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
        if (dmg == null) return;

        dmg.TakeDamage(_damage, false);

        Enemy enemy = other.GetComponent<Enemy>() ?? other.GetComponentInParent<Enemy>();

        if (_freezeDuration > 0f && enemy != null && enemy.CurrentHealth > 0f)
            enemy.Freeze(_freezeDuration, _frozenVulnerabilityMult);

        if (enemy != null && enemy.CurrentHealth > 0f)
        {
            if (_hasFireArrow   && _fireDotDps   > 0f) enemy.ApplyFireDoT(_fireDotDuration, _fireDotDps);
            if (_hasPoisonArrow && _poisonDotDps > 0f) enemy.ApplyPoisonDoT(_poisonDotDuration, _poisonDotDps);
        }
    }

    void ReturnToPool()
    {
        _initialized = false;
        StopAllElementParticles();
        if (PlayerArrowPooler.Instance != null)
            PlayerArrowPooler.Instance.ReturnArrow(gameObject);
        else
            Destroy(gameObject);
    }

    // ── Element Particles ─────────────────────────────────────────────────────

    void RefreshElementParticles()
    {
        bool hasFire   = _hasFireArrow;
        bool hasPoison = _hasPoisonArrow;
        // _hasIceArrow is the authoritative bool (mirrors HasLongbowFreezeUnlock).
        // _freezeDuration > 0f is kept as a fallback for any edge-case path that
        // sets the freeze value but forgets the bool.
        bool hasIce    = _hasIceArrow || _freezeDuration > 0f;
        bool chaos     = hasFire && hasPoison && hasIce; // All three unlocked → Chaos mode

        if (chaos)
        {
            // Chaos mode: suppress individual effects, show chaos particle only
            SetParticle(fireParticles,   false);
            SetParticle(poisonParticles, false);
            SetParticle(iceParticles,    false);
            SetParticle(chaosParticles,  true);
        }
        else
        {
            SetParticle(chaosParticles,  false);
            SetParticle(fireParticles,   hasFire);
            SetParticle(poisonParticles, hasPoison);
            SetParticle(iceParticles,    hasIce);
        }
    }

    void StopAllElementParticles()
    {
        SetParticle(fireParticles,   false);
        SetParticle(poisonParticles, false);
        SetParticle(iceParticles,    false);
        SetParticle(chaosParticles,  false);
    }

    static void SetParticle(ParticleSystem ps, bool active)
    {
        if (ps == null) return;
        if (active)
        {
            if (!ps.isPlaying) ps.Play();
        }
        else
        {
            if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
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

            dmg.TakeDamage(_damage, false);
        }
    }
}
