using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAugmentController : MonoBehaviour
{
    /// <summary>Bu id'lerden kaç farklısı alındıysa otomatik ok mutasyonu ilerler (VS tarzı).</summary>
    private static readonly AugmentId[] ArrowAugmentIdsForAutomaticMutation =
    {
        AugmentId.DoubleArrowUnlock,
        AugmentId.ArrowCount_IncreaseNumberOfArrowsBy1,
        AugmentId.ArrowCount_PlusOneArrows,
        AugmentId.ArrowCount_PlusOneAndSpeed10Percent,
        AugmentId.ArrowCount_PlusOneAndSpeed15Percent,
        AugmentId.ArrowCount_IncreaseYourArrowsBy1,
    };

    public const int ArrowAutomaticMutationRequiredDistinctTypes = 5;

    [SerializeField] private float movementSpeedBonus;
    [SerializeField] private bool hasChargedBowAoe;
    [SerializeField] private float chargedBowAoeRadius = 3f;
    [SerializeField] private bool hasDoubleArrowUnlock;
    [SerializeField] private bool hasWallLootsUnlock;
    [SerializeField] private bool hasExtraAugmentSlotUnlock;
    [SerializeField] private bool hasDashUnluck;
    [SerializeField] private float dashCooldownMultiplier = 1f;
    [SerializeField] private float luckMultiplier = 1f;
    [SerializeField] private float hammerChargeMultiplier = 1f;
    [SerializeField] private float dashDistanceMultiplier = 1f;
    [SerializeField] private float incomingDamageReduction = 0f;
    [SerializeField] private bool hasHammerChargeDamageReductionUnlock;
    [SerializeField] private float hammerFreezeDuration = 0f;
    [SerializeField] private float bowFreezeDuration = 0f;
    [SerializeField] private float hammerAoeRadiusBonus = 0f;
    [SerializeField] private float bowAoeRadiusBonus = 0f;
    [SerializeField] private float flatMaxHealthBonus = 0f;
    private float _initialChargedBowAoeRadius;
    private readonly Dictionary<AugmentId, int> _appliedAugmentCounts = new();

    [SerializeField] private int arrowShotBonusCount;
    [SerializeField] private float arrowProjectileSpeedMultiplier = 1f;
    [SerializeField] private float outgoingDamageMultiplier = 1f;
    [SerializeField] private float maxHealthMultiplier = 1f;

    [Header("Motor / hızlı test")]
    [SerializeField]
    [Tooltip("Play modunda işaretle: 5 augment toplamadan otomatik ok mutasyonu açılmış gider (gerçek ilerleme değişmez).")]
    private bool cheatForceRadialArrowMutation;

    [Header("Motor test (Play Mode'da güncellenir)")]
    [SerializeField]
    [Tooltip("Read-only görünüm: MutatedArrowShots ile aynı; Play’de seçili oyuncunun Inspectorunda izle.")]
    private bool mutatedArrowShotsMotor;
    [SerializeField]
    [Tooltip("Sayılan farklı ok-augment çeşitleri (bu run’da kaçını topladin; 5+ mutasyon).")]
    private int arrowMutationProgressMotor;

    private bool _loggedAllArrowAugmentsComplete;
    private Player _player;

    public event Action<AugmentDefinition> AugmentApplied;

    public float MovementSpeedBonus => Mathf.Max(0f, movementSpeedBonus);
    public bool HasChargedBowAoe => hasChargedBowAoe;

    public int ArrowShotMultiplier => Mathf.Max(
        1,
        1 + Mathf.Max(0, arrowShotBonusCount) + (hasDoubleArrowUnlock ? 1 : 0));
    public float ArrowProjectileSpeedMultiplier => Mathf.Max(0.01f, arrowProjectileSpeedMultiplier);
    public float OutgoingDamageMultiplier => Mathf.Max(0.01f, outgoingDamageMultiplier);
    public float MaxHealthMultiplier => Mathf.Max(0.01f, maxHealthMultiplier);
    public bool HasWallLootsUnlock => hasWallLootsUnlock;
    public bool HasExtraAugmentSlotUnlock => hasExtraAugmentSlotUnlock;
    public bool HasDashUnluck => hasDashUnluck;
    public float DashCooldownMultiplier => Mathf.Max(0.01f, dashCooldownMultiplier);
    public float LuckMultiplier => Mathf.Max(0.01f, luckMultiplier);
    public float HammerChargeMultiplier => Mathf.Max(0.01f, hammerChargeMultiplier);
    public float DashDistanceMultiplier => Mathf.Max(0.01f, dashDistanceMultiplier);
    public float IncomingDamageReduction => Mathf.Clamp01(incomingDamageReduction);
    public bool HasHammerChargeDamageReductionUnlock => hasHammerChargeDamageReductionUnlock;
    public float HammerFreezeDuration => Mathf.Max(0f, hammerFreezeDuration);
    public float BowFreezeDuration => Mathf.Max(0f, bowFreezeDuration);
    public float HammerAoeRadiusMultiplier => 1f + Mathf.Max(0f, hammerAoeRadiusBonus);
    public float ChargedBowAoeRadius => Mathf.Max(0f, chargedBowAoeRadius * (1f + Mathf.Max(0f, bowAoeRadiusBonus)));
    public float FlatMaxHealthBonus => Mathf.Max(0f, flatMaxHealthBonus);

    public int CountDistinctArrowAugmentTypesOwnedForMutation()
    {
        int n = 0;
        for (int i = 0; i < ArrowAugmentIdsForAutomaticMutation.Length; i++)
        {
            if (GetAppliedCount(ArrowAugmentIdsForAutomaticMutation[i]) > 0)
                n++;
        }

        return n;
    }

    public bool HasRadialBowMutationUnlock =>
        cheatForceRadialArrowMutation ||
        CountDistinctArrowAugmentTypesOwnedForMutation() >= ArrowAutomaticMutationRequiredDistinctTypes;

    /// <summary>
    /// Test amaçlı: mutasyon için gerekli 5 farklı arrow augmenttan sonra true (aynı kosul <see cref="HasRadialBowMutationUnlock"/> ile).
    /// </summary>
    public bool MutatedArrowShots => HasRadialBowMutationUnlock;

    /// <summary>İleride kombo/policy buraya bağlanır (otomatik radialı iptal vb.).</summary>
    public bool ShouldUseRadialBowVolleyMutation(Player _)
    {
        return HasRadialBowMutationUnlock;
    }

    private void Awake()
    {
        _player = GetComponent<Player>();
        _initialChargedBowAoeRadius = chargedBowAoeRadius;
    }

    public void ResetAll()
    {
        movementSpeedBonus = 0f;
        hasChargedBowAoe = false;
        chargedBowAoeRadius = _initialChargedBowAoeRadius;
        hasDoubleArrowUnlock = false;
        hasWallLootsUnlock = false;
        hasExtraAugmentSlotUnlock = false;
        hasDashUnluck = false;
        dashCooldownMultiplier = 1f;
        luckMultiplier = 1f;
        hammerChargeMultiplier = 1f;
        dashDistanceMultiplier = 1f;
        incomingDamageReduction = 0f;
        hasHammerChargeDamageReductionUnlock = false;
        hammerFreezeDuration = 0f;
        bowFreezeDuration = 0f;
        hammerAoeRadiusBonus = 0f;
        bowAoeRadiusBonus = 0f;
        flatMaxHealthBonus = 0f;
        arrowShotBonusCount = 0;
        arrowProjectileSpeedMultiplier = 1f;
        outgoingDamageMultiplier = 1f;
        maxHealthMultiplier = 1f;
        _appliedAugmentCounts.Clear();
        _loggedAllArrowAugmentsComplete = false;
    }

    private void LateUpdate()
    {
        int real = CountDistinctArrowAugmentTypesOwnedForMutation();
        arrowMutationProgressMotor = cheatForceRadialArrowMutation
            ? Mathf.Max(real, ArrowAutomaticMutationRequiredDistinctTypes)
            : real;
        mutatedArrowShotsMotor = MutatedArrowShots;
    }

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
        int maxCount = GetMaxApplyCount(augment);
        return currentCount < maxCount;
    }

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
            case AugmentId.ChargedBowAoeUnlock:
                hasChargedBowAoe = true;
                if (augment.value > 0f)
                    chargedBowAoeRadius = Mathf.Max(chargedBowAoeRadius, augment.value);
                break;
            case AugmentId.DoubleArrowUnlock:
                hasDoubleArrowUnlock = true;
                break;
            case AugmentId.WallLootsUnlock:
                hasWallLootsUnlock = true;
                break;
            case AugmentId.ExtraAugmentSlotUnlock:
                hasExtraAugmentSlotUnlock = true;
                break;
            case AugmentId.DashUnluck:
                hasDashUnluck = true;
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
            case AugmentId.HammerChargeReduce_Common_I:
            case AugmentId.HammerChargeReduce_Common_II:
            case AugmentId.HammerChargeReduce_Rare:
            case AugmentId.HammerChargeReduce_Extraordinary:
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
            case AugmentId.HammerChargeDamageReductionUnlock:
                hasHammerChargeDamageReductionUnlock = true;
                break;
            case AugmentId.HammerFreeze_Common:
            case AugmentId.HammerFreeze_Rare:
            case AugmentId.HammerFreeze_Extraordinary:
                hammerFreezeDuration += Mathf.Max(0f, augment.value);
                break;
            case AugmentId.BowFreeze_Common:
            case AugmentId.BowFreeze_Rare:
            case AugmentId.BowFreeze_Extraordinary:
                bowFreezeDuration += Mathf.Max(0f, augment.value);
                break;
            case AugmentId.HammerAoeRadius_Common:
            case AugmentId.HammerAoeRadius_Rare:
            case AugmentId.HammerAoeRadius_Extraordinary:
                hammerAoeRadiusBonus += Mathf.Max(0f, augment.value);
                break;
            case AugmentId.BowAoeRadius_Common:
            case AugmentId.BowAoeRadius_Rare:
            case AugmentId.BowAoeRadius_Extraordinary:
                bowAoeRadiusBonus += Mathf.Max(0f, augment.value);
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
                maxHealthMultiplier *= 0.5f;
                break;
            case AugmentId.GlassCannonDoubleDamageHalveMaxHealth:
                outgoingDamageMultiplier *= 2f;
                maxHealthMultiplier *= 0.5f;
                break;
            case AugmentId.MaxHealthIncreasePercent:
                maxHealthMultiplier *= 1f + Mathf.Max(0f, augment.value);
                break;
            case AugmentId.ArrowCount_IncreaseNumberOfArrowsBy1:
            case AugmentId.ArrowCount_PlusOneArrows:
            case AugmentId.ArrowCount_IncreaseYourArrowsBy1:
                arrowShotBonusCount++;
                break;
            case AugmentId.ArrowCount_PlusOneAndSpeed10Percent:
            case AugmentId.ArrowCount_PlusOneAndSpeed15Percent:
                arrowShotBonusCount++;
                arrowProjectileSpeedMultiplier *= 1f + Mathf.Max(0f, augment.value);
                break;
        }

        _appliedAugmentCounts[augment.id] = GetAppliedCount(augment.id) + 1;
        AugmentApplied?.Invoke(augment);

        if (!Mathf.Approximately(prevMaxHpMult, maxHealthMultiplier))
            _player?.OnMaxHealthMultiplierChanged(prevMaxHpMult, maxHealthMultiplier);

        MaybeLogArrowAugmentSetComplete();
    }

    private void MaybeLogArrowAugmentSetComplete()
    {
        if (_loggedAllArrowAugmentsComplete) return;
        if (CountDistinctArrowAugmentTypesOwnedForMutation() < ArrowAutomaticMutationRequiredDistinctTypes)
            return;

        _loggedAllArrowAugmentsComplete = true;
    }

    private static int GetMaxApplyCount(AugmentDefinition augment)
    {
        if (augment == null) return 0;
        if (IsUnlockAugment(augment.id)) return 1;

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

    private static bool IsUnlockAugment(AugmentId id)
    {
        switch (id)
        {
            case AugmentId.ChargedBowAoeUnlock:
            case AugmentId.WallLootsUnlock:
            case AugmentId.DashUnluck:
            case AugmentId.HammerChargeDamageReductionUnlock:
                return true;
            default:
                return false;
        }
    }

    private static int GetMovementSpeedMaxApplyCountFromRarity(int rarity)
    {
        switch (rarity)
        {
            case 1: return 3;
            case 2: return 2;
            case 3: return 1;
            default: return 1;
        }
    }

    private bool MeetsAugmentPrerequisites(AugmentId id)
    {
        switch (id)
        {
            case AugmentId.WallLootsUnlock:
                return hasChargedBowAoe;
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
            case AugmentId.BowAoeRadius_Common:
            case AugmentId.BowAoeRadius_Rare:
            case AugmentId.BowAoeRadius_Extraordinary:
                return hasChargedBowAoe;
            default:
                return true;
        }
    }
}
