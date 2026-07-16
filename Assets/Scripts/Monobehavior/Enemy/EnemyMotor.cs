using UnityEngine;
using System.Collections;

/// <summary>
/// Enemy movement: patrol route, chase, melee approach, magnet pull motion and knockback.
/// Config values stay serialized on Enemy; this component reads them through its owner.
/// </summary>
public class EnemyMotor : MonoBehaviour
{
    private Enemy _owner;
    private EntitySensor _sensor;
    private EntityStatusEffects _status;
    private Rigidbody2D _rb;

    private Vector2 _patrolAnchor;
    private int _patrolLegIndex;
    private float _patrolLegDeadline;
    private bool _patrolLegStarted;
    private bool _patrolWaiting;
    private float _patrolWaitUntil;
    private bool _isKnockedBack;

    public bool IsKnockedBack => _isKnockedBack;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    public void Configure(Enemy owner, EntitySensor sensor, EntityStatusEffects status)
    {
        _owner  = owner;
        _sensor = sensor;
        _status = status;
        ResetForSpawn();
    }

    /// <summary>Pooled respawn reset; called from Enemy.OnEnable.</summary>
    public void ResetForSpawn()
    {
        if (_owner == null) return;
        _patrolAnchor     = _owner.ReferencePosition;
        _patrolLegIndex   = 0;
        _patrolLegStarted = false;
        _patrolWaiting    = false;
        _isKnockedBack    = false;
    }

    public void ResetPatrolRoute()
    {
        _patrolLegIndex   = 0;
        _patrolLegStarted = false;
        _patrolWaiting    = false;
    }

    /// <summary>
    /// Ping-pong route around the spawn anchor: anchor → left → anchor → right → anchor,
    /// each side patrolLegUnits wide. Directions swap every round by construction.
    /// </summary>
    private Vector2 GetPatrolWaypointWorld(int leg)
    {
        switch (leg)
        {
            case 0: return _patrolAnchor + Vector2.left * _owner.patrolLegUnits;
            case 1: return _patrolAnchor;
            case 2: return _patrolAnchor + Vector2.right * _owner.patrolLegUnits;
            default: return _patrolAnchor;
        }
    }

    private void StartPatrolLeg(Vector2 from, float secondsPerUnit)
    {
        Vector2 wp = GetPatrolWaypointWorld(_patrolLegIndex);
        // 50% tolerance + fixed buffer: a blocked waypoint gets skipped instead of
        // being pushed at forever.
        _patrolLegDeadline = Time.time + Vector2.Distance(from, wp) * secondsPerUnit * 1.5f + 0.5f;
        _patrolLegStarted  = true;
    }

    private void AdvancePatrolLeg(Vector2 from, float secondsPerUnit)
    {
        _patrolLegIndex = (_patrolLegIndex + 1) % 4;
        StartPatrolLeg(from, secondsPerUnit);
    }

    /// <summary>Legs 0 and 2 end at the left/right extremes; anchor legs are pass-through.</summary>
    private static bool IsEndpointLeg(int leg) => leg == 0 || leg == 2;

