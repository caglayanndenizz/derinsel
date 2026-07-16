using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bridges Enemy state to the art-pack Animator (PixelMonsters shared controller).
/// Mapping: Patrol → Walk, Chase → Run, death → Die, everything else → Idle.
/// Entering the Attack state plays Ready once as a wind-up; the first swing
/// (melee hit / projectile spawn) fires an attack trigger and drops back to Idle
/// between swings. While the player is farther than runResumeDistance the
/// animator falls back to Run (still closing in) and the Ready wind-up re-arms.
/// Controller-agnostic: parameters missing from the Animator are skipped, so any
/// enemy prefab can carry this component with its own controller variant.
/// </summary>
[RequireComponent(typeof(Enemy))]
public class EnemyAnimator : MonoBehaviour
{
    [Tooltip("Left empty, the first Animator on this object or its children is used.")]
    [SerializeField] private Animator animator;

    [Tooltip("One is picked at random per attack (equal chance). Names must be trigger parameters on the controller.")]
    [SerializeField] private string[] attackTriggers = { "Attack" };

    [Tooltip("Patrol speeds below this count as standing still (Idle).")]
    [SerializeField] private float idleSpeedThreshold = 0.05f;

    [Tooltip("In the Attack state, farther than this from the player shows Run (still closing in); the Ready wind-up re-arms.")]
    [SerializeField] private float runResumeDistance = 1.5f;

    [Tooltip("Movement speed (units/sec) at which locomotion clips play at 1x; slower movement plays them proportionally slower.")]
    [SerializeField] private float animReferenceSpeed = 5f;

    private static readonly int IdleHash  = Animator.StringToHash("Idle");
    private static readonly int WalkHash  = Animator.StringToHash("Walk");
    private static readonly int RunHash   = Animator.StringToHash("Run");
    private static readonly int ReadyHash = Animator.StringToHash("Ready");
    private static readonly int DieHash   = Animator.StringToHash("Die");

    private Enemy _enemy;
    private Rigidbody2D _rb;
    private int[] _attackTriggerHashes;
    private HashSet<int> _availableParams;
    private Enemy.State _lastState = Enemy.State.Patrol;
    private bool _readyPending;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);

        _attackTriggerHashes = new int[attackTriggers.Length];
        for (int i = 0; i < attackTriggers.Length; i++)
            _attackTriggerHashes[i] = Animator.StringToHash(attackTriggers[i]);

        _availableParams = new HashSet<int>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
                _availableParams.Add(p.nameHash);
        }
    }

    private void Start()
    {
        // Enemy.Awake resolves the IAttackBehavior (including the melee fallback),
        // so the subscription has to wait until Start.
        if (_enemy.AttackBehavior != null)
            _enemy.AttackBehavior.AttackPerformed += OnAttackPerformed;
    }

    private void OnDestroy()
    {
        if (_enemy != null && _enemy.AttackBehavior != null)
            _enemy.AttackBehavior.AttackPerformed -= OnAttackPerformed;
    }

    /// <summary>Pooled respawn reset; clears Die/trigger residue from the previous life.</summary>
    private void OnEnable()
    {
        _readyPending = false;
        _lastState = Enemy.State.Patrol;
        if (animator == null) return;
        animator.Rebind();
        animator.Update(0f);
        animator.speed = 1f;
        ApplyExclusiveBool(IdleHash);
    }

    private void LateUpdate()
    {
        if (animator == null) return;

        if (_enemy.IsDead)   { animator.speed = 1f; ApplyExclusiveBool(DieHash);  return; }
        if (_enemy.IsFrozen) { animator.speed = 1f; ApplyExclusiveBool(IdleHash); return; }

        Enemy.State state = _enemy.CurrentState;
        if (state == Enemy.State.Attack && _lastState != Enemy.State.Attack)
            _readyPending = true; // one Ready wind-up per approach; cleared by the first swing
        _lastState = state;

        int target;
        switch (state)
        {
            case Enemy.State.Attack:
                Transform playerT = _enemy.PlayerTransform;
                bool closingIn = playerT != null
                    && Vector2.Distance(_enemy.ReferencePosition, playerT.position) > runResumeDistance;
                if (closingIn)
                {
                    _readyPending = true; // wind-up replays once the enemy gets back in swing range
                    target = RunHash;
                }
                else
                {
                    target = _readyPending ? ReadyHash : IdleHash;
                }
                break;
            case Enemy.State.Chase:
                target = RunHash;
                break;
            case Enemy.State.Patrol:
                bool moving = _rb != null && _rb.linearVelocity.sqrMagnitude > idleSpeedThreshold * idleSpeedThreshold;
                target = moving ? WalkHash : IdleHash;
                break;
            default:
                target = IdleHash;
                break;
        }

        // Locomotion clips track actual movement speed (walk plays slower than run);
        // every other state runs at normal playback.
        if (target == WalkHash || target == RunHash)
        {
            float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
            animator.speed = Mathf.Max(0.2f, speed / Mathf.Max(0.01f, animReferenceSpeed));
        }
        else
        {
            animator.speed = 1f;
        }

        ApplyExclusiveBool(target);
    }

    private void ApplyExclusiveBool(int activeHash)
    {
        SetBoolIfPresent(IdleHash,  activeHash == IdleHash);
        SetBoolIfPresent(WalkHash,  activeHash == WalkHash);
        SetBoolIfPresent(RunHash,   activeHash == RunHash);
        SetBoolIfPresent(ReadyHash, activeHash == ReadyHash);
        SetBoolIfPresent(DieHash,   activeHash == DieHash);
    }

    private void SetBoolIfPresent(int hash, bool value)
    {
        if (_availableParams.Contains(hash))
            animator.SetBool(hash, value);
    }

    private void OnAttackPerformed()
    {
        _readyPending = false;
        if (animator == null || _attackTriggerHashes.Length == 0) return;
        int hash = _attackTriggerHashes[Random.Range(0, _attackTriggerHashes.Length)];
        if (_availableParams.Contains(hash))
            animator.SetTrigger(hash);
    }
}
