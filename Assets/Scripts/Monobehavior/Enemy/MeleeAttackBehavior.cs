using UnityEngine;

/// <summary>
/// Melee attack style: closes to contact range and applies direct damage on a
/// cooldown while in Attack state.
/// </summary>
public class MeleeAttackBehavior : MonoBehaviour, IAttackBehavior
{
    [Tooltip("Time between melee hits (seconds).")]
    public float attackInterval = 2f;
    [Tooltip("Actual contact/proximity range for melee damage to be applied.")]
    public float hitRange = 1.6f;
    [Tooltip("Chase → Attack when player is within this distance; Attack → Chase when farther.")]
    public float attackRange = 5f;

    private Enemy _owner;
    private float _nextAttackTime;

    public float AttackRange => attackRange;

    // Slightly inside hit range so the motor doesn't oscillate on the exact boundary.
    public float DesiredApproachDistance => hitRange * 0.75f;

    public void Configure(Enemy owner)
    {
        _owner = owner;
        ResetForSpawn();
    }

    public void ResetForSpawn()
    {
        _nextAttackTime = Time.time;
    }

    // Melee has no wind-up to restart on state transitions.
    public void ResetAttackCooldown() { }

    public void TickAttack()
    {
        GameObject player = _owner.PlayerObject;
        if (_owner.CurrentState != Enemy.State.Attack || player == null) return;
        if (Time.time < _nextAttackTime) return;
        if (!CanHitPlayer()) return;

        IDamageable target = player.GetComponent<IDamageable>();
        if (target != null)
            target.TakeDamage(_owner.Stats != null ? _owner.Stats.enemyAP : 0f, false);

        _nextAttackTime = Time.time + attackInterval;
    }

    private bool CanHitPlayer()
    {
        if (!_owner.HasLineOfSightToPlayer()) return false;

        Vector2 origin = _owner.ReferencePosition;
        Collider2D[] nearby = Physics2D.OverlapCircleAll(origin, Mathf.Max(0.1f, hitRange));
        for (int i = 0; i < nearby.Length; i++)
        {
            Collider2D hit = nearby[i];
            if (hit == null) continue;
            if (hit.CompareTag("Player")) return true;
            if (hit.GetComponentInParent<Player>() != null) return true;
        }

        return false;
    }
}
