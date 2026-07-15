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
        _patrolAnchor   = _owner.ReferencePosition;
        _patrolLegIndex = 0;
        _isKnockedBack  = false;
    }

    public void ResetPatrolRoute()
    {
        _patrolLegIndex = 0;
    }

    private Vector2 GetPatrolWaypointWorld(int leg)
    {
        Vector2 left = Vector2.left * _owner.patrolLegLeft;
        Vector2 fwd = _owner.patrolForwardWorld.sqrMagnitude > 0.0001f
            ? _owner.patrolForwardWorld.normalized * _owner.patrolLegForward
            : Vector2.up * _owner.patrolLegForward;
        Vector2 right = Vector2.right * _owner.patrolLegRight;
        switch (leg)
        {
            case 0: return _patrolAnchor + left;
            case 1: return _patrolAnchor + left + fwd;
            case 2: return _patrolAnchor + left + fwd + right;
            case 3: return _patrolAnchor;
            default: return _patrolAnchor;
        }
    }

    private void AdvancePatrolLegIfReached(Vector2 pos)
    {
        Vector2 wp = GetPatrolWaypointWorld(_patrolLegIndex);
        if (Vector2.Distance(pos, wp) < _owner.patrolWaypointReachDistance)
            _patrolLegIndex = (_patrolLegIndex + 1) % 4;
    }

    public void MoveByState()
    {
        if (_owner.IsDead || _status.IsFrozen) return;

        if (_owner.enemyType != Enemy.EnemyType.Mage)
        {
            MoveMeleeTypeWithTranslate();
            return;
        }

        if (_rb == null) return;

        Vector2 velocity = Vector2.zero;
        float baseSpeed = _owner.Stats != null ? _owner.Stats.moveSpeed : 4f;
        Vector2 origin = _owner.ReferencePosition;
        Transform playerT = _owner.PlayerTransform;

        if (_owner.CurrentState == Enemy.State.Patrol)
        {
            AdvancePatrolLegIfReached(origin);
            Vector2 targetWp = GetPatrolWaypointWorld(_patrolLegIndex);
            Vector2 toWp = targetWp - origin;
            if (toWp.sqrMagnitude > 0.0001f)
            {
                Vector2 dir = toWp.normalized;
                velocity = _sensor.GetAvoidanceDirection(dir) * baseSpeed;
            }
        }
        else if (playerT != null && _owner.CurrentState == Enemy.State.Chase)
        {
            float dist = Vector2.Distance(origin, playerT.position);
            if (dist > _owner.attackCloseMaxDistance)
            {
                Vector2 targetDir = ((Vector2)playerT.position - origin).normalized;
                velocity = _sensor.GetAvoidanceDirection(targetDir) * baseSpeed * _owner.chaseApproachSpeedMultiplier;
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

    private void MoveMeleeTypeWithTranslate()
    {
        Transform playerT = _owner.PlayerTransform;
        if (playerT == null) return;
        if (_owner.CurrentState == Enemy.State.Patrol) return;

        if (_rb != null)
            _rb.linearVelocity = Vector2.zero;

        Vector3 toPlayer = playerT.position - transform.position;
        toPlayer.z = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return;

        float speed = _owner.Stats != null ? _owner.Stats.moveSpeed : 4f;
        Vector3 dir = toPlayer.normalized;
        transform.Translate(dir * speed * Time.fixedDeltaTime, Space.World);
        FaceByHorizontal(dir.x);
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
