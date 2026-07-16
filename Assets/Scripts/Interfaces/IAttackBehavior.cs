using UnityEngine;

/// <summary>
/// Attack-style strategy: a prefab carries either RangedAttackBehavior or
/// MeleeAttackBehavior. Enemy and EnemyMotor read this interface instead of
/// branching on an enemy type enum.
/// </summary>
public interface IAttackBehavior
{
    /// <summary>Fired the moment the attack actually happens (melee hit applied, projectile spawned).</summary>
    event System.Action AttackPerformed;

    /// <summary>Chase → Attack state transition threshold (world units).</summary>
    float AttackRange { get; }

    /// <summary>
    /// Distance at which the motor stops approaching the player:
    /// ranged keeps AttackRange, melee closes to hit range.
    /// </summary>
    float DesiredApproachDistance { get; }

    void Configure(Enemy owner);

    /// <summary>Pooled respawn reset; called from Enemy.OnEnable.</summary>
    void ResetForSpawn();

    /// <summary>Restarts the attack cooldown on state transitions.</summary>
    void ResetAttackCooldown();

    /// <summary>Called every Update while the enemy is alive and unfrozen.</summary>
    void TickAttack();
}
