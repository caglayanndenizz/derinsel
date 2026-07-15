using UnityEngine;
using System.Collections;

/// <summary>
/// Freeze, fire/poison DoT, bleed stack and magnet pull state for any BaseEntity.
/// Depends only on BaseEntity (TakeDamage/IsDead), so future bosses can reuse it as-is.
/// Movement/physics reactions (magnet movement, freeze stopping the entity) are read
/// by the owning entity via IsFrozen / IsMagnetPulled each frame.
/// </summary>
public class EntityStatusEffects : MonoBehaviour
{
    /// <summary>Raised whenever any status starts or ends. EntityStatusVisuals listens to this.</summary>
    public event System.Action StatusChanged;

    private BaseEntity  _entity;
    private Rigidbody2D _rb;

    private bool      _isFrozen;
    private float     _frozenVulnerabilityMultiplier = 1f; // Resonance: LongbowFreezeUnlock ile artar
    private Coroutine _freezeCoroutine;

    private bool      _isOnFire;
    private Coroutine _fireCoroutine;

    private bool      _isPoisoned;
    private Coroutine _poisonCoroutine;

    private bool      _isBleeding;
    private Coroutine _bleedCoroutine;
    private int       _bleedStacks;
    private float     _bleedDamagePerStack;
    private float     _lastBleedHitTime;
    private int       _bleedMaxStacks;
    private float     _bleedExpireSeconds;

    // Hammer Charge Magnet — set by Player every frame, auto-expires after a short duration
    private bool    _isMagnetPulled;
    private Vector2 _magnetTargetPos;
    private float   _magnetPullSpeed;
    private float   _magnetPullExpireTime;

    public bool IsFrozen   => _isFrozen;
    public bool IsOnFire   => _isOnFire;
    public bool IsPoisoned => _isPoisoned;
    public bool IsBleeding => _isBleeding;

    /// <summary>Resonance: frozen entities take multiplied damage. 1 when not frozen.</summary>
    public float DamageTakenMultiplier => _isFrozen ? _frozenVulnerabilityMultiplier : 1f;

    public bool    IsMagnetPulled  => _isMagnetPulled && Time.time < _magnetPullExpireTime;
    public Vector2 MagnetTargetPos => _magnetTargetPos;
    public float   MagnetPullSpeed => _magnetPullSpeed;

    private bool EntityDead => _entity != null && _entity.IsDead;

    private void Awake()
    {
        _entity = GetComponent<BaseEntity>();
        _rb     = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        ResetAll();
    }

    /// <summary>Clears every status. Called on enable so pooled entities respawn clean.</summary>
    public void ResetAll()
    {
        if (_freezeCoroutine != null) { StopCoroutine(_freezeCoroutine); _freezeCoroutine = null; }
        if (_fireCoroutine   != null) { StopCoroutine(_fireCoroutine);   _fireCoroutine   = null; }
        if (_poisonCoroutine != null) { StopCoroutine(_poisonCoroutine); _poisonCoroutine = null; }
        if (_bleedCoroutine  != null) { StopCoroutine(_bleedCoroutine);  _bleedCoroutine  = null; }
        _isFrozen    = false;
        _isOnFire    = false;
        _isPoisoned  = false;
        _isBleeding  = false;
        _bleedStacks = 0;
        _frozenVulnerabilityMultiplier = 1f;
        _isMagnetPulled  = false;
        _magnetPullSpeed = 0f;
        StatusChanged?.Invoke();
    }

    /// <summary>
    /// Freezes the entity. If vulnerabilityMultiplier > 1f (Resonance unlock),
    /// damage taken during the freeze is multiplied.
    /// </summary>
    public void Freeze(float duration, float vulnerabilityMultiplier = 1f)
    {
        if (EntityDead) return;
        if (_freezeCoroutine != null)
            StopCoroutine(_freezeCoroutine);
        _freezeCoroutine = StartCoroutine(FreezeRoutine(duration, vulnerabilityMultiplier));
    }

