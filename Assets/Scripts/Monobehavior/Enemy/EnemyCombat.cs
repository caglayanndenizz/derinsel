using UnityEngine;

/// <summary>
/// Enemy attacks: mage ranged projectile and melee hit, driven by the owner's state.
/// Config values stay serialized on Enemy; this component reads them through its owner.
/// </summary>
public class EnemyCombat : MonoBehaviour
{
    private Enemy _owner;

    private float _nextRangedFireTime;
    private float _nextTypeAttackTime;

    private float AdjustedMageRangedFireInterval
        => _owner.mageRangedFireInterval / Mathf.Max(0.01f, _owner.mageProjectileFireRateMultiplier);

    public void Configure(Enemy owner)
    {
        _owner = owner;
        ResetForSpawn();
    }

    /// <summary>Pooled respawn reset; called from Enemy.OnEnable.</summary>
    public void ResetForSpawn()
    {
        if (_owner == null) return;
        _nextRangedFireTime = _owner.enemyType == Enemy.EnemyType.Mage
            ? Time.time + AdjustedMageRangedFireInterval
            : 0f;
        _nextTypeAttackTime = Time.time;
    }

    /// <summary>Mage only: restarts the ranged fire cooldown on state transitions.</summary>
    public void ResetRangedFireCooldown()
    {
        if (_owner.enemyType == Enemy.EnemyType.Mage)
            _nextRangedFireTime = Time.time + AdjustedMageRangedFireInterval;
    }

    public void TickAttack()
    {
        switch (_owner.enemyType)
        {
            case Enemy.EnemyType.Mage:
                TryFireRangedProjectile();
                break;
            case Enemy.EnemyType.Tanky:
            case Enemy.EnemyType.Warrior:
                TryMeleeAttackOnPlayer();
                break;
        }
    }

    private void TryMeleeAttackOnPlayer()
    {
        GameObject player = _owner.PlayerObject;
        if (_owner.CurrentState != Enemy.State.Attack || player == null) return;
        if (Time.time < _nextTypeAttackTime) return;
        if (!CanHitPlayerWithMelee()) return;

        IDamageable target = player.GetComponent<IDamageable>();
        if (target != null)
            target.TakeDamage(_owner.Stats != null ? _owner.Stats.enemyAP : 0f, false);

        _nextTypeAttackTime = Time.time + _owner.meleeAttackInterval;
    }

    private bool CanHitPlayerWithMelee()
    {
        if (_owner.PlayerObject == null) return false;
        if (!_owner.HasLineOfSightToPlayer()) return false;

        Vector2 origin = _owner.ReferencePosition;
        Collider2D[] nearby = Physics2D.OverlapCircleAll(origin, Mathf.Max(0.1f, _owner.meleeHitRange));
        for (int i = 0; i < nearby.Length; i++)
        {
            Collider2D hit = nearby[i];
            if (hit == null) continue;
            if (hit.CompareTag("Player")) return true;
            if (hit.GetComponentInParent<Player>() != null) return true;
        }

        return false;
    }

    private Vector3 GetProjectileSpawnPosition()
    {
        if (_owner.enemyType != Enemy.EnemyType.Mage) return transform.position;
        if (_owner.mageProjectilePivot != null) return _owner.mageProjectilePivot.position;
        if (_owner.mageProjectileSpawnPoint != null) return _owner.mageProjectileSpawnPoint.position;
        return transform.position + _owner.mageProjectileSpawnOffset;
    }

    private void TryFireRangedProjectile()
    {
        if (_owner.enemyType != Enemy.EnemyType.Mage) return;
        if (_owner.CurrentState == Enemy.State.Patrol || _owner.PlayerObject == null || _owner.mageProjectilePrefab == null) return;
        if (!_owner.HasLineOfSightToPlayer()) return;
        if (Time.time < _nextRangedFireTime) return;

        Vector2 aim = _owner.HasLastKnownPlayerPosition
            ? _owner.LastKnownPlayerPosition
            : (Vector2)_owner.PlayerObject.transform.position;
        Vector3 spawnPos = GetProjectileSpawnPosition();

        if (_owner.mageProjectilePooler == null)
            _owner.mageProjectilePooler = EnemyProjectilePooler.Instance;

        float dmg = _owner.mageProjectileDamageOverride;
        if (_owner.mageUseAttackPowerForProjectile && _owner.Stats != null)
            dmg = _owner.Stats.enemyAP;
        if (dmg <= 0f)
            dmg = Mathf.Max(0.0001f, _owner.mageProjectileDamageOverride);

        GameObject proj = _owner.mageProjectilePooler != null
            ? _owner.mageProjectilePooler.GetProjectile(spawnPos, Quaternion.identity, mover =>
            {
                if (mover != null)
                    mover.Initialize(aim, _owner.mageProjectileSpeed, dmg, _owner.mageProjectileMaxLifetime);
            })
            : Instantiate(_owner.mageProjectilePrefab, spawnPos, Quaternion.identity);

        if (proj == null)
        {
            _nextRangedFireTime = Time.time + AdjustedMageRangedFireInterval;
            return;
        }

        if (_owner.mageProjectilePooler == null)
        {
            var mover = proj.GetComponent<EnemyProjectile>();
            if (mover != null)
                mover.Initialize(aim, _owner.mageProjectileSpeed, dmg, _owner.mageProjectileMaxLifetime);
        }

        _nextRangedFireTime = Time.time + AdjustedMageRangedFireInterval;
    }
}
