using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bridges Enemy state to the art-pack Animator (PixelMonsters shared controller).
/// Mapping: Patrol → Walk/Idle, Chase → Run, Attack → Ready (or Run while still
/// closing in), death → Die. Idle is only ever reached from Patrol; Attack never
/// shows Idle. Controller-agnostic: parameters missing from the Animator are
/// skipped, so any enemy prefab can carry this component with its own controller variant.
/// </summary>
[RequireComponent(typeof(Enemy))]
public class EnemyAnimator : MonoBehaviour
{
    [Tooltip("Left empty, the first Animator on this object or its children is used.")]
    [SerializeField] private Animator animator;

    [Tooltip("One is picked at random per attack (equal chance). Names must be trigger parameters on the controller.")]
    [SerializeField] private string[] attackTriggers = { "Attack" };

    [Tooltip("Trigger fired whenever the enemy takes damage, including the killing blow. Empty = disabled.")]
    [SerializeField] private string hitTrigger = "Hit";

    [Tooltip("Patrol speeds below this count as standing still (Idle).")]
    [SerializeField] private float idleSpeedThreshold = 0.05f;

    [Tooltip("In the Attack state, farther than this from the player shows Run (still closing in); closer shows Ready.")]
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
    private EnemyMotor _motor;
    private int _hitTriggerHash;
    private int[] _attackTriggerHashes;
    private HashSet<int> _availableParams;

    /// <summary>Length of the controller's "Die" clip in seconds; 0 when the controller has none.</summary>
    public float DieAnimationLength { get; private set; }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _rb = GetComponent<Rigidbody2D>();
        _motor = GetComponent<EnemyMotor>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);

        _attackTriggerHashes = new int[attackTriggers.Length];
        for (int i = 0; i < attackTriggers.Length; i++)
            _attackTriggerHashes[i] = Animator.StringToHash(attackTriggers[i]);
        _hitTriggerHash = string.IsNullOrEmpty(hitTrigger) ? 0 : Animator.StringToHash(hitTrigger);

        _availableParams = new HashSet<int>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
                _availableParams.Add(p.nameHash);

            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name == "Die")
                {
                    DieAnimationLength = clip.length;
                    break;
                }
            }
        }
    }

    private void Start()
    {
        // Enemy.Awake resolves the IAttackBehavior (including the melee fallback),
        // so the subscription has to wait until Start.
        if (_enemy.AttackBehavior != null)
            _enemy.AttackBehavior.AttackPerformed += OnAttackPerformed;
        _enemy.Damaged += OnDamaged;
    }

    private void OnDestroy()
    {
        if (_enemy == null) return;
        if (_enemy.AttackBehavior != null)
            _enemy.AttackBehavior.AttackPerformed -= OnAttackPerformed;
        _enemy.Damaged -= OnDamaged;
    }

    private void OnDamaged()
    {
        if (animator == null || _hitTriggerHash == 0) return;
        if (_availableParams.Contains(_hitTriggerHash))
            animator.SetTrigger(_hitTriggerHash);
    }

    /// <summary>Pooled respawn reset; clears Die/trigger residue from the previous life.</summary>
    private void OnEnable()
    {
        if (animator == null) return;
        animator.Rebind();
        animator.Update(0f);
        animator.speed = 1f;
        ApplyExclusiveBool(IdleHash);
    }

    private void LateUpdate()
    {
        if (animator == null) return;

        if (_enemy.IsDead)
        {
            animator.speed = 1f;
            // Önce knockback tamamlanır; ölüm animasyonu ceset durduktan sonra başlar
            if (_motor == null || !_motor.IsKnockedBack)
                ApplyExclusiveBool(DieHash);
            return;
        }
        if (_enemy.IsFrozen) { animator.speed = 1f; ApplyExclusiveBool(IdleHash); return; }

        Enemy.State state = _enemy.CurrentState;

        int target;
        switch (state)
        {
            case Enemy.State.Attack:
                Transform playerT = _enemy.PlayerTransform;
                bool closingIn = playerT != null
                    && Vector2.Distance(_enemy.ReferencePosition, playerT.position) > runResumeDistance;
                if (closingIn)
                {
                    target = RunHash;
                }
                else
                {
                    target = ReadyHash; // Attack state never shows Idle; only Patrol can
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
        if (animator == null || _attackTriggerHashes.Length == 0) return;
        int hash = _attackTriggerHashes[Random.Range(0, _attackTriggerHashes.Length)];
        if (_availableParams.Contains(hash))
            animator.SetTrigger(hash);
    }
}