    public void MoveByState()
    {
        if (_owner.IsDead || _status.IsFrozen) return;
        if (_rb == null) return;

        Vector2 velocity = Vector2.zero;
        Vector2 origin = _owner.ReferencePosition;
        Transform playerT = _owner.PlayerTransform;

        if (_owner.CurrentState == Enemy.State.Patrol)
        {
            // stats.walkSpeed = seconds to traverse one unit; patrol speed is its inverse
            float secondsPerUnit = _owner.Stats != null ? Mathf.Max(0.01f, _owner.Stats.walkSpeed) : 1f;

            if (!_patrolLegStarted) StartPatrolLeg(origin, secondsPerUnit);

            // Endpoint wait: stand still at zero velocity (animator shows Idle), then resume
            if (_patrolWaiting)
            {
                if (Time.time < _patrolWaitUntil)
                {
                    _rb.linearVelocity = Vector2.zero;
                    return;
                }
                _patrolWaiting = false;
                AdvancePatrolLeg(origin, secondsPerUnit);
            }

            Vector2 targetWp = GetPatrolWaypointWorld(_patrolLegIndex);
            bool reached  = Vector2.Distance(origin, targetWp) < _owner.patrolWaypointReachDistance;
            bool timedOut = Time.time >= _patrolLegDeadline;

            if (reached && IsEndpointLeg(_patrolLegIndex) && _owner.patrolWaitSeconds > 0f)
            {
                _patrolWaiting  = true;
                _patrolWaitUntil = Time.time + _owner.patrolWaitSeconds;
                _rb.linearVelocity = Vector2.zero;
                return;
            }

            if (reached || timedOut)
            {
                AdvancePatrolLeg(origin, secondsPerUnit);
                targetWp = GetPatrolWaypointWorld(_patrolLegIndex);
            }

            Vector2 toWp = targetWp - origin;
            if (toWp.sqrMagnitude > 0.0001f)
                velocity = _sensor.GetAvoidanceDirection(toWp.normalized) * (1f / secondsPerUnit);
        }
        else if (playerT != null)
        {
            // Chase ve Attack: behavior'ın istediği mesafeye kadar yaklaş
            // (ranged menzilinde durur, melee vuruş mesafesine kadar girer)
            float dist = Vector2.Distance(origin, playerT.position);
            if (dist > _owner.AttackBehavior.DesiredApproachDistance)
            {
                float runSpeed = _owner.Stats != null ? _owner.Stats.runSpeed : 5f;
                Vector2 targetDir = ((Vector2)playerT.position - origin).normalized;
                velocity = _sensor.GetAvoidanceDirection(targetDir) * runSpeed;
            }
        }

        _rb.linearVelocity = velocity;

        if (_owner.CurrentState == Enemy.State.Patrol)
        {
            if (velocity.sqrMagnitude > 0.0001f)
                FaceByHorizontal(velocity.x);
        }
        else if (playerT != null)
            FaceByHorizontal(playerT.position.x - transform.position.x);
    }

    public void FaceByHorizontal(float horizontal)
    {
        if (Mathf.Abs(horizontal) < 0.0001f) return;

        Vector3 currentScale = transform.localScale;
        float xMagnitude = Mathf.Abs(currentScale.x);
        if (xMagnitude < 0.0001f) xMagnitude = 1f;

        currentScale.x = horizontal >= 0f ? xMagnitude : -xMagnitude;
        transform.localScale = currentScale;
    }

    public void ApplyMagnetMovement()
    {
        Vector2 current = _rb != null ? _rb.position : (Vector2)transform.position;
        Vector2 toTarget = _status.MagnetTargetPos - current;
        if (toTarget.sqrMagnitude < 0.04f) return; // already close enough

        Vector2 dir = toTarget.normalized;
        FaceByHorizontal(dir.x);

        if (_rb != null)
            _rb.linearVelocity = dir * _status.MagnetPullSpeed;
        else
            transform.Translate(dir * _status.MagnetPullSpeed * Time.fixedDeltaTime, Space.World);
    }

    public void ApplyKnockback(float force)
    {
        StartCoroutine(KnockbackRoutine(force));
    }

    private IEnumerator KnockbackRoutine(float force)
    {
        _isKnockedBack = true;
        Transform playerT = _owner.PlayerTransform;
        if (playerT != null)
        {
            Vector2 dir = ((Vector2)transform.position - (Vector2)playerT.position).normalized;
            _rb.linearVelocity = Vector2.zero;
            _rb.AddForce(dir * force, ForceMode2D.Impulse);
        }

        float elapsed = 0f;
        while (elapsed < _owner.knockbackDuration)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;

            Vector2 currentPos = _owner.ReferencePosition;
            if (_sensor.IsNavigationBlockedAt(currentPos))
            {
                if (_rb != null)
                {
                    _rb.position = _sensor.LastSafeWorldPosition;
                    _rb.linearVelocity = Vector2.zero;
                }
                else
                {
                    transform.position = _sensor.LastSafeWorldPosition;
                }
            }
            else
            {
                _sensor.SetLastSafePosition(currentPos);
            }
        }

        if (!_owner.IsDead) { _rb.linearVelocity = Vector2.zero; _isKnockedBack = false; }
    }
}
