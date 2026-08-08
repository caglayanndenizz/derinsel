using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAugmentController : MonoBehaviour
{
    // ── Gem Tier System ───────────────────────────────────────────────────────

    public const int GemCoalThreshold     = 1;
    public const int GemGoldThreshold     = 2;
    public const int GemDiamondThreshold  = 3;
    public const int GemObsidianThreshold = 4;

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
        AugmentId.BowDamageMultiplier_Common,
        AugmentId.BowDamageMultiplier_Rare,
        AugmentId.BowDamageMultiplier_Extraordinary,
        AugmentId.ProjectileCount_PlusOneProjectiles,
        AugmentId.ProjectileCount_PlusOneAndSpeed10Percent,
        AugmentId.ProjectileCount_PlusOneAndSpeed15Percent,
        AugmentId.BowChargeSpeed_Tier1,
        AugmentId.BowChargeSpeed_Tier2,
        AugmentId.BowChargeSpeed_Tier3,
    };

    private static readonly AugmentId[] HammerGemAugmentIds =
    {
        AugmentId.HammerChargeDamageReductionUnlock,
        AugmentId.HammerFreezeUnlock,
        AugmentId.HammerAoeRadiusUnlock,
        AugmentId.HammerChargeSpeed_Tier1,
        AugmentId.HammerChargeSpeed_Tier2,
        AugmentId.HammerChargeSpeed_Tier3,
        AugmentId.HammerFreeze_Common,
        AugmentId.HammerFreeze_Rare,
        AugmentId.HammerAoeRadius_Common,
        AugmentId.HammerAoeRadius_Rare,
        AugmentId.HammerDamageMultiplier_Common,
        AugmentId.HammerDamageMultiplier_Rare,
        AugmentId.HammerDamageMultiplier_Extraordinary,
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
    [SerializeField] private bool  hasHammerChargeDamageReductionUnlock;
    [SerializeField] private float hammerChargeMultiplier  = 1f;
    [SerializeField] private float hammerAoeRadiusBonus    = 0f;
    [SerializeField] private float hammerFreezeDuration    = 0f;

    [Header("Weapon Damage Multipliers")]
    [SerializeField] private float bowDamageMultiplier    = 1f;
    [SerializeField] private float hammerDamageMultiplier = 1f;
    [SerializeField] private float playerBaseDamageBonus  = 0f;
    [SerializeField] private float knockbackMultiplier = 1f;
    [SerializeField] private bool  hasCriticalStrikeChance;
    [SerializeField] private float critChance = 0f;
    [SerializeField] private float critDamage = 1f;

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
    [SerializeField] private bool  hasLifeStealUnlock;
    [SerializeField] private int   projectileShotBonusCount;
    [SerializeField] private float arrowProjectileSpeedMultiplier = 1f;

    [Header("Longbow — Charge")]
    [SerializeField] private float bowChargeMultiplier = 1f;

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
    private readonly List<AugmentDefinition> _appliedAugments = new();
    private readonly List<float> _appliedAugmentRolls = new();

    [Header("Unlock Augment Database")]
    [Tooltip("Tracks unlock augments per weapon. Used for mutation checks.")]
    [SerializeField] private UnlockAugmentDatabase unlockDatabase;

    [Header("Test")]
    [Tooltip("When checked, triggers the Obsidian mutation as if 4 longbow augments were obtained.")]
    [SerializeField] private bool mutationAugmentsLongbow;
    [Tooltip("When checked, triggers the Obsidian mutation as if all hammer gem augments were obtained.")]
    [SerializeField] private bool mutationAugmentsHammer;

    // Weapon mutation flags — set automatically when all unlock augments for a weapon are obtained
    private bool _longbowMutated;
    private bool _crossbowMutated;
    private bool _hammerMutated;

    [Header("Debug (Updated in Play Mode)")]
    [SerializeField] private int      longbowGemCountDebug;
    [SerializeField] private string   longbowGemTierDebug;
    [SerializeField] private int      hammerGemCountDebug;
    [SerializeField] private string   hammerGemTierDebug;

    private Player _player;

    public event Action<AugmentDefinition> AugmentApplied;
    public event Action<AugmentDefinition> AugmentRemoved;

    /// <summary>Every currently active augment instance, in the order they were applied. Duplicates appear once per stack.</summary>
    public IReadOnlyList<AugmentDefinition> AppliedAugments => _appliedAugments;

    // ── Properties ────────────────────────────────────────────────────────────

    public float MovementSpeedBonus          => Mathf.Max(0f, movementSpeedBonus);
    public bool  HasChargedLongbowAoe            => hasChargedLongbowAoe   || mutationAugmentsLongbow;
    public bool  HasLongbowFreezeUnlock          => hasLongbowFreezeUnlock  || mutationAugmentsLongbow;
    public bool  HasFireArrowUnlock          => hasFireArrowUnlock  || mutationAugmentsLongbow;
    public bool  HasPoisonArrowUnlock        => hasPoisonArrowUnlock || mutationAugmentsLongbow;
    public bool  HasArrowSizeUnlock          => hasArrowSizeUnlock;
    public bool  HasLifeStealUnlock          => hasLifeStealUnlock;
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
    public float BowDamageMultiplier    => Mathf.Max(0.01f, bowDamageMultiplier);
    public float HammerDamageMultiplier => Mathf.Max(0.01f, hammerDamageMultiplier);
    public float PlayerBaseDamageBonus  => Mathf.Max(0f, playerBaseDamageBonus);
    public float KnockbackMultiplier => Mathf.Max(0.01f, knockbackMultiplier);
    public bool  HasCriticalStrikeChance => hasCriticalStrikeChance;
    public float CritChance              => Mathf.Clamp01(critChance);
    public float CritDamage              => Mathf.Max(1f, critDamage);
    public float MaxHealthMultiplier            => Mathf.Max(0.01f, maxHealthMultiplier);
    public bool  HasExtraAugmentSlotUnlock      => hasExtraAugmentSlotUnlock;
    public float LuckMultiplier                 => Mathf.Max(0.01f, luckMultiplier);
    public float HammerChargeMultiplier         => Mathf.Max(0f, hammerChargeMultiplier);
    public float BowChargeMultiplier            => Mathf.Max(0f, bowChargeMultiplier);
    public float IncomingDamageReduction        => Mathf.Clamp01(incomingDamageReduction);
    public bool  HasHammerChargeDamageReductionUnlock => hasHammerChargeDamageReductionUnlock;
    public float HammerFreezeDuration                => Mathf.Max(0f, hammerFreezeDuration);
    /// <summary>Resonance multiplier — damage dealt to frozen enemies is multiplied by this value.</summary>
    public float FrozenEnemyVulnerabilityMultiplier  => Mathf.Max(1f, frozenEnemyVulnerabilityMultiplier);
    public float HammerMagnetRadius                  => Mathf.Max(0f, hammerMagnetRadius);
    public float HammerMagnetPullSpeed               => Mathf.Max(0f, hammerMagnetPullSpeed);
    public float HammerChargeFullStopDuration        => Mathf.Max(0f, hammerChargeFullStopDuration);
    public float HammerAoeRadiusMultiplier      => 1f + Mathf.Max(0f, hammerAoeRadiusBonus);
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

    public int HammerGemAugmentCount
    {
        get
        {
            if (mutationAugmentsHammer) return GemObsidianThreshold;
            int total = 0;
            for (int i = 0; i < HammerGemAugmentIds.Length; i++)
                total += GetAppliedCount(HammerGemAugmentIds[i]);
            return total;
        }
    }

    public GemTier HammerGemTier
    {
        get
        {
            int n = HammerGemAugmentCount;
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

    public bool HasHammerMutationUnlock =>
        mutationAugmentsHammer || HammerGemTier == GemTier.Obsidian || _hammerMutated;

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
        hammerGemCountDebug  = HammerGemAugmentCount;
        hammerGemTierDebug   = HammerGemTier.ToString();
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    /// <summary>Resets every mutated field to its default — the baseline that <see cref="RecomputeFromAppliedAugments"/> replays augments onto.</summary>
    private void ResetMutableFieldsToDefaults()
    {
        movementSpeedBonus                  = 0f;
        hasChargedLongbowAoe                    = false;
        chargedLongbowAoeRadius                 = _initialChargedLongbowAoeRadius;
        hasTripleArrowUnlock                = false;
        bowChargeMultiplier                 = 1f;
        hasExtraAugmentSlotUnlock           = false;
        luckMultiplier                      = 1f;
        hammerChargeMultiplier              = 1f;
        incomingDamageReduction             = 0f;
        hasHammerChargeDamageReductionUnlock = false;
        hammerFreezeDuration                = 0f;
        frozenEnemyVulnerabilityMultiplier   = 1f;
        bowDamageMultiplier                 = 1f;
        hammerDamageMultiplier              = 1f;
        playerBaseDamageBonus               = 0f;
        knockbackMultiplier                 = 1f;
        longbowFreezeDuration                   = 0f;
        hasLongbowFreezeUnlock                  = false;
        hasFireArrowUnlock                  = false;
        hasPoisonArrowUnlock                = false;
        hasArrowSizeUnlock                  = false;
        hasLifeStealUnlock                  = false;
        hammerAoeRadiusBonus                = 0f;
        longbowAoeRadiusBonus                   = 0f;
        flatMaxHealthBonus                  = 0f;
        projectileShotBonusCount            = 0;
        arrowProjectileSpeedMultiplier      = 1f;
        outgoingDamageMultiplier            = 1f;
        maxHealthMultiplier                 = 1f;
        hasCrossbowBoltPierce               = false;
        hasCrossbowBoltBleed                = false;
        hasCriticalStrikeChance             = false;
        critChance                          = 0f;
        critDamage                          = 1f;
    }

    /// <summary>Full reset for a new run — clears the active augment list and permanent weapon-mutation flags too.</summary>
    public void ResetAll()
    {
        ResetMutableFieldsToDefaults();
        _longbowMutated  = false;
        _crossbowMutated = false;
        _hammerMutated   = false;
        _appliedAugments.Clear();
        _appliedAugmentRolls.Clear();
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
        int count = 0;
        for (int i = 0; i < _appliedAugments.Count; i++)
            if (_appliedAugments[i] != null && _appliedAugments[i].id == id)
                count++;
        return count;
    }

    public bool CanApplyAugment(AugmentDefinition augment)
    {
        if (augment == null) return false;
        if (!MeetsAugmentPrerequisites(augment.id)) return false;
        int currentCount = GetAppliedCount(augment.id);
        int maxCount     = GetMaxApplyCount(augment);
        return currentCount < maxCount;
    }

    // ── Apply / Remove ────────────────────────────────────────────────────────

    public void ApplyAugment(AugmentDefinition augment)
    {
        if (augment == null) return;
        if (!CanApplyAugment(augment)) return;

        _appliedAugments.Add(augment);
        _appliedAugmentRolls.Add(RollAugmentBonus(augment.id));
        RecomputeFromAppliedAugments();
        AugmentApplied?.Invoke(augment);

        if (augment is UnlockAugmentDefinition unlockDef)
            CheckWeaponMutation(unlockDef.weaponType);
    }

    /// <summary>
    /// Sells one stack of the given augment — removes it from the active list and fully
    /// undoes its effect (via <see cref="RecomputeFromAppliedAugments"/>). Weapon-mutation
    /// flags are NOT reverted; once a weapon mutates it stays mutated even if a prerequisite
    /// unlock is later sold, and any other augment that already required it stays applied.
    /// </summary>
    public bool RemoveAugment(AugmentDefinition augment)
    {
        if (augment == null) return false;

        int index = _appliedAugments.FindIndex(a => a != null && a.id == augment.id);
        if (index < 0) return false;

        _appliedAugments.RemoveAt(index);
        _appliedAugmentRolls.RemoveAt(index);
        RecomputeFromAppliedAugments();
        AugmentRemoved?.Invoke(augment);
        return true;
    }

    /// <summary>Rolls the one-time random bonus for augments whose magnitude is randomized at pick-time (not per-hit). Returns 0 for all other augments.</summary>
    private static float RollAugmentBonus(AugmentId id)
    {
        switch (id)
        {
            case AugmentId.BaseDamageIncrease_Tier1: return UnityEngine.Random.Range(2f, 5f);
            case AugmentId.BaseDamageIncrease_Tier2: return UnityEngine.Random.Range(4f, 7f);
            case AugmentId.BaseDamageIncrease_Tier3: return UnityEngine.Random.Range(5f, 10f);
            default: return 0f;
        }
    }

    /// <summary>Resets to defaults then replays every active augment's effect in application order — keeps ApplyAugment/RemoveAugment symmetric.</summary>
    private void RecomputeFromAppliedAugments()
    {
        float prevMaxHealthMultiplier = maxHealthMultiplier;
        float prevFlatMaxHealthBonus  = flatMaxHealthBonus;

        ResetMutableFieldsToDefaults();

        for (int i = 0; i < _appliedAugments.Count; i++)
            ApplyEffectOnly(_appliedAugments[i], _appliedAugmentRolls[i]);

        if (!Mathf.Approximately(prevFlatMaxHealthBonus, flatMaxHealthBonus))
            _player?.OnFlatMaxHealthBonusChanged(flatMaxHealthBonus - prevFlatMaxHealthBonus);

        if (!Mathf.Approximately(prevMaxHealthMultiplier, maxHealthMultiplier))
            _player?.OnMaxHealthMultiplierChanged(prevMaxHealthMultiplier, maxHealthMultiplier);
    }

    /// <summary>Pure stat mutation for one augment instance — no bookkeeping, no events. Only ever called by RecomputeFromAppliedAugments.</summary>
    private void ApplyEffectOnly(AugmentDefinition augment, float rolledValue)
    {
        switch (augment.id)
        {
            case AugmentId.ChargedLongbowAoeUnlock:
                hasChargedLongbowAoe = true;
                if (augment.value > 0f)
                    chargedLongbowAoeRadius = Mathf.Max(chargedLongbowAoeRadius, augment.value);
                break;
            case AugmentId.TripleArrowUnlock:
                hasTripleArrowUnlock = true;
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
            case AugmentId.LifeStealUnlock:
                hasLifeStealUnlock = true;
                break;
            case AugmentId.HammerChargeSpeed_Tier1:
            case AugmentId.HammerChargeSpeed_Tier2:
            case AugmentId.HammerChargeSpeed_Tier3:
                hammerChargeMultiplier -= Mathf.Clamp01(augment.value);
                break;
            case AugmentId.BowChargeSpeed_Tier1:
            case AugmentId.BowChargeSpeed_Tier2:
            case AugmentId.BowChargeSpeed_Tier3:
                bowChargeMultiplier -= Mathf.Clamp01(augment.value);
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

            case AugmentId.BowDamageMultiplier_Common:
            case AugmentId.BowDamageMultiplier_Rare:
            case AugmentId.BowDamageMultiplier_Extraordinary:
                bowDamageMultiplier *= 1f + Mathf.Max(0f, augment.value);
                break;
            case AugmentId.HammerDamageMultiplier_Common:
            case AugmentId.HammerDamageMultiplier_Rare:
            case AugmentId.HammerDamageMultiplier_Extraordinary:
                hammerDamageMultiplier *= 1f + Mathf.Max(0f, augment.value);
                break;

            // ── Player Base Damage ─────────────────────────────────────────────
            case AugmentId.BaseDamageIncrease_Tier1:
            case AugmentId.BaseDamageIncrease_Tier2:
            case AugmentId.BaseDamageIncrease_Tier3:
                playerBaseDamageBonus += rolledValue;
                break;

            // ── Knockback ───────────────────────────────────────────────────────
            case AugmentId.KnockbackForceIncrease:
                knockbackMultiplier *= 1f + Mathf.Max(0f, augment.value);
                break;

            // ── Critical Strike ────────────────────────────────────────────────
            case AugmentId.CriticalStrikeChance:
                hasCriticalStrikeChance = true;
                critChance = 0.10f;
                critDamage = 1.5f;
                break;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static int GetMaxApplyCount(AugmentDefinition augment)
    {
        if (augment == null) return 0;
        if (augment is UnlockAugmentDefinition) return 1; // one-time unlock

        return 1;
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
