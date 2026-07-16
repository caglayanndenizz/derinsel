using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animator bridge for boss-type enemies. Bosses have no Patrol/Idle wandering,
/// so unlike EnemyAnimator this component does not poll a state machine: the
/// boss's own script (built on the future boss base class) drives it explicitly
/// through the public API (SetMoving, PlayAttack, PlayHit, PlayDie, SetPhase...).
/// Same controller-agnostic guarantees as EnemyAnimator: parameter names are
/// configurable per prefab and anything missing from the controller is skipped,
/// so each boss can ship its own bespoke Animator without touching this script.
/// </summary>
public class BossAnimator : MonoBehaviour
{
    [Tooltip("Left empty, the first Animator on this object or its children is used.")]
    [SerializeField] private Animator animator;

    [Header("Parameter Names")]
    [Tooltip("Bool set while the boss is moving; cleared when it stands still.")]
    [SerializeField] private string moveBool = "Run";

    [Tooltip("Bool set once when the boss dies.")]
    [SerializeField] private string dieBool = "Die";

    [Tooltip("Trigger fired when the boss takes a hit. Empty = disabled.")]
    [SerializeField] private string hitTrigger = "Hit";

    [Tooltip("Attack trigger pool; PlayAttack() without an index picks one at random, PlayAttack(i) picks a specific one.")]
    [SerializeField] private string[] attackTriggers = { "Attack", "Attack2", "Attack3" };

    [Header("Locomotion")]
    [Tooltip("Movement speed (units/sec) at which locomotion clips play at 1x while moving. 0 = no speed matching, always 1x.")]
    [SerializeField] private float animReferenceSpeed = 0f;

    private Rigidbody2D _rb;
    private HashSet<int> _availableParams;
    private int _moveHash;
    private int _dieHash;
    private int _hitHash;
    private int[] _attackHashes;
    private bool _isDead;
    private bool _isMoving;

    public bool IsDead => _isDead;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);

        _moveHash = Animator.StringToHash(moveBool);
        _dieHash  = Animator.StringToHash(dieBool);
        _hitHash  = Animator.StringToHash(hitTrigger);
        _attackHashes = new int[attackTriggers.Length];
        for (int i = 0; i < attackTriggers.Length; i++)
            _attackHashes[i] = Animator.StringToHash(attackTriggers[i]);

        _availableParams = new HashSet<int>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
                _availableParams.Add(p.nameHash);
        }
    }

    /// <summary>Pooled respawn reset; clears death/trigger residue from the previous life.</summary>
    private void OnEnable()
    {
        _isDead = false;
        _isMoving = false;
        if (animator == null) return;
        animator.Rebind();
        animator.Update(0f);
        animator.speed = 1f;
    }

    private void LateUpdate()
    {
        if (animator == null || _isDead || !_isMoving) return;

        // Optional locomotion speed matching, same formula as EnemyAnimator.
        if (animReferenceSpeed > 0f)
        {
            float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
            animator.speed = Mathf.Max(0.2f, speed / animReferenceSpeed);
        }
    }

    /// <summary>Toggles the locomotion state; the boss script calls this from its own movement logic.</summary>
    public void SetMoving(bool moving)
    {
        if (_isDead) return;
        _isMoving = moving;
        SetBoolIfPresent(_moveHash, moving);
        if (animator != null && !moving) animator.speed = 1f;
    }

    /// <summary>Fires a random trigger from the attack pool.</summary>
    public void PlayAttack()
    {
        if (_attackHashes.Length == 0) return;
        PlayAttack(Random.Range(0, _attackHashes.Length));
    }

    /// <summary>Fires a specific attack trigger by pool index (e.g. a phase-bound special).</summary>
    public void PlayAttack(int index)
    {
        if (_isDead || animator == null) return;
        if (index < 0 || index >= _attackHashes.Length) return;
        if (_availableParams.Contains(_attackHashes[index]))
            animator.SetTrigger(_attackHashes[index]);
    }

    /// <summary>Fires the hit-reaction trigger, if one is configured.</summary>
    public void PlayHit()
    {
        if (_isDead || animator == null) return;
        if (_availableParams.Contains(_hitHash))
            animator.SetTrigger(_hitHash);
    }

    /// <summary>Enters the death state; every other call becomes a no-op until respawn.</summary>
    public void PlayDie()
    {
        if (_isDead) return;
        _isDead = true;
        _isMoving = false;
        if (animator != null) animator.speed = 1f;
        SetBoolIfPresent(_moveHash, false);
        SetBoolIfPresent(_dieHash, true);
    }

    /// <summary>Passthrough for boss-specific bools (phase flags like "Enraged"); skipped if the controller lacks it.</summary>
    public void SetBool(string parameterName, bool value)
        => SetBoolIfPresent(Animator.StringToHash(parameterName), value);

    /// <summary>Passthrough for boss-specific triggers (intro roars, phase transitions); skipped if the controller lacks it.</summary>
    public void SetTrigger(string parameterName)
    {
        if (animator == null) return;
        int hash = Animator.StringToHash(parameterName);
        if (_availableParams.Contains(hash))
            animator.SetTrigger(hash);
    }

    private void SetBoolIfPresent(int hash, bool value)
    {
        if (animator != null && _availableParams.Contains(hash))
            animator.SetBool(hash, value);
    }
}
