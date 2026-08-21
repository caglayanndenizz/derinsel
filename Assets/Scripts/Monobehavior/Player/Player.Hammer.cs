using UnityEngine;
using UnityEngine.UI;

public partial class Player
{
    [Header("Hammer Settings (Heavy)")]
    public float maxChargeTime = 1.5f;
    public float hammerAOE = 2.5f;
    [SerializeField] private float heavyImpactFallbackDelay = 0.2f;

    [Header("Spammable Light Attack Settings")]
    public float lightAttackDuration = 0.1f;
    [SerializeField] private float lightImpactFallbackDelay = 0.08f;
    private float _lastLightAttackResolveTime = -999f;
    private bool _lightAttackInProgress = false;
    private float _lightFallbackExecuteAt = -1f;
    private float _lastHeavyResolveTime = -999f;
    private bool _heavyAttackInProgress = false;
    private float _heavyFallbackExecuteAt = -1f;
    private float _pendingChargeFraction = 1f;

    [Header("Hammer Charge UI")]
    public Slider chargeMeter;
    public GameObject meterCanvas;

    private bool _prevChargeFullState = false;

    // ─── Attack resolution (animation events + fallbacks) ────────────────────

    public void LightAttack()
    {
        ClearLightAttackPendingState();
    }

    private void ClearLightAttackPendingState()
    {
        if (Time.time - _lastLightAttackResolveTime < Mathf.Max(0.01f, lightAttackDuration * 0.5f))
            return;
        _lastLightAttackResolveTime = Time.time;
        _lightAttackInProgress = false;
        _lightFallbackExecuteAt = -1f;
    }

    private void HandleLightImpactFallback()
    {
        if (!_lightAttackInProgress) return;
        if (Time.time < _lightFallbackExecuteAt) return;
        ClearLightAttackPendingState();
    }

    private void HandleHeavyImpactFallback()
    {
        if (!_heavyAttackInProgress) return;
        if (Time.time < _heavyFallbackExecuteAt) return;
        HammerSlam();
    }

    private void TriggerHeavyAttack(float chargeFraction)
    {
        _pendingChargeFraction = chargeFraction;
        if (animator != null)
            animator.SetTrigger(Animator.StringToHash("HeavyAttack"));
        _heavyAttackInProgress = true;
        _heavyFallbackExecuteAt = Time.time + Mathf.Max(0.05f, heavyImpactFallbackDelay);
    }

