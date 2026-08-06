using UnityEngine;
using System.Collections;

public partial class Player
{
    [Header("─── CROSSBOW ───────────────────────────")]
    [Tooltip("Crossbow bolt prefab (must have PlayerBolt component).")]
    public GameObject crossbowBoltPrefab;

    [Header("Crossbow — Fire")]
    [Tooltip("Time between shots (seconds). Recommended to match animation duration.")]
    public float crossbowAttackRate = 0.5f;
    [Tooltip("Delay from trigger to bolt spawn (seconds). Typically: animationDuration - 0.2")]
    public float crossbowBoltReleaseDelay = 0.3f;

    [Header("Crossbow — Bolt Stats")]
    [Tooltip("Bolt speed = arrowSpeed x this multiplier.")]
    public float crossbowBoltSpeedMultiplier = 2f;
    [Tooltip("Maximum lifetime of a bolt in the scene (seconds).")]
    public float crossbowBoltMaxLifetime = 5f;

    // ─── Crossbow / bolt ─────────────────────────────────────────────────────

    private void ScheduleCrossbowBolt(Vector2 aimWorld)
    {
        // Lock attackPoint position at fire time so direction stays correct after delay.
        Vector2 fireOrigin = attackPoint != null ? (Vector2)attackPoint.position : Vector2.zero;
        float delay = Mathf.Max(0f, crossbowBoltReleaseDelay);
        if (delay <= 0f) { SpawnCrossbowBolt(aimWorld, fireOrigin); return; }
        StartCoroutine(CrossbowBoltSpawnAfterDelay(delay, aimWorld, fireOrigin));
    }

    private IEnumerator CrossbowBoltSpawnAfterDelay(float delay, Vector2 aimWorld, Vector2 fireOrigin)
    {
        yield return new WaitForSeconds(delay);
        SpawnCrossbowBolt(aimWorld, fireOrigin);
    }

    private void SpawnCrossbowBolt(Vector2 aimWorld, Vector2 directionOrigin)
    {
        if (attackPoint == null) return;

        PlayerAugmentController aug = playerAugmentController;
        float dmgMult  = aug != null ? aug.OutgoingDamageMultiplier : 1f;
        float useSpeed = arrowSpeed * crossbowBoltSpeedMultiplier;

        Vector2 origin = attackPoint.position;
        // Direction calculated from fire-time origin to avoid flip/movement errors during delay.
        Vector2 offset = aimWorld - directionOrigin;
        if (offset.sqrMagnitude < 0.0001f) offset = Vector2.right * 0.01f;
        Vector2 dir = offset.normalized;
        float targetDistance = Mathf.Max(0.5f, offset.magnitude);
        int boltCount = aug != null ? Mathf.Max(1, aug.ProjectileShotMultiplier) : 1;
        float spreadStepDegrees = GetArrowSpreadStepDegrees(boltCount);
        float centerOffset = (boltCount - 1) * 0.5f;

        for (int i = 0; i < boltCount; i++)
        {
            float angleOffset = (i - centerOffset) * spreadStepDegrees;
            Vector2 shotDir = Quaternion.Euler(0f, 0f, angleOffset) * dir;
            // Her civata kendi hasarını ayrı ayrı roll eder (min-max aralık).
            TrySpawnSingleCrossbowBolt(origin, origin + shotDir * targetDistance, useSpeed, dmgMult);
        }
    }

    private void TrySpawnSingleCrossbowBolt(Vector2 origin, Vector2 aimWorld, float useSpeed, float dmgMult)
    {
        PlayerAugmentController aug = playerAugmentController;
        float bowMult         = aug != null ? aug.BowDamageMultiplier : 1f;
        float baseDamageBonus = aug != null ? aug.PlayerBaseDamageBonus : 0f;
        float useDamage       = ((stats != null ? stats.RollPlayerBaseDamage() : 0f) + baseDamageBonus) * bowMult * dmgMult;
        PlayerBolt bolt = null;
        if (PlayerArrowPooler.Instance != null)
            PlayerArrowPooler.Instance.GetBolt(origin, Quaternion.identity, b => bolt = b);

        if (bolt == null && crossbowBoltPrefab != null)
        {
            GameObject boltGO = Instantiate(crossbowBoltPrefab, origin, Quaternion.identity);
            bolt = boltGO.GetComponent<PlayerBolt>();
            if (bolt == null) { Destroy(boltGO); return; }
        }

        if (bolt == null) return;

        bolt.Initialize(
            aimWorld,
            useSpeed,
            useDamage,
            crossbowBoltMaxLifetime,
            enemyLayers,
            transform,
            hasPierce:                aug != null && aug.HasCrossbowBoltPierce,
            pierceFalloff:            aug != null ? aug.CrossbowPierceDamageFalloff    : 0.20f,
            pierceFloor:              aug != null ? aug.CrossbowPierceDamageFloor      : 0.30f,
            pierceFalloffCount:       aug != null ? aug.CrossbowPierceFalloffCount     : 3,
            hasBleed:                 aug != null && aug.HasCrossbowBoltBleed,
            bleedDamageRatioPerStack: aug != null ? aug.CrossbowBleedDamageRatioPerStack : 0.01f,
            bleedMaxStacks:           aug != null ? aug.CrossbowBleedMaxStacks          : 5,
            bleedExpireSeconds:       aug != null ? aug.CrossbowBleedExpireSeconds      : 5f,
            critChance:               aug != null ? aug.CritChance                      : 0f,
            critDamage:               aug != null ? aug.CritDamage                      : 1f
        );
    }
}
