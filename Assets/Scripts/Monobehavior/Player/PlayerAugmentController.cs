using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAugmentController : MonoBehaviour
{
    // ── Cevher Sistemi ────────────────────────────────────────────────────────

    public const int CevherKomurThreshold    = 1;
    public const int CevherAltinThreshold    = 2;
    public const int CevherElmasThreshold    = 4;
    public const int CevherObsidyenThreshold = 6;

    private static readonly AugmentId[] LongbowCevherAugmentIds =
    {
        AugmentId.ChargedLongbowAoeUnlock,
        AugmentId.DoubleArrowUnlock,
        AugmentId.LongbowFreezeUnlock,
        AugmentId.FireArrowUnlock,
        AugmentId.PoisonArrowUnlock,
        AugmentId.LongbowAoeRadius_Common,
        AugmentId.LongbowAoeRadius_Rare,
        AugmentId.LongbowAoeRadius_Extraordinary,
        AugmentId.ProjectileCount_PlusOneProjectiles,
        AugmentId.ProjectileCount_PlusOneAndSpeed10Percent,
        AugmentId.ProjectileCount_PlusOneAndSpeed15Percent,
    };

    // ── Runtime stats ─────────────────────────────────────────────────────────

    [SerializeField] private float movementSpeedBonus;
    [SerializeField] private bool  hasChargedLongbowAoe;
    [SerializeField] private float chargedLongbowAoeRadius = 3f;
    [SerializeField] private bool  hasDoubleArrowUnlock;
    [SerializeField] private bool  hasExtraAugmentSlotUnlock;
    [SerializeField] private bool  hasDashUnluck;
    [SerializeField] private float dashCooldownMultiplier  = 1f;
    [SerializeField] private float luckMultiplier          = 1f;
    [SerializeField] private float hammerChargeMultiplier  = 1f;
    [SerializeField] private float dashDistanceMultiplier  = 1f;
    [SerializeField] private float incomingDamageReduction = 0f;
    [SerializeField] private bool  hasHammerChargeUnlock;
    [SerializeField] private bool  hasHammerChargeDamageReductionUnlock;
    [SerializeField] private float hammerFreezeDuration    = 0f;
    [SerializeField] private float longbowFreezeDuration       = 0f;
    [SerializeField] private bool  hasLongbowFreezeUnlock;
    [SerializeField] private bool  hasFireArrowUnlock;
    [SerializeField] private bool  hasPoisonArrowUnlock;
    [SerializeField] private float hammerAoeRadiusBonus    = 0f;

    [Header("Hammer — Light Attack Stats")]
    [SerializeField] private float hammerLightDamageMultiplier  = 1f;
    [SerializeField] private float hammerLightRateMultiplier    = 1f;

    [Header("Hammer — Slam Cooldown")]
    [SerializeField] private float hammerSlamCooldownMultiplier = 1f;

    [Header("Longbow — AoE & DoT")]
    [SerializeField] private float longbowAoeRadiusBonus            = 0f;

    [Header("Longbow — Fire Arrow DoT")]
    [SerializeField] private float fireDotDuration         = 3f;
    [SerializeField] private float fireDotDamagePerSecond  = 2f;

    [Header("Longbow — Poison Arrow DoT")]
    [SerializeField] private float poisonDotDuration       = 5f;
    [SerializeField] private float poisonDotDamagePerSecond= 1.5f;

    [Header("Crossbow — Bolt Pierce")]
    [SerializeField] private bool  hasCrossbowBoltPierce;
    [Tooltip("Her düşman başına hasarın yüzde kaçı düşer (0.20 = %20).")]
    [SerializeField] private float crossbowPierceDamageFalloff = 0.20f;
    [Tooltip("Pierce hasarının minimum çarpanı (0.30 = %30).")]
    [SerializeField] private float crossbowPierceDamageFloor   = 0.30f;
    [Tooltip("Kaç düşman sonrası floor'a düşülür (varsayılan 3 → 4. düşmandan itibaren %30).")]
    [SerializeField] private int   crossbowPierceFalloffCount  = 3;

    [Header("Crossbow — Bolt Bleed")]
    [SerializeField] private bool  hasCrossbowBoltBleed;
    [Tooltip("Her stack'in bolt hasarına oranı (0.01 = %1). Max stack'te toplam hasar = maxStacks * oran.")]
    [SerializeField] private float crossbowBleedDamageRatioPerStack = 0.01f;
    [Tooltip("Maksimum stack sayısı (varsayılan 5 → toplam %5).")]
    [SerializeField] private int   crossbowBleedMaxStacks           = 5;
    [Tooltip("Son vuruştan bu süre geçerse (saniye) kanama sona erer.")]
    [SerializeField] private float crossbowBleedExpireSeconds       = 5f;

    [SerializeField] private float flatMaxHealthBonus = 0f;

    private float _initialChargedLongbowAoeRadius;
    private readonly Dictionary<AugmentId, int> _appliedAugmentCounts = new();

    [SerializeField] private int   projectileShotBonusCount;
    [SerializeField] private float arrowProjectileSpeedMultiplier = 1f;
    [SerializeField] private float outgoingDamageMultiplier       = 1f;
    [SerializeField] private float maxHealthMultiplier            = 1f;

    [Header("Unlock Augment Database")]
    [Tooltip("Silaha göre unlock augmentleri takip etmek için. Mutation kontrolünde kullanılır.")]
    [SerializeField] private UnlockAugmentDatabase unlockDatabase;

    [Header("MutationAugmentsLongbow")]
    [SerializeField]
    [Tooltip("İşaretlenince 6 longbow augment varmış gibi Obsidyen mutasyonu tetiklenir (test).")]
    private bool mutationAugmentsLongbow;

    // Weapon mutation flags — set automatically when all unlock augments for a weapon are obtained
    private bool _longbowMutated;
    private bool _crossbowMutated;
    private bool _hammerMutated;

    [Header("Motor test (Play Mode'da güncellenir)")]
    [SerializeField] private int      longbowCevherCountMotor;
    [SerializeField] private string   longbowCevherTierMotor;

    private Player _player;

    public event Action<AugmentDefinition> AugmentApplied;

    // ── Properties ────────────────────────────────────────────────────────────

    public float MovementSpeedBonus          => Mathf.Max(0f, movementSpeedBonus);
    public bool  HasChargedLongbowAoe            => hasChargedLongbowAoe   || mutationAugmentsLongbow;
    public bool  HasLongbowFreezeUnlock          => hasLongbowFreezeUnlock  || mutationAugmentsLongbow;
    public bool  HasFireArrowUnlock          => hasFireArrowUnlock  || mutationAugmentsLongbow;
    public bool  HasPoisonArrowUnlock        => hasPoisonArrowUnlock || mutationAugmentsLongbow;
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
        1 + Mathf.Max(0, projectileShotBonusCount) + ((hasDoubleArrowUnlock || mutationAugmentsLongbow) ? 1 : 0));
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
    public bool  HasDashUnluck                  => hasDashUnluck;
    public float DashCooldownMultiplier         => Mathf.Max(0.01f, dashCooldownMultiplier);
    public float LuckMultiplier                 => Mathf.Max(0.01f, luckMultiplier);
    public float HammerChargeMultiplier         => Mathf.Max(0.01f, hammerChargeMultiplier);
    public float DashDistanceMultiplier         => Mathf.Max(0.01f, dashDistanceMultiplier);
    public float IncomingDamageReduction        => Mathf.Clamp01(incomingDamageReduction);
    public bool  HasHammerChargeUnlock                => hasHammerChargeUnlock;
    public bool  HasHammerChargeDamageReductionUnlock => hasHammerChargeDamageReductionUnlock;
    public float HammerFreezeDuration           => Mathf.Max(0f, hammerFreezeDuration);
    public float HammerAoeRadiusMultiplier      => 1f + Mathf.Max(0f, hammerAoeRadiusBonus);
    public float HammerLightDamageMultiplier    => Mathf.Max(0.01f, hammerLightDamageMultiplier);
    public float HammerLightRateMultiplier      => Mathf.Max(0.01f, hammerLightRateMultiplier);
    public float HammerSlamCooldownMultiplier   => Mathf.Max(0.01f, hammerSlamCooldownMultiplier);
    public float ChargedLongbowAoeRadius            => Mathf.Max(0f, chargedLongbowAoeRadius * (1f + Mathf.Max(0f, longbowAoeRadiusBonus)));
    public float FlatMaxHealthBonus             => Mathf.Max(0f, flatMaxHealthBonus);

    // ── Cevher Sistemi hesaplama ──────────────────────────────────────────────

    public int LongbowCevherAugmentCount
    {
        get
        {
            if (mutationAugmentsLongbow) return CevherObsidyenThreshold;
            int total = 0;
            for (int i = 0; i < LongbowCevherAugmentIds.Length; i++)
                total += GetAppliedCount(LongbowCevherAugmentIds[i]);
            return total;
        }
    }

    public CevherTier LongbowCevherTier
    {
        get
        {
            int n = LongbowCevherAugmentCount;
            if (n >= CevherObsidyenThreshold) return CevherTier.Obsidyen;
            if (n >= CevherElmasThreshold)    return CevherTier.Elmas;
            if (n >= CevherAltinThreshold)    return CevherTier.Altin;
            return CevherTier.Komur;
        }
    }

    public bool HasLongbowMutation  => _longbowMutated;
    public bool HasCrossbowMutation => _crossbowMutated;
    public bool HasHammerMutation   => _hammerMutated;

    public bool HasRadialLongbowMutationUnlock =>
        mutationAugmentsLongbow || LongbowCevherTier == CevherTier.Obsidyen || _longbowMutated;

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
        longbowCevherCountMotor = LongbowCevherAugmentCount;
        longbowCevherTierMotor  = LongbowCevherTier.ToString();
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    public void ResetAll()
    {
        movementSpeedBonus                  = 0f;
        hasChargedLongbowAoe                    = false;
        chargedLongbowAoeRadius                 = _initialChargedLongbowAoeRadius;
        hasDoubleArrowUnlock                = false;
        hasExtraAugmentSlotUnlock           = false;
        hasDashUnluck                       = false;
        dashCooldownMultiplier              = 1f;
        luckMultiplier                      = 1f;
        hammerChargeMultiplier              = 1f;
        dashDistanceMultiplier              = 1f;
        incomingDamageReduction             = 0f;
        hasHammerChargeUnlock                = false;
        hasHammerChargeDamageReductionUnlock = false;
        hammerFreezeDuration                = 0f;
        hammerLightDamageMultiplier         = 1f;
        hammerLightRateMultiplier           = 1f;
        hammerSlamCooldownMultiplier        = 1f;
        longbowFreezeDuration                   = 0f;
        hasLongbowFreezeUnlock                  = false;
        hasFireArrowUnlock                  = false;
        hasPoisonArrowUnlock                = false;
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
            case AugmentId.DoubleArrowUnlock:
                hasDoubleArrowUnlock = true;
                break;
            case AugmentId.ExtraAugmentSlotUnlock:
                hasExtraAugmentSlotUnlock = true;
                break;
            case AugmentId.DashUnluck:
                hasDashUnluck = true;
                break;
            case AugmentId.LongbowFreezeUnlock:
                hasLongbowFreezeUnlock = true;
                longbowFreezeDuration  = augment.value > 0f ? augment.value : 1.5f;
                break;
            case AugmentId.FireArrowUnlock:
                hasFireArrowUnlock = true;
                break;
            case AugmentId.PoisonArrowUnlock:
                hasPoisonArrowUnlock = true;
                break;
            case AugmentId.DashCooldownReduce_Common_I:
            case AugmentId.DashCooldownReduce_Common_II:
            case AugmentId.DashCooldownReduce_Rare:
            case AugmentId.DashCooldownReduce_Extraordinary:
                dashCooldownMultiplier *= Mathf.Max(0.01f, 1f - Mathf.Clamp01(augment.value));
                break;
            case AugmentId.LuckIncrease_Common_I:
            case AugmentId.LuckIncrease_Common_II:
            case AugmentId.LuckIncrease_Common_III:
            case AugmentId.LuckIncrease_Rare:
            case AugmentId.LuckIncrease_Extraordinary:
                luckMultiplier *= 1f + Mathf.Max(0f, augment.value);
                break;
            case AugmentId.HammerChargeReduceUnlock:
                hammerChargeMultiplier *= Mathf.Max(0.01f, 1f - Mathf.Clamp01(augment.value));
                break;
            case AugmentId.DashDistanceIncrease_Uncommon_I:
            case AugmentId.DashDistanceIncrease_Uncommon_II:
            case AugmentId.DashDistanceIncrease_Uncommon_III:
            case AugmentId.DashDistanceIncrease_Rare:
            case AugmentId.DashDistanceIncrease_Extraordinary:
                dashDistanceMultiplier *= 1f + Mathf.Max(0f, augment.value);
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
            case AugmentId.HammerFreezeUnlock:
                hammerFreezeDuration += Mathf.Max(0f, augment.value);
                break;
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
            case AugmentId.DashCooldownReduce_Common_I:
            case AugmentId.DashCooldownReduce_Common_II:
            case AugmentId.DashCooldownReduce_Rare:
            case AugmentId.DashCooldownReduce_Extraordinary:
            case AugmentId.DashDistanceIncrease_Uncommon_I:
            case AugmentId.DashDistanceIncrease_Uncommon_II:
            case AugmentId.DashDistanceIncrease_Uncommon_III:
            case AugmentId.DashDistanceIncrease_Rare:
            case AugmentId.DashDistanceIncrease_Extraordinary:
                return hasDashUnluck;
            case AugmentId.HammerChargeReduceUnlock:
            case AugmentId.HammerChargeDamageReductionUnlock:
            case AugmentId.HammerFreezeUnlock:
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
                // (Obsidyen cevher tier / radial longbow mutation unlocked)
                return HasRadialLongbowMutationUnlock;
            default:
                return true;
        }
    }
}
