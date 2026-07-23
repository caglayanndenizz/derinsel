using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAugmentController : MonoBehaviour
{
    // ── Gem Tier System ───────────────────────────────────────────────────────

    public const int GemCoalThreshold     = 1;
    public const int GemGoldThreshold     = 2;
    public const int GemDiamondThreshold  = 4;
    public const int GemObsidianThreshold = 6;

    private static readonly AugmentId[] LongbowGemAugmentIds =
    {
        AugmentId.ChargedLongbowAoeUnlock,
        AugmentId.TripleArrowUnlock,
        AugmentId.LongbowFreezeUnlock,
        AugmentId.FireArrowUnlock,
        AugmentId.PoisonArrowUnlock,
        AugmentId.LongbowAoeRadius_Common,
        AugmentId.LongbowAoeRadius_Rare,
        AugmentId.LongbowAoeRadius_Extraordinary,
        AugmentId.ProjectileCount_ExtraArrow,
        AugmentId.ProjectileCount_PlusOneProjectiles,
        AugmentId.ProjectileCount_PlusOneAndSpeed10Percent,
        AugmentId.ProjectileCount_PlusOneAndSpeed15Percent,
        AugmentId.ProjectileCount_ExtraArrowPlus,
    };

    // ── Runtime stats ─────────────────────────────────────────────────────────

    [Header("General")]
    [SerializeField] private float movementSpeedBonus;
    [SerializeField] private float luckMultiplier          = 1f;
    [SerializeField] private float incomingDamageReduction = 0f;
    [SerializeField] private float outgoingDamageMultiplier       = 1f;
    [SerializeField] private float maxHealthMultiplier            = 1f;
    [SerializeField] private float flatMaxHealthBonus = 0f;
    [SerializeField] private bool  hasExtraAugmentSlotUnlock;

    [Header("Hammer — Unlock")]
    [SerializeField] private bool  hasHammerChargeUnlock;
    [SerializeField] private bool  hasHammerChargeDamageReductionUnlock;
    [SerializeField] private float hammerChargeMultiplier  = 1f;
    [SerializeField] private float hammerAoeRadiusBonus    = 0f;
    [SerializeField] private float hammerFreezeDuration    = 0f;
    [SerializeField] private float hammerSlamCooldownMultiplier = 1f;

    [Header("Hammer — Light Attack")]
    [SerializeField] private float hammerLightDamageMultiplier  = 1f;
    [SerializeField] private float hammerLightRateMultiplier    = 1f;

    [Header("Hammer — Charge Magnet")]
    [Tooltip("Magnet pull radius during charge (units).")]
    [SerializeField] private float hammerMagnetRadius = 6f;
    [Tooltip("Magnet pull speed (units/second).")]
    [SerializeField] private float hammerMagnetPullSpeed = 7f;
    [Tooltip("Freeze duration applied to enemies when charge is full (seconds).")]
    [SerializeField] private float hammerChargeFullStopDuration = 1.2f;

    [Header("Longbow — Unlock")]
    [SerializeField] private bool  hasChargedLongbowAoe;
    [SerializeField] private float chargedLongbowAoeRadius = 3f;
    [SerializeField] private bool  hasTripleArrowUnlock;
    [SerializeField] private float longbowAoeRadiusBonus   = 0f;
    [SerializeField] private bool  hasArrowSizeUnlock;
    [SerializeField] private bool  hasVampiricArrowUnlock;
    [SerializeField] private int   projectileShotBonusCount;
    [SerializeField] private float arrowProjectileSpeedMultiplier = 1f;

    [Header("Longbow — Freeze")]
    [SerializeField] private bool  hasLongbowFreezeUnlock;
    [SerializeField] private float longbowFreezeDuration   = 0f;
    [Tooltip("Resonance — multiplied onto damage dealt to frozen enemies. Set to 1.5 when LongbowFreezeUnlock is obtained.")]
    [SerializeField] private float frozenEnemyVulnerabilityMultiplier = 1f;

    [Header("Longbow — Fire Arrow")]
    [SerializeField] private bool  hasFireArrowUnlock;
    [SerializeField] private float fireDotDuration         = 3f;
    [SerializeField] private float fireDotDamagePerSecond  = 2f;

    [Header("Longbow — Poison Arrow")]
    [SerializeField] private bool  hasPoisonArrowUnlock;
    [SerializeField] private float poisonDotDuration        = 5f;
    [SerializeField] private float poisonDotDamagePerSecond = 1.5f;

    [Header("Crossbow — Pierce")]
    [SerializeField] private bool  hasCrossbowBoltPierce;
    [Tooltip("Damage reduction per enemy pierced (0.20 = 20%).")]
    [SerializeField] private float crossbowPierceDamageFalloff = 0.20f;
    [Tooltip("Minimum damage multiplier for pierce (0.30 = 30%).")]
    [SerializeField] private float crossbowPierceDamageFloor   = 0.30f;
    [Tooltip("Number of enemies before damage drops to floor (default 3 → 30% from the 4th enemy onward).")]
    [SerializeField] private int   crossbowPierceFalloffCount  = 3;

    [Header("Crossbow — Bleed")]
    [SerializeField] private bool  hasCrossbowBoltBleed;
    [Tooltip("Damage per stack as a ratio of bolt damage (0.01 = 1%). Total at max stacks = maxStacks * ratio.")]
    [SerializeField] private float crossbowBleedDamageRatioPerStack = 0.01f;
    [Tooltip("Maximum stack count (default 5 → total 5%).")]
    [SerializeField] private int   crossbowBleedMaxStacks           = 5;
    [Tooltip("Bleed expires if this many seconds pass since the last hit (seconds).")]
    [SerializeField] private float crossbowBleedExpireSeconds       = 5f;

    private float _initialChargedLongbowAoeRadius;
    private readonly Dictionary<AugmentId, int> _appliedAugmentCounts = new();

    [Header("Unlock Augment Database")]
    [Tooltip("Tracks unlock augments per weapon. Used for mutation checks.")]
    [SerializeField] private UnlockAugmentDatabase unlockDatabase;

    [Header("Test")]
    [Tooltip("When checked, triggers the Obsidian mutation as if 6 longbow augments were obtained.")]
    [SerializeField] private bool mutationAugmentsLongbow;

    // Weapon mutation flags — set automatically when all unlock augments for a weapon are obtained
    private bool _longbowMutated;
    private bool _crossbowMutated;
    private bool _hammerMutated;

    [Header("Debug (Updated in Play Mode)")]
    [SerializeField] private int      longbowGemCountDebug;
    [SerializeField] private string   longbowGemTierDebug;

    private Player _player;

    public event Action<AugmentDefinition> AugmentApplied;

    // ── Properties ────────────────────────────────────────────────────────────

    public float MovementSpeedBonus          => Mathf.Max(0f, movementSpeedBonus);
    public bool  HasChargedLongbowAoe            => hasChargedLongbowAoe   || mutationAugmentsLongbow;
    public bool  HasLongbowFreezeUnlock          => hasLongbowFreezeUnlock  || mutationAugmentsLongbow;
    public bool  HasFireArrowUnlock          => hasFireArrowUnlock  || mutationAugmentsLongbow;
    public bool  HasPoisonArrowUnlock        => hasPoisonArrowUnlock || mutationAugmentsLongbow;
    public bool  HasArrowSizeUnlock          => hasArrowSizeUnlock;
    public bool  HasVampiricArrowUnlock      => hasVampiricArrowUnlock;
    public float FireDotDuration             => Mathf.Max(0f, fireDotDuration);
    public float FireDotDamagePerSecond      => Mathf.Max(0f, fireDotDamagePerSecond);
    public float PoisonDotDuration           => Mathf.Max(0f, poisonDotDuration);
    public float PoisonDotDamagePerSecond    => Mathf.Max(0f, poisonDotDamagePerSecond);
    public float LongbowFreezeDuration           => (mutationAugmentsLongbow && longbowFreezeDuration <= 0f) ? 1.5f : Mathf.Max(0f, longbowFreezeDuration);
    public bool  HasCrossbowBoltPierce           => hasCrossbowBoltPierce;
    public float CrossbowPierceDamageFalloff     => Mathf.Clamp01(crossbowPierceDamageFalloff);
    public float CrossbowPierceDamageFloor       => Mathf.Clamp01(crossbowPierceDamageFloor);
    public int   CrossbowPierceFalloffCount      => Mathf.Max(1, crossbowPierceFalloffCount);
    public bool  HasCrossbowBoltBleed            => hasCrossbowBoltBleed;
    public float CrossbowBleedDamageRatioPerStack => Mathf.Max(0f, crossbowBleedDamageRatioPerStack);
    public int   CrossbowBleedMaxStacks          => Mathf.Max(1, crossbowBleedMaxStacks);
    public float CrossbowBleedExpireSeconds      => Mathf.Max(0f, crossbowBleedExpireSeconds);
    public int ProjectileShotMultiplier => Mathf.Max(
        1,
        1 + Mathf.Max(0, projectileShotBonusCount) + ((hasTripleArrowUnlock || mutationAugmentsLongbow) ? 2 : 0));
    public float ArrowProjectileSpeedMultiplier => Mathf.Max(0.01f, arrowProjectileSpeedMultiplier);
    public float OutgoingDamageMultiplier       => Mathf.Max(0.01f, outgoingDamageMultiplier);
    public float MaxHealthMultiplier            => Mathf.Max(0.01f, maxHealthMultiplier);
    /// <summary>
    /// Passive — automatically active when the player has Charged Longbow AoE OR any Hammer Slam AoE upgrade.
    /// No augment slot is consumed; wall-loot behaviour triggers whenever this returns true.
    /// </summary>
    [Tooltip("Read-only at runtime. True when the player has Charged Longbow AoE or any Hammer AoE radius bonus.")]
    public bool  HasWallLootsUnlock             => hasChargedLongbowAoe || hammerAoeRadiusBonus > 0f;
    public bool  HasExtraAugmentSlotUnlock      => hasExtraAugmentSlotUnlock;
    public float LuckMultiplier                 => Mathf.Max(0.01f, luckMultiplier);
    public float HammerChargeMultiplier         => Mathf.Max(0.01f, hammerChargeMultiplier);
    public float IncomingDamageReduction        => Mathf.Clamp01(incomingDamageReduction);
    public bool  HasHammerChargeUnlock                => hasHammerChargeUnlock;
    public bool  HasHammerChargeDamageReductionUnlock => hasHammerChargeDamageReductionUnlock;
    public float HammerFreezeDuration                => Mathf.Max(0f, hammerFreezeDuration);
    /// <summary>Resonance multiplier — damage dealt to frozen enemies is multiplied by this value.</summary>
    public float FrozenEnemyVulnerabilityMultiplier  => Mathf.Max(1f, frozenEnemyVulnerabilityMultiplier);
    public float HammerMagnetRadius                  => Mathf.Max(0f, hammerMagnetRadius);
    public float HammerMagnetPullSpeed               => Mathf.Max(0f, hammerMagnetPullSpeed);
    public float HammerChargeFullStopDuration        => Mathf.Max(0f, hammerChargeFullStopDuration);
    public float HammerAoeRadiusMultiplier      => 1f + Mathf.Max(0f, hammerAoeRadiusBonus);
    public float HammerLightDamageMultiplier    => Mathf.Max(0.01f, hammerLightDamageMultiplier);
    public float HammerLightRateMultiplier      => Mathf.Max(0.01f, hammerLightRateMultiplier);
    public float HammerSlamCooldownMultiplier   => Mathf.Max(0.01f, hammerSlamCooldownMultiplier);
    public float ChargedLongbowAoeRadius            => Mathf.Max(0f, chargedLongbowAoeRadius * (1f + Mathf.Max(0f, longbowAoeRadiusBonus)));
    public float FlatMaxHealthBonus             => Mathf.Max(0f, flatMaxHealthBonus);

    // ── Gem Tier calculation ──────────────────────────────────────────────────

    public int LongbowGemAugmentCount
    {
        get
        {
            if (mutationAugmentsLongbow) return GemObsidianThreshold;
            int total = 0;
            for (int i = 0; i < LongbowGemAugmentIds.Length; i++)
                total += GetAppliedCount(LongbowGemAugmentIds[i]);
            return total;
        }
    }

    public GemTier LongbowGemTier
    {
        get
        {
            int n = LongbowGemAugmentCount;
            if (n >= GemObsidianThreshold) return GemTier.Obsidian;
            if (n >= GemDiamondThreshold)  return GemTier.Diamond;
            if (n >= GemGoldThreshold)     return GemTier.Gold;
            return GemTier.Coal;
        }
    }

    public bool HasLongbowMutation  => _longbowMutated;
    public bool HasCrossbowMutation => _crossbowMutated;
    public bool HasHammerMutation   => _hammerMutated;

    public bool HasRadialLongbowMutationUnlock =>
        mutationAugmentsLongbow || LongbowGemTier == GemTier.Obsidian || _longbowMutated;

    public bool MutatedArrowShots => HasRadialLongbowMutationUnlock;

    public bool ShouldUseRadialLongbowVolleyMutation(Player _) => HasRadialLongbowMutationUnlock;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _player = GetComponent<Player>();
        _initialChargedLongbowAoeRadius = chargedLongbowAoeRadius;
    }

    private void LateUpdate()
    {
        longbowGemCountDebug = LongbowGemAugmentCount;
        longbowGemTierDebug  = LongbowGemTier.ToString();
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    public void ResetAll()
    {
        movementSpeedBonus                  = 0f;
        hasChargedLongbowAoe                    = false;
        chargedLongbowAoeRadius                 = _initialChargedLongbowAoeRadius;
        hasTripleArrowUnlock                = false;
        hasExtraAugmentSlotUnlock           = false;
        luckMultiplier                      = 1f;
        hammerChargeMultiplier              = 1f;
        incomingDamageReduction             = 0f;
        hasHammerChargeUnlock                = false;
        hasHammerChargeDamageReductionUnlock = false;
        hammerFreezeDuration                = 0f;
        frozenEnemyVulnerabilityMultiplier   = 1f;
        hammerLightDamageMultiplier         = 1f;
        hammerLightRateMultiplier           = 1f;
        hammerSlamCooldownMultiplier        = 1f;
        longbowFreezeDuration                   = 0f;
        hasLongbowFreezeUnlock                  = false;
        hasFireArrowUnlock                  = false;
        hasPoisonArrowUnlock                = false;
        hasArrowSizeUnlock                  = false;
        hasVampiricArrowUnlock              = false;
        hammerAoeRadiusBonus                = 0f;
        longbowAoeRadiusBonus                   = 0f;
        flatMaxHealthBonus                  = 0f;
        projectileShotBonusCount            = 0;
        arrowProjectileSpeedMultiplier      = 1f;
        outgoingDamageMultiplier            = 1f;
        maxHealthMultiplier                 = 1f;
        hasCrossbowBoltPierce               = false;
        hasCrossbowBoltBleed                = false;
        _longbowMutated                     = false;
        _crossbowMutated                    = false;
        _hammerMutated                      = false;
        _appliedAugmentCounts.Clear();
    }

    // ── Augment query ─────────────────────────────────────────────────────────

    public bool HasAugment(AugmentId id)
    {
        if (id == AugmentId.None) return false;
        return GetAppliedCount(id) > 0;
    }

    public int GetAppliedCount(AugmentId id)
    {
        if (id == AugmentId.None) return 0;
        return _appliedAugmentCounts.TryGetValue(id, out int count) ? Mathf.Max(0, count) : 0;
    }

    public bool CanApplyAugment(AugmentDefinition augment)
    {
        if (augment == null) return false;
        if (!MeetsAugmentPrerequisites(augment.id)) return false;
        int currentCount = GetAppliedCount(augment.id);
        int maxCount     = GetMaxApplyCount(augment);
        return currentCount < maxCount;
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    public void ApplyAugment(AugmentDefinition augment)
    {
        if (augment == null) return;
        if (!CanApplyAugment(augment)) return;

        float prevMaxHpMult = maxHealthMultiplier;

        switch (augment.id)
        {
            case AugmentId.MovementSpeedIncreaseCommon:
            case AugmentId.MovementSpeedIncreaseRare:
            case AugmentId.MovementSpeedIncreaseExtraordinary:
                movementSpeedBonus += Mathf.Max(0f, augment.value);
                break;
            case AugmentId.ChargedLongbowAoeUnlock:
                hasChargedLongbowAoe = true;
                if (augment.value > 0f)
                    chargedLongbowAoeRadius = Mathf.Max(chargedLongbowAoeRadius, augment.value);
                break;
            case AugmentId.TripleArrowUnlock:
                hasTripleArrowUnlock = true;
                break;
            case AugmentId.ExtraAugmentSlotUnlock:
                hasExtraAugmentSlotUnlock = true;
                break;
            case AugmentId.LongbowFreezeUnlock:
                hasLongbowFreezeUnlock = true;
                longbowFreezeDuration  = augment.value > 0f ? augment.value : 1.5f;
                // Resonance: frozen enemies take 50% more damage once freeze unlock is obtained
                frozenEnemyVulnerabilityMultiplier = Mathf.Max(frozenEnemyVulnerabilityMultiplier, 1.5f);
                break;
            case AugmentId.FireArrowUnlock:
                hasFireArrowUnlock = true;
                break;
            case AugmentId.PoisonArrowUnlock:
                hasPoisonArrowUnlock = true;
                break;
            case AugmentId.ArrowSizeUnlock:
                hasArrowSizeUnlock = true;
                break;
            case AugmentId.VampiricArrowUnlock:
                hasVampiricArrowUnlock = true;
                break;
            case AugmentId.LuckIncrease_Common_I:
            case AugmentId.LuckIncrease_Common_II:
            case AugmentId.LuckIncrease_Common_III:
            case AugmentId.LuckIncrease_Rare:
            case AugmentId.LuckIncrease_Extraordinary:
                luckMultiplier *= 1f + Mathf.Max(0f, augment.value);
                break;
            case AugmentId.HammerChargeReduce_Common_I:
            case AugmentId.HammerChargeReduce_Common_II:
            case AugmentId.HammerChargeReduce_Rare:
            case AugmentId.HammerChargeReduceUnlock:
                hammerChargeMultiplier *= Mathf.Max(0.01f, 1f - Mathf.Clamp01(augment.value));
                break;
            case AugmentId.DamageReduction_Common:
            case AugmentId.DamageReduction_Rare:
            case AugmentId.DamageReduction_Extraordinary:
                incomingDamageReduction = Mathf.Clamp01(incomingDamageReduction + augment.value);
                break;
            case AugmentId.HammerChargeUnlock:
                hasHammerChargeUnlock = true;
                break;
            case AugmentId.HammerChargeDamageReductionUnlock:
                hasHammerChargeDamageReductionUnlock = true;
                break;
            case AugmentId.HammerFreeze_Common:
            case AugmentId.HammerFreeze_Rare:
            case AugmentId.HammerFreezeUnlock:
                hammerFreezeDuration += Mathf.Max(0f, augment.value);
                break;
            case AugmentId.HammerAoeRadius_Common:
            case AugmentId.HammerAoeRadius_Rare:
            case AugmentId.HammerAoeRadiusUnlock:
                hammerAoeRadiusBonus += Mathf.Max(0f, augment.value);
                break;
            case AugmentId.LongbowAoeRadius_Common:
            case AugmentId.LongbowAoeRadius_Rare:
            case AugmentId.LongbowAoeRadius_Extraordinary:
                longbowAoeRadiusBonus += Mathf.Max(0f, augment.value);
                break;
            case AugmentId.MaxHealthFlatIncrease_Common_I:
            case AugmentId.MaxHealthFlatIncrease_Common_II:
            case AugmentId.MaxHealthFlatIncrease_Common_III:
            case AugmentId.MaxHealthFlatIncrease_Common_IV:
                flatMaxHealthBonus += Mathf.Max(0f, augment.value);
                _player?.OnFlatMaxHealthBonusChanged(augment.value);
                break;
            case AugmentId.HalfHealthBonusDamage:
                outgoingDamageMultiplier *= 1.5f;
                maxHealthMultiplier      *= 0.5f;
                break;
            case AugmentId.GlassCannonDoubleDamageHalveMaxHealth:
                outgoingDamageMultiplier *= 2f;
                maxHealthMultiplier      *= 0.5f;
                break;
            case AugmentId.MaxHealthIncreasePercent:
                maxHealthMultiplier *= 1f + Mathf.Max(0f, augment.value);
                break;
            case AugmentId.ProjectileCount_ExtraArrow:
            case AugmentId.ProjectileCount_ExtraArrowPlus:
            case AugmentId.ProjectileCount_PlusOneProjectiles:
                projectileShotBonusCount++;
                break;
            case AugmentId.ProjectileCount_PlusOneAndSpeed10Percent:
            case AugmentId.ProjectileCount_PlusOneAndSpeed15Percent:
                projectileShotBonusCount++;
                arrowProjectileSpeedMultiplier *= 1f + Mathf.Max(0f, augment.value);
                break;
            case AugmentId.CrossbowBoltPierce:
                hasCrossbowBoltPierce = true;
                break;
            case AugmentId.CrossbowBoltBleed:
                hasCrossbowBoltBleed = true;
                break;

            // ── Hammer Light Attack ───────────────────────────────────────────
            case AugmentId.HammerLightDamageIncrease_Common:
            case AugmentId.HammerLightDamageIncrease_Rare:
            case AugmentId.HammerLightDamageIncrease_Extraordinary:
                hammerLightDamageMultiplier *= 1f + Mathf.Max(0f, augment.value);
                break;
            case AugmentId.HammerLightAttackSpeedIncrease_Common:
            case AugmentId.HammerLightAttackSpeedIncrease_Rare:
                hammerLightRateMultiplier *= Mathf.Max(0.01f, 1f - Mathf.Clamp01(augment.value));
                break;

            // ── Hammer Slam Cooldown ──────────────────────────────────────────
            case AugmentId.HammerSlamCooldownReduceUnlock:
                hammerSlamCooldownMultiplier *= Mathf.Max(0.01f, 1f - Mathf.Clamp01(augment.value));
                break;
        }

        _appliedAugmentCounts[augment.id] = GetAppliedCount(augment.id) + 1;
        AugmentApplied?.Invoke(augment);

        if (!Mathf.Approximately(prevMaxHpMult, maxHealthMultiplier))
            _player?.OnMaxHealthMultiplierChanged(prevMaxHpMult, maxHealthMultiplier);

        if (augment is UnlockAugmentDefinition unlockDef)
            CheckWeaponMutation(unlockDef.weaponType);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static int GetMaxApplyCount(AugmentDefinition augment)
    {
        if (augment == null) return 0;
        if (augment is UnlockAugmentDefinition) return 1; // one-time unlock

        switch (augment.id)
        {
            case AugmentId.MovementSpeedIncreaseCommon:
            case AugmentId.MovementSpeedIncreaseRare:
            case AugmentId.MovementSpeedIncreaseExtraordinary:
                return GetMovementSpeedMaxApplyCountFromRarity(augment.rarity);
            default:
                return 1;
        }
    }

    private static int GetMovementSpeedMaxApplyCountFromRarity(int rarity)
    {
        switch (rarity)
        {
            case 1:  return 3;
            case 2:  return 2;
            case 3:  return 1;
            default: return 1;
        }
    }

    // ── Weapon Mutation ───────────────────────────────────────────────────────

    private void CheckWeaponMutation(WeaponType weaponType)
    {
        if (unlockDatabase == null) return;

        System.Collections.Generic.List<UnlockAugmentDefinition> pool;
        switch (weaponType)
        {
            case WeaponType.Longbow:   pool = unlockDatabase.longbowUnlocks;   break;
            case WeaponType.Crossbow:  pool = unlockDatabase.crossbowUnlocks;  break;
            case WeaponType.Hammer:    pool = unlockDatabase.hammerUnlocks;    break;
            case WeaponType.Universal: pool = unlockDatabase.universalUnlocks; break;
            default: return;
        }

        if (pool == null || pool.Count == 0) return;

        foreach (UnlockAugmentDefinition u in pool)
            if (u == null || GetAppliedCount(u.id) == 0) return;

        GrantWeaponMutation(weaponType);
    }

    private void GrantWeaponMutation(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Longbow:  _longbowMutated  = true; break;
            case WeaponType.Crossbow: _crossbowMutated = true; break;
            case WeaponType.Hammer:   _hammerMutated   = true; break;
        }
    }

    private bool MeetsAugmentPrerequisites(AugmentId id)
    {
        switch (id)
        {
            case AugmentId.HammerChargeReduce_Common_I:
            case AugmentId.HammerChargeReduce_Common_II:
            case AugmentId.HammerChargeReduce_Rare:
            case AugmentId.HammerChargeReduceUnlock:
            case AugmentId.HammerChargeDamageReductionUnlock:
            case AugmentId.HammerFreeze_Common:
            case AugmentId.HammerFreeze_Rare:
            case AugmentId.HammerFreezeUnlock:
            case AugmentId.HammerAoeRadius_Common:
            case AugmentId.HammerAoeRadius_Rare:
            case AugmentId.HammerAoeRadiusUnlock:
            case AugmentId.HammerSlamCooldownReduceUnlock:
                return hasHammerChargeUnlock;
            case AugmentId.LongbowAoeRadius_Common:
            case AugmentId.LongbowAoeRadius_Rare:
            case AugmentId.LongbowAoeRadius_Extraordinary:
                return hasChargedLongbowAoe;
            case AugmentId.CrossbowBoltPierce:
            case AugmentId.CrossbowBoltBleed:
                // Crossbow augments are only relevant once the crossbow is active
                // (Obsidian gem tier / radial longbow mutation unlocked)
                return HasRadialLongbowMutationUnlock;
            default:
                return true;
        }
    }
}
