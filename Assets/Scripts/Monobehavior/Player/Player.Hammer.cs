using UnityEngine;
using UnityEngine.UI;

public partial class Player
{
    [Header("Hammer Settings (Heavy)")]
    public float maxChargeTime = 1.5f;
    public float hammerAOE = 2.5f;
    public float hammerCooldown = 3f;
    [SerializeField] private float heavyImpactFallbackDelay = 0.2f;
    [Tooltip("Hold duration (seconds) before charge mode activates. Releasing before this threshold triggers a light attack.")]
    [SerializeField] private float hammerChargeStartDelay = 0.5f;

    [Header("Hammer Settings (Light)")]
    [Tooltip("Minimum time between light hammer attacks (seconds).")]
    [SerializeField] private float hammerLightAttackRate = 0.4f;
    [Tooltip("Light hammer salvo radius. Usually half the heavy hammer radius.")]
    [SerializeField] private float hammerLightAoe;
    [Tooltip("Fallback trigger delay if no animation event is received (seconds).")]
    [SerializeField] private float hammerLightFallbackDelay = 0.15f;

    [Header("Light Attack Settings (Spammable)")]
    public float lightAttackRate = 0.2f;
    public float lightAttackDuration = 0.1f;
    [SerializeField] private float lightImpactFallbackDelay = 0.08f;
    private float _nextAttackTime = 0f;
    private float _lastLightAttackResolveTime = -999f;
    private bool _lightAttackInProgress = false;
    private float _lightFallbackExecuteAt = -1f;
    private float _lastHeavyResolveTime = -999f;
    private bool _heavyAttackInProgress = false;
    private float _heavyFallbackExecuteAt = -1f;

    [Header("Hammer Cooldown UI")]
    public Slider hammerCooldownBar;
    public GameObject hammerCooldownCanvas;
    [SerializeField] private bool showCooldownBarWhenReady = true;

    [Header("Hammer Charge UI")]
    public Slider chargeMeter;
    public GameObject meterCanvas;

    private float _nextHammerUseTime = 0f;
    private bool  _hammerLightInProgress = false;
    private bool  _prevChargeFullState = false;
    private float _hammerLightFallbackAt = -1f;
    private float _lastHammerLightResolveTime = -999f;

    private static readonly int HammerLightAttackHash = Animator.StringToHash("HammerLightAttack");

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

    private void TriggerHeavyAttack()
    {
        float slamCooldownMult = playerAugmentController != null ? playerAugmentController.HammerSlamCooldownMultiplier : 1f;
        _nextHammerUseTime = Time.time + hammerCooldown * slamCooldownMult;
        UpdateHammerCooldownUI();
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

        if (_defaultImpulseSource != null) _defaultImpulseSource.GenerateImpulse();
        float effectiveHammerAoe = hammerAOE * (playerAugmentController != null ? playerAugmentController.HammerAoeRadiusMultiplier : 1f);
        float hammerFreezeDuration = playerAugmentController != null ? playerAugmentController.HammerFreezeDuration : 0f;

        if (generator != null)
        {
            var brokenWalls = generator.BreakWallsInArea(attackPoint.position, effectiveHammerAoe);
            wallLootHandler?.TrySpawnWallLootForBrokenWalls(brokenWalls);
        }

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, effectiveHammerAoe, enemyLayers);
        int successfulHits = 0;
        Vector3 firstHitPosition = attackPoint.position;
        foreach (Collider2D enemy in hitEnemies)
        {
            IDamageable target = enemy.GetComponent<IDamageable>() ?? enemy.GetComponentInParent<IDamageable>();
            if (target == null) continue;
            BaseEntity targetEntity = enemy.GetComponent<BaseEntity>() ?? enemy.GetComponentInParent<BaseEntity>();
            float heavyDamage = targetEntity != null ? targetEntity.CurrentHealth : stats.heavyAttackDamage;
            float dmgMult = playerAugmentController != null ? playerAugmentController.OutgoingDamageMultiplier : 1f;
            target.TakeDamage(heavyDamage * dmgMult, true);
            if (hammerFreezeDuration > 0f && targetEntity != null && targetEntity.CurrentHealth > 0f)
            {
                Enemy enemyComp = targetEntity as Enemy ?? enemy.GetComponentInParent<Enemy>();
                if (enemyComp != null)
                    enemyComp.Freeze(hammerFreezeDuration);
            }
            if (successfulHits == 0)
                firstHitPosition = enemy.ClosestPoint(attackPoint.position);
            successfulHits++;
        }