    private IEnumerator FreezeRoutine(float duration, float vulnerabilityMultiplier)
    {
        _isFrozen = true;
        _isMagnetPulled = false; // cancel magnet pull when freeze begins
        _frozenVulnerabilityMultiplier = Mathf.Max(1f, vulnerabilityMultiplier);
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        StatusChanged?.Invoke();

        yield return new WaitForSeconds(duration);

        _isFrozen = false;
        _frozenVulnerabilityMultiplier = 1f;
        _freezeCoroutine = null;
        StatusChanged?.Invoke();
    }

    /// <summary>
    /// Called by the Hammer Charge Magnet. Auto-expires if calls stop.
    /// </summary>
    public void SetMagnetPull(Vector2 targetPos, float speed, float expireDuration = 0.15f)
    {
        if (EntityDead || _isFrozen) return;
        _isMagnetPulled       = true;
        _magnetTargetPos      = targetPos;
        _magnetPullSpeed      = speed;
        _magnetPullExpireTime = Time.time + expireDuration;
    }

    public void ApplyFireDoT(float duration, float dps)
    {
        if (EntityDead || duration <= 0f || dps <= 0f) return;
        if (_fireCoroutine != null) StopCoroutine(_fireCoroutine);
        _fireCoroutine = StartCoroutine(FireDoTRoutine(duration, dps));
    }

    public void ApplyPoisonDoT(float duration, float dps)
    {
        if (EntityDead || duration <= 0f || dps <= 0f) return;
        if (_poisonCoroutine != null) StopCoroutine(_poisonCoroutine);
        _poisonCoroutine = StartCoroutine(PoisonDoTRoutine(duration, dps));
    }

    public void ApplyBleedStack(float damagePerStack, int maxStacks = 5, float expireSeconds = 5f)
    {
        if (EntityDead || damagePerStack <= 0f) return;
        _bleedMaxStacks      = Mathf.Max(1, maxStacks);
        _bleedExpireSeconds  = Mathf.Max(0f, expireSeconds);
        _bleedDamagePerStack = damagePerStack;
        _bleedStacks         = Mathf.Min(_bleedStacks + 1, _bleedMaxStacks);
        _lastBleedHitTime    = Time.time;
        _isBleeding          = true;
        StatusChanged?.Invoke();
        if (_bleedCoroutine == null)
            _bleedCoroutine = StartCoroutine(BleedRoutine());
    }

    private IEnumerator FireDoTRoutine(float duration, float dps)
    {
        _isOnFire = true;
        StatusChanged?.Invoke();

        float elapsed = 0f;
        float tickInterval = 0.5f;
        float damagePerTick = dps * tickInterval;

        while (elapsed < duration && !EntityDead)
        {
            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
            if (EntityDead) break;
            _entity.TakeDamage(damagePerTick, false);
        }

        _isOnFire = false;
        _fireCoroutine = null;
        StatusChanged?.Invoke();
    }

    private IEnumerator PoisonDoTRoutine(float duration, float dps)
    {
        _isPoisoned = true;
        StatusChanged?.Invoke();

        float elapsed = 0f;
        float tickInterval = 1f;
        float damagePerTick = dps * tickInterval;

        while (elapsed < duration && !EntityDead)
        {
            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
            if (EntityDead) break;
            _entity.TakeDamage(damagePerTick, false);
        }

        _isPoisoned = false;
        _poisonCoroutine = null;
        StatusChanged?.Invoke();
    }

    private IEnumerator BleedRoutine()
    {
        while (_bleedStacks > 0 && !EntityDead)
        {
            yield return new WaitForSeconds(1f);
            if (EntityDead) break;

            if (Time.time - _lastBleedHitTime > _bleedExpireSeconds)
                break;

            _entity.TakeDamage(_bleedStacks * _bleedDamagePerStack, false);
        }

        _bleedStacks    = 0;
        _isBleeding     = false;
        _bleedCoroutine = null;
        StatusChanged?.Invoke();
    }
}