    public void HammerSlam()
    {
        if (Time.time - _lastHeavyResolveTime < 0.05f) return;

        _lastHeavyResolveTime = Time.time;
        _heavyAttackInProgress = false;
        _heavyFallbackExecuteAt = -1f;

        PlaySFX(hammerAttackSFX, hammerAttackSFXPitch, hammerAttackSFXVolume);

        if (_defaultImpulseSource != null) _defaultImpulseSource.GenerateImpulse();
        float effectiveHammerAoe = hammerAOE * (playerAugmentController != null ? playerAugmentController.HammerAoeRadiusMultiplier : 1f);
        float hammerFreezeDuration = playerAugmentController != null ? playerAugmentController.HammerFreezeDuration : 0f;

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, effectiveHammerAoe, enemyLayers);
        int successfulHits = 0;
        Vector3 firstHitPosition = attackPoint.position;
        foreach (Collider2D enemy in hitEnemies)
        {
            IDamageable target = enemy.GetComponent<IDamageable>() ?? enemy.GetComponentInParent<IDamageable>();
            if (target == null) continue;
            BaseEntity targetEntity = enemy.GetComponent<BaseEntity>() ?? enemy.GetComponentInParent<BaseEntity>();
            float hammerMult      = playerAugmentController != null ? playerAugmentController.HammerDamageMultiplier : 1f;
            float gemTierMult     = playerAugmentController != null ? playerAugmentController.HammerGemTierDamageMultiplier : 1f;
            float baseDamageBonus = playerAugmentController != null ? playerAugmentController.PlayerBaseDamageBonus : 0f;
            float heavyDamage     = ((stats != null ? stats.RollPlayerBaseDamage() : 0f) + baseDamageBonus) * hammerMult * gemTierMult * Mathf.Clamp01(_pendingChargeFraction);
            float dmgMult = playerAugmentController != null ? playerAugmentController.OutgoingDamageMultiplier : 1f;
            bool guaranteedCrit = playerAugmentController != null
                && playerAugmentController.HasHammerGuaranteedCritOnFullCharge
                && _pendingChargeFraction >= 0.999f;
            bool isCrit = guaranteedCrit
                || (playerAugmentController != null && UnityEngine.Random.value < playerAugmentController.CritChance);
            float critMult = isCrit ? playerAugmentController.CritDamage : 1f;
            float finalDamage = heavyDamage * dmgMult * critMult;
            target.TakeDamage(finalDamage, true);

            if (playerAugmentController != null && playerAugmentController.HasHammerLifestealUnlock)
                Heal(finalDamage * playerAugmentController.HammerLifestealRatio);

            Enemy enemyComp = (targetEntity as Enemy) ?? enemy.GetComponentInParent<Enemy>();
            if (enemyComp != null && targetEntity != null && targetEntity.CurrentHealth > 0f)
            {
                if (hammerFreezeDuration > 0f)
                    enemyComp.Freeze(hammerFreezeDuration);
                if (playerAugmentController != null && playerAugmentController.HasHammerBleedUnlock)
                    enemyComp.ApplyBleedStack(
                        finalDamage * playerAugmentController.HammerBleedDamageRatioPerStack,
                        playerAugmentController.HammerBleedMaxStacks,
                        playerAugmentController.HammerBleedExpireSeconds);
            }
            if (successfulHits == 0)
                firstHitPosition = enemy.ClosestPoint(attackPoint.position);
            successfulHits++;
        }

        if (successfulHits > 0)
            impactFeedback?.PlayHeavyHit(firstHitPosition, _defaultImpulseSource);
    }

    // ─── Hammer Charge Magnet ────────────────────────────────────────────────

    private void UpdateHammerMagnet()
    {
        if (playerAugmentController == null || !playerAugmentController.HasHammerMagnetUnlock) return;
        if (!(_currentState is HammerState) && !(_currentState is GreatHammerState)) return;

        bool isCharging   = _currentState.IsChargingForMovement;
        bool isChargeFull = _currentState.IsChargeMeterFull(this);

        if (isCharging && !isChargeFull)
        {
            float radius    = playerAugmentController.HammerMagnetRadius;
            float pullSpeed = playerAugmentController.HammerMagnetPullSpeed;
            Vector2 playerPos = transform.position;

            Collider2D[] nearby = Physics2D.OverlapCircleAll(playerPos, radius, enemyLayers);
            foreach (Collider2D col in nearby)
            {
                Enemy e = col.GetComponent<Enemy>() ?? col.GetComponentInParent<Enemy>();
                if (e == null || e.IsDead) continue;
                e.SetMagnetPull(playerPos, pullSpeed);
            }
        }

        if (isChargeFull && !_prevChargeFullState)
        {
            float radius       = playerAugmentController.HammerMagnetRadius;
            float stopDuration = playerAugmentController.HammerChargeFullStopDuration;
            Vector2 playerPos  = transform.position;

            Collider2D[] nearby = Physics2D.OverlapCircleAll(playerPos, radius, enemyLayers);
            foreach (Collider2D col in nearby)
            {
                Enemy e = col.GetComponent<Enemy>() ?? col.GetComponentInParent<Enemy>();
                if (e == null || e.IsDead) continue;
                e.Freeze(stopDuration);
            }
        }

        _prevChargeFullState = isChargeFull;
    }

    // ─── Gizmos ──────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        // Hammer attack — radial circle (red)
        float effectiveHeavyAoe = hammerAOE * (playerAugmentController != null ? playerAugmentController.HammerAoeRadiusMultiplier : 1f);
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.15f);
        Gizmos.DrawSphere(attackPoint.position, effectiveHeavyAoe);
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 1f);
        Gizmos.DrawWireSphere(attackPoint.position, effectiveHeavyAoe);
    }
}