        if (successfulHits > 0)
            impactFeedback?.PlayHeavyHit(firstHitPosition, _defaultImpulseSource);
    }

    // ─── Hammer Light Attack ──────────────────────────────────────────────────

    private void TriggerHammerLightAttack()
    {
        if (Time.time < _nextAttackTime) return;
        float lightRateMult = playerAugmentController != null ? playerAugmentController.HammerLightRateMultiplier : 1f;
        float effectiveRate = Mathf.Max(0.05f, hammerLightAttackRate * lightRateMult);
        _nextAttackTime = Time.time + effectiveRate;

        if (animator != null)
            animator.CrossFade(HammerLightAttackHash, 0.05f, 0, 0f);

        _hammerLightInProgress = true;
        _hammerLightFallbackAt = Time.time + Mathf.Max(0.05f, hammerLightFallbackDelay);
    }

    private void HandleHammerLightFallback()
    {
        if (!_hammerLightInProgress) return;
        if (Time.time < _hammerLightFallbackAt) return;
        HammerLightSlam();
    }

    public void HammerLightSlam()
    {
        if (Time.time - _lastHammerLightResolveTime < 0.05f) return;
        _lastHammerLightResolveTime = Time.time;
        _hammerLightInProgress = false;
        _hammerLightFallbackAt = -1f;

        if (attackPoint == null) return;

        Vector2 facing    = transform.localScale.x >= 0f ? Vector2.right : Vector2.left;
        Vector2 boxSize   = new Vector2(hammerLightAoe * 2f, hammerLightAoe);
        Vector2 boxCenter = (Vector2)attackPoint.position + facing * hammerLightAoe;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(boxCenter, boxSize, 0f, facing, 0f, enemyLayers);
        if (hits.Length == 0) return;

        float lightDmg     = stats != null ? stats.hammerLightDamage : 0f;
        float dmgMult      = playerAugmentController != null ? playerAugmentController.OutgoingDamageMultiplier : 1f;
        float lightDmgMult = playerAugmentController != null ? playerAugmentController.HammerLightDamageMultiplier : 1f;
        float useDamage    = lightDmg * dmgMult * lightDmgMult;

        Vector2 firstHitPoint = attackPoint.position;
        bool anyHit = false;

        foreach (RaycastHit2D hit in hits)
        {
            IDamageable target = hit.collider.GetComponent<IDamageable>() ?? hit.collider.GetComponentInParent<IDamageable>();
            if (target == null) continue;
            target.TakeDamage(useDamage, false);
            if (!anyHit)
            {
                firstHitPoint = hit.collider.ClosestPoint(attackPoint.position);
                anyHit = true;
            }
        }

        if (anyHit)
            impactFeedback?.PlayLightHit(firstHitPoint, _defaultImpulseSource);
    }

    // ─── Hammer Charge Magnet ────────────────────────────────────────────────

    private void UpdateHammerMagnet()
    {
        if (playerAugmentController == null || !playerAugmentController.HasHammerChargeUnlock) return;
        if (!(_currentState is HammerState)) return;

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

    // ─── UI ──────────────────────────────────────────────────────────────────

    private void InitializeHammerCooldownUI()
    {
        if (hammerCooldownBar == null) return;
        hammerCooldownBar.minValue = 0f;
        hammerCooldownBar.maxValue = 1f;
        hammerCooldownBar.value = 1f;
        if (hammerCooldownCanvas != null)
            hammerCooldownCanvas.SetActive(showCooldownBarWhenReady);
    }

    private void UpdateHammerCooldownUI()
    {
        if (hammerCooldownBar == null) return;
        float slamCooldownMult = playerAugmentController != null ? playerAugmentController.HammerSlamCooldownMultiplier : 1f;
        float cooldown = Mathf.Max(0f, hammerCooldown * slamCooldownMult);
        if (cooldown <= 0f)
        {
            hammerCooldownBar.value = 1f;
            if (hammerCooldownCanvas != null) hammerCooldownCanvas.SetActive(showCooldownBarWhenReady);
            return;
        }
        float remaining = Mathf.Max(0f, _nextHammerUseTime - Time.time);
        hammerCooldownBar.value = Mathf.Clamp01(1f - (remaining / cooldown));
        if (hammerCooldownCanvas != null)
        {
            bool isReady = remaining <= 0f;
            hammerCooldownCanvas.SetActive(!isReady || showCooldownBarWhenReady);
        }
    }

    // ─── Gizmos ──────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        Vector2 facing    = transform.localScale.x >= 0f ? Vector2.right : Vector2.left;
        Vector2 boxSize   = new Vector2(hammerLightAoe * 2f, hammerLightAoe);
        Vector2 boxCenter = (Vector2)attackPoint.position + facing * hammerLightAoe;

        // Light attack — directional box (orange)
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.25f);
        Gizmos.DrawCube(boxCenter, boxSize);
        Gizmos.color = new Color(1f, 0.6f, 0f, 1f);
        Gizmos.DrawWireCube(boxCenter, boxSize);

        // Heavy slam — radial circle (red)
        float effectiveHeavyAoe = hammerAOE * (playerAugmentController != null ? playerAugmentController.HammerAoeRadiusMultiplier : 1f);
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.15f);
        Gizmos.DrawSphere(attackPoint.position, effectiveHeavyAoe);
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 1f);
        Gizmos.DrawWireSphere(attackPoint.position, effectiveHeavyAoe);
    }
}
