using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public partial class Player
{
    [Header("Longbow / Arrow")]
    [Tooltip("Arrow prefab with a PlayerArrow component.")]
    public GameObject arrowPrefab;
    public float arrowSpeed = 14f;
    public float arrowMaxLifetime = 8f;
    [Tooltip("If empty, Camera.main is used for cursor world position.")]
    public Camera aimCamera;
    [Tooltip("Arrow is instantiated after the bow animation finishes.")]
    public float longbowArrowReleaseDelay = 0.4f;
    [Tooltip("Arrow SPEED on a fully charged right-click shot is multiplied by this. Damage comes from EntityStats.bowHeavyAp.")]
    public float longbowChargedSpeedDamageMultiplier = 3f;
    [Tooltip("Hold duration (seconds) before the bow is considered fully charged.")]
    public float maxLongbowChargeTime = 0.5f;
    [Tooltip("If empty, the bow charge bar is not shown.")]
    public Slider longbowChargeMeter;
    [Tooltip("If empty, the bow charge UI canvas does not open.")]
    public GameObject longbowMeterCanvas;

    [Header("Longbow radial mutation (auto)")]
    [Tooltip("Seconds between automatic volleys once the Obsidian gem tier is reached with 6 longbow augments.")]
    [SerializeField] private float radialLongbowAutoVolleyIntervalSeconds = 1f;
    [SerializeField] private int autoArrowVolleyCount = 8;
    [SerializeField] private float autoArrowVolleyAngleStepDegrees = 45f;
    [SerializeField] private float radialLongbowSpawnInset = 0.2f;
    [Tooltip("Travel distance for arrows in automatic volley mode (no cursor).")]
    [SerializeField] private float radialLongbowAutoVolleyTravelDistance = 8f;

    private bool _hadRadialLongbowMutationLastFrame;
    private float _nextRadialLongbowAutoVolleyTime;

    // ─── Longbow / arrow ──────────────────────────────────────────────────────

    private Vector2 GetLongbowAimWorldPointAtCurrentMouse()
    {
        if (attackPoint == null) return Vector2.zero;
        Camera cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null) return attackPoint.position;

        Vector3 mouse = Input.mousePosition;
        float planeZ = attackPoint.position.z;
        mouse.z = Mathf.Abs(cam.transform.position.z - planeZ);
        return cam.ScreenToWorldPoint(mouse);
    }

    private void ScheduleLongbowArrow(float damage, bool useBowChargedMultiplier, Vector2 aimWorldAtFireInput)
    {
        // Lock attackPoint position at fire time so direction stays correct after delay.
        Vector2 fireOrigin = attackPoint != null ? (Vector2)attackPoint.position : Vector2.zero;
        float delay = Mathf.Max(0f, longbowArrowReleaseDelay);
        if (delay <= 0f)
        {
            SpawnArrowTowardWorld(damage, useBowChargedMultiplier, aimWorldAtFireInput, fireOrigin);
            return;
        }
        StartCoroutine(LongbowArrowSpawnAfterDelay(delay, damage, useBowChargedMultiplier, aimWorldAtFireInput, fireOrigin));
    }

    private IEnumerator LongbowArrowSpawnAfterDelay(float delaySeconds, float damage, bool useBowChargedMultiplier, Vector2 aimWorldAtFireInput, Vector2 fireOrigin)
    {
        yield return new WaitForSeconds(delaySeconds);
        SpawnArrowTowardWorld(damage, useBowChargedMultiplier, aimWorldAtFireInput, fireOrigin);
    }

    private void SpawnArrowTowardWorld(float damage, bool useBowChargedMultiplier, Vector2 targetWorld, Vector2 directionOrigin)
    {
        if (arrowPrefab == null || attackPoint == null) return;

        bool chargedExplosionEnabled = useBowChargedMultiplier &&
                                       playerAugmentController != null &&
                                       playerAugmentController.HasChargedLongbowAoe;
        float spdMult      = useBowChargedMultiplier ? Mathf.Max(1f, longbowChargedSpeedDamageMultiplier) : 1f;
        float dmgMult      = playerAugmentController != null ? playerAugmentController.OutgoingDamageMultiplier : 1f;
        float arrowSpdMult = playerAugmentController != null ? playerAugmentController.ArrowProjectileSpeedMultiplier : 1f;
        float useSpeed     = arrowSpeed * spdMult * arrowSpdMult;
        float useDamage    = damage * dmgMult;

        Vector2 origin = attackPoint.position;
        // Direction is calculated from fire-time origin to avoid flip/movement errors during delay.
        Vector2 offset = targetWorld - directionOrigin;
        if (offset.sqrMagnitude < 0.0001f) offset = Vector2.right * 0.01f;
        Vector2 dir = offset.normalized;
        float targetDistance = Mathf.Max(0.5f, offset.magnitude);
        int arrowCount = playerAugmentController != null ? Mathf.Max(1, playerAugmentController.ProjectileShotMultiplier) : 1;
        float spreadStepDegrees = GetArrowSpreadStepDegrees(arrowCount);
        float centerOffset = (arrowCount - 1) * 0.5f;

        for (int i = 0; i < arrowCount; i++)
        {
            float angleOffset = (i - centerOffset) * spreadStepDegrees;
            Vector2 shotDir = Quaternion.Euler(0f, 0f, angleOffset) * dir;
            Vector2 spawnPos = origin + shotDir * radialLongbowSpawnInset;
            TrySpawnSinglePlayerArrow(spawnPos, origin + shotDir * targetDistance, useSpeed, useDamage, chargedExplosionEnabled);
        }
    }

    private void UpdateRadialLongbowAutoVolley()
    {
        if (arrowPrefab == null) return;

        bool active = playerAugmentController != null &&
                      playerAugmentController.ShouldUseRadialLongbowVolleyMutation(this);
        if (!active)
        {
            _hadRadialLongbowMutationLastFrame = false;
            return;
        }

        if (!_hadRadialLongbowMutationLastFrame)
            _nextRadialLongbowAutoVolleyTime = Time.time;

        _hadRadialLongbowMutationLastFrame = true;

        if (Time.time < _nextRadialLongbowAutoVolleyTime) return;

        float baseDamage = stats != null ? stats.bowLightAp : 0f;
        FireRadialLongbowMutationAutoVolley(baseDamage);
        _nextRadialLongbowAutoVolleyTime = Time.time + Mathf.Max(0.05f, radialLongbowAutoVolleyIntervalSeconds);
    }

    private void FireRadialLongbowMutationAutoVolley(float lightDamage)
    {
        float dmgMult      = playerAugmentController != null ? playerAugmentController.OutgoingDamageMultiplier : 1f;
        float arrowSpdMult = playerAugmentController != null ? playerAugmentController.ArrowProjectileSpeedMultiplier : 1f;
        float useSpeed     = arrowSpeed * arrowSpdMult;
        float useDamage    = lightDamage * dmgMult;
        bool chargedExplosion = playerAugmentController != null && playerAugmentController.HasChargedLongbowAoe;

        Vector2 radialOrigin = transform.position;
        float targetDistance = Mathf.Max(0.5f, radialLongbowAutoVolleyTravelDistance);
        int volleyCount = Mathf.Clamp(autoArrowVolleyCount, 1, 64);
        float step = Mathf.Max(1f, autoArrowVolleyAngleStepDegrees);

        for (int i = 0; i < volleyCount; i++)
        {
            Vector2 radialDir = RadialBowDirFromAngleDegrees(i * step);
            float forward = Mathf.Max(0f, radialLongbowSpawnInset);
            Vector2 spawnPos = radialOrigin + radialDir * forward;
            Vector2 shotTarget = radialOrigin + radialDir * Mathf.Max(forward + 0.3f, targetDistance);
            TrySpawnSinglePlayerArrow(spawnPos, shotTarget, useSpeed, useDamage, chargedExplosion);
        }
    }

    private static Vector2 RadialBowDirFromAngleDegrees(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
    }

    private void TrySpawnSinglePlayerArrow(
        Vector2 spawnWorldPosition,
        Vector2 targetWorldPoint,
        float useSpeed,
        float useDamage,
        bool chargedExplosionEnabled)
    {
        float explosionRadius = chargedExplosionEnabled && playerAugmentController != null
            ? playerAugmentController.ChargedLongbowAoeRadius
            : 0f;

        PlayerArrow mover = null;
        if (PlayerArrowPooler.Instance != null)
            PlayerArrowPooler.Instance.GetArrow(spawnWorldPosition, Quaternion.identity, a => mover = a);

        if (mover == null)
        {
            if (arrowPrefab == null) return;
            GameObject go = Instantiate(arrowPrefab, spawnWorldPosition, Quaternion.identity);
            mover = go.GetComponent<PlayerArrow>();
            if (mover == null) { Destroy(go); return; }
        }

        mover.transform.localScale = (playerAugmentController != null && playerAugmentController.HasArrowSizeUnlock)
            ? Vector3.one * 2f
            : Vector3.one;

        mover.Initialize(
            targetWorldPoint,
            useSpeed,
            useDamage,
            arrowMaxLifetime,
            enemyLayers,
            transform,
            chargedExplosionEnabled,
            explosionRadius,
            chargedExplosionEnabled ? _defaultImpulseSource : null,
            playerAugmentController != null ? playerAugmentController.LongbowFreezeDuration : 0f,
            playerAugmentController != null && playerAugmentController.HasLongbowFreezeUnlock,
            playerAugmentController != null && playerAugmentController.HasFireArrowUnlock,
            playerAugmentController != null ? playerAugmentController.FireDotDuration : 0f,
            playerAugmentController != null ? playerAugmentController.FireDotDamagePerSecond : 0f,
            playerAugmentController != null && playerAugmentController.HasPoisonArrowUnlock,
            playerAugmentController != null ? playerAugmentController.PoisonDotDuration : 0f,
            playerAugmentController != null ? playerAugmentController.PoisonDotDamagePerSecond : 0f,
            playerAugmentController != null ? playerAugmentController.FrozenEnemyVulnerabilityMultiplier : 1f,
            playerAugmentController != null && playerAugmentController.HasVampiricArrowUnlock);
    }

    private static float GetArrowSpreadStepDegrees(int arrowCount)
    {
        switch (arrowCount)
        {
            case 2: return 15f;
            case 3: return 10f;
            case 4: return 8f;
            default: return arrowCount > 4 ? 8f : 0f;
        }
    }

    private void CancelPendingLongbowArrow()
    {
        StopAllCoroutines();
    }
}
