using UnityEngine;

/// <summary>
/// Ranged attack style: fires pooled projectiles at the player's last known
/// position while in Chase/Attack state. Owns all projectile config that used
/// to live as mage* fields on Enemy.
/// </summary>
public class RangedAttackBehavior : MonoBehaviour, IAttackBehavior
{
    [Header("Projectile")]
    [Tooltip("Prefab must have an EnemyProjectile script.")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f;
    [Tooltip("Uses EnemyEntityStats.enemyAP as damage when enabled.")]
    public bool useAttackPowerForProjectile = true;
    public float projectileDamageOverride = 5f;
    public float projectileMaxLifetime = 12f;

    [Header("Fire Timing")]
    [Tooltip("Time between projectiles while in Attack state (seconds).")]
    public float fireInterval = 4f;
    [Tooltip("Fire rate multiplier. 1.5 => 50% faster fire rate.")]
    public float fireRateMultiplier = 1.5f;

    [Header("Spawn Point")]
    [Tooltip("Projectile spawns from this child transform (searches for 'projectilePivot' child if empty).")]
    public Transform projectilePivot;
    public Transform projectileSpawnPoint;
    public Vector3 projectileSpawnOffset = Vector3.zero;
    public EnemyProjectilePooler projectilePooler;

    [Header("Range")]
    [Tooltip("Chase → Attack when player is within this distance; Attack → Chase when farther.")]
    public float attackRange = 10f;

    private Enemy _owner;
    private float _nextFireTime;

    public float AttackRange => attackRange;
    public float DesiredApproachDistance => attackRange;

    private float AdjustedFireInterval => fireInterval / Mathf.Max(0.01f, fireRateMultiplier);

    public void Configure(Enemy owner)
    {
        _owner = owner;
        if (projectilePivot == null)
            projectilePivot = transform.Find("projectilePivot");
        ResetForSpawn();
    }

    public void ResetForSpawn()
    {
        _nextFireTime = Time.time + AdjustedFireInterval;
    }

    public void ResetAttackCooldown()
    {
        _nextFireTime = Time.time + AdjustedFireInterval;
    }

    public void TickAttack()
    {
        if (_owner.CurrentState == Enemy.State.Patrol || _owner.PlayerObject == null || projectilePrefab == null) return;
        if (!_owner.HasLineOfSightToPlayer()) return;
        if (Time.time < _nextFireTime) return;

        Vector2 aim = _owner.HasLastKnownPlayerPosition
            ? _owner.LastKnownPlayerPosition
            : (Vector2)_owner.PlayerObject.transform.position;
        Vector3 spawnPos = GetProjectileSpawnPosition();

        if (projectilePooler == null)
            projectilePooler = EnemyProjectilePooler.Instance;

        float dmg = projectileDamageOverride;
        if (useAttackPowerForProjectile && _owner.Stats != null)
            dmg = _owner.Stats.enemyAP;
        if (dmg <= 0f)
            dmg = Mathf.Max(0.0001f, projectileDamageOverride);

        GameObject proj = projectilePooler != null
            ? projectilePooler.GetProjectile(spawnPos, Quaternion.identity, mover =>
            {
                if (mover != null)
                    mover.Initialize(aim, projectileSpeed, dmg, projectileMaxLifetime);
            })
            : Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

        if (proj != null && projectilePooler == null)
        {
            var mover = proj.GetComponent<EnemyProjectile>();
            if (mover != null)
                mover.Initialize(aim, projectileSpeed, dmg, projectileMaxLifetime);
        }

        _nextFireTime = Time.time + AdjustedFireInterval;
    }

    private Vector3 GetProjectileSpawnPosition()
    {
        if (projectilePivot != null) return projectilePivot.position;
        if (projectileSpawnPoint != null) return projectileSpawnPoint.position;
        return transform.position + projectileSpawnOffset;
    }
}
