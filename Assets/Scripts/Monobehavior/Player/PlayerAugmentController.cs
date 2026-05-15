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
    public float ChargedBowAoeRadius => Mathf.Max(0f, chargedBowAoeRadius);
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
                return hasDashUnluck;
            default:
                return true;
        }
    }
}
