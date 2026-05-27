using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Offer cycle (each level-up is one offer):
///   Every unlockOfferInterval-th offer → unlock-only offer (UnlockAugmentDatabase pool).
///   All other offers                   → regular stat offer (AugmentDatabase.regularAugments).
///
/// Regular offer tier progression — gated by regular offer count, NOT floor or player level:
///   0 .. t2UnlockAfterOffers-1               → all T1
///   t2UnlockAfterOffers .. t3UnlockAfterOffers-1 → T1 + T2
///   t3UnlockAfterOffers+                     → T1 + T2 + T3
///
/// Pity: after pityThresholdOffers total level-up offers without any T3 AND T3 pool is open,
///       the next regular offer forces ALL slots to T3.
/// Rejection: per-ID weight multiplier that drops on skip and recovers over subsequent offers.
/// </summary>
public class AugmentWeightSystem : MonoBehaviour
{
    public static AugmentWeightSystem Instance { get; private set; }

    [Header("References")]
    [SerializeField] private AugmentDatabase      augmentDatabase;
    [Tooltip("Direct override for the unlock pool. Falls back to augmentDatabase.unlockDatabase when null.")]
    [SerializeField] private UnlockAugmentDatabase unlockDatabase;

    [Header("Tier Base Weights")]
    [SerializeField] private float t1BaseWeight = 100f;
    [SerializeField] private float t2BaseWeight = 35f;
    [SerializeField] private float t3BaseWeight = 8f;

    [Header("Offer Cycle")]
    [Tooltip("Every Nth level-up is an unlock-only offer. 3 = offers 3, 6, 9... show only unlocks.")]
    [SerializeField] private int unlockOfferInterval = 3;

    [Header("Tier Unlock (by regular offer count — floor/level independent)")]
    [Tooltip("T2 augments enter the pool after this many regular (non-unlock) offers.")]
    [SerializeField] private int t2UnlockAfterOffers = 3;
    [Tooltip("T3 augments enter the pool after this many regular offers.")]
    [SerializeField] private int t3UnlockAfterOffers = 8;

    [Header("Pity")]
    [Tooltip("After this many level-up offers (regular + unlock) without a T3, the next regular offer forces all slots to T3.")]
    [SerializeField] private int pityThresholdOffers = 4;

    [Header("Rejection (per augment ID)")]
    [Tooltip("Weight multiplier immediately after the augment is skipped.")]
    [SerializeField] private float rejectionStartMultiplier = 0.30f;
    [Tooltip("Multiplier recovery per offer back toward 1.0. At 0.14 with start=0.30 → full in ~5 offers.")]
    [SerializeField] private float rejectionRecoveryPerRound = 0.14f;

    [Header("Runtime State (Read-Only)")]
    [SerializeField] private int _totalOfferCount;
    [SerializeField] private int _regularOfferCount;
    [SerializeField] private int _offersSinceLastT3;

    private readonly Dictionary<AugmentId, float> _rejectionMult   = new Dictionary<AugmentId, float>();
    private readonly Dictionary<AugmentId, int>   _rejectedAtOffer = new Dictionary<AugmentId, int>();

    // ── Public Read ────────────────────────────────────────────────────────────

    public IReadOnlyList<AugmentDefinition> AllAugments =>
        (IReadOnlyList<AugmentDefinition>)(augmentDatabase?.regularAugments) ??
        System.Array.Empty<AugmentDefinition>();

    public int  TotalOfferCount   => _totalOfferCount;
    public int  RegularOfferCount => _regularOfferCount;
    public int  OffersSinceLastT3 => _offersSinceLastT3;
    public int  PityThreshold     => pityThresholdOffers;
    public bool IsPityActive      => _offersSinceLastT3 >= pityThresholdOffers
                                     && _regularOfferCount >= t3UnlockAfterOffers;

    /// <summary>True if the NEXT call to BuildOffer will produce an unlock offer.</summary>
    public bool IsNextOfferUnlock =>
        unlockOfferInterval > 0 && (_totalOfferCount + 1) % unlockOfferInterval == 0;

    // ── Unity ──────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Main API ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a weighted augment offer for one level-up selection.
    /// Automatically decides whether this is an unlock offer or a regular tier offer.
    /// </summary>
    public List<AugmentDefinition> BuildOffer(PlayerAugmentController controller, int slotCount)
    {
        if (slotCount <= 0) return new List<AugmentDefinition>();

        _totalOfferCount++;
        bool isUnlockOffer = unlockOfferInterval > 0 && _totalOfferCount % unlockOfferInterval == 0;

        List<AugmentDefinition> result;
        if (isUnlockOffer)
        {
            result = BuildUnlockOffer(controller, slotCount);
            if (result.Count == 0)
            {
                // No eligible unlocks — fall back to regular offer
                _regularOfferCount++;
                result = BuildRegularOffer(controller, slotCount, pityActive: false);
            }
        }
        else
        {
            _regularOfferCount++;
            result = BuildRegularOffer(controller, slotCount, pityActive: IsPityActive);
        }

        // Pity counter only advances on regular offers — unlock offers can never contain T3
        if (!isUnlockOffer)
        {
            bool hadT3 = false;
            foreach (AugmentDefinition a in result)
                if (a != null && a.rarity == 3) { hadT3 = true; break; }
            _offersSinceLastT3 = hadT3 ? 0 : _offersSinceLastT3 + 1;
        }

        return result;
    }

    /// <summary>
    /// Call after the player selects an augment. All other augments in the offer receive a
    /// temporary weight penalty.
    /// </summary>
    public void NotifySelection(AugmentDefinition selected, List<AugmentDefinition> fullOffer)
    {
        if (fullOffer == null) return;
        foreach (AugmentDefinition aug in fullOffer)
        {
            if (aug == null || aug.id == AugmentId.None) continue;
            if (selected != null && aug.id == selected.id) continue;
            _rejectionMult[aug.id]   = rejectionStartMultiplier;
            _rejectedAtOffer[aug.id] = _totalOfferCount;
        }
    }

    /// <summary>effective_weight = base_weight × rejection_multiplier</summary>
    public float GetEffectiveWeight(AugmentDefinition augment)
    {
        if (augment == null || augment.id == AugmentId.None) return 0f;
        return GetBaseWeight(augment) * CurrentRejectionMultiplier(augment.id);
    }

    /// <summary>Resets all offer counters and rejection state. Call at the start of a new run.</summary>
    public void Reset()
    {
        _totalOfferCount   = 0;
        _regularOfferCount = 0;
        _offersSinceLastT3 = 0;
        _rejectionMult.Clear();
        _rejectedAtOffer.Clear();

        // Silah mutasyon sayaçlarını da sıfırla
        WeaponMutationChecker.Instance?.ResetAll();
    }

    // ── Offer Builders ─────────────────────────────────────────────────────────

    private List<AugmentDefinition> BuildUnlockOffer(PlayerAugmentController controller, int slotCount)
    {
        var result     = new List<AugmentDefinition>();
        var candidates = new List<AugmentDefinition>();
        var usedIds    = new HashSet<AugmentId>();

        UnlockAugmentDatabase db = unlockDatabase != null ? unlockDatabase : augmentDatabase?.unlockDatabase;
        if (db == null) return result;

        foreach (UnlockAugmentDefinition aug in db.GetAllUnlocks())
        {
            if (!IsEligible(aug, controller, usedIds)) continue;
            candidates.Add(aug);
        }

        while (result.Count < slotCount && candidates.Count > 0)
        {
            AugmentDefinition pick = WeightedRandom(candidates);
            result.Add(pick);
            candidates.Remove(pick);
            usedIds.Add(pick.id);
        }
        return result;
    }

    private List<AugmentDefinition> BuildRegularOffer(
        PlayerAugmentController controller,
        int slotCount,
        bool pityActive)
    {
        var result  = new List<AugmentDefinition>(slotCount);
        var usedIds = new HashSet<AugmentId>();

        List<int> tierSlots = pityActive
            ? BuildAllT3Slots(slotCount)
            : GetRegularTierSlots(slotCount);

        foreach (int tier in tierSlots)
        {
            AugmentDefinition pick = PickFromTier(tier, controller, usedIds);
            if (pick == null) break;
            result.Add(pick);
            usedIds.Add(pick.id);
        }
        return result;
    }

    /// <summary>
    /// Slot tier composition by regular offer count.
    /// Slot 0 → T1 always.
    /// Slot 1 → T2 once t2UnlockAfterOffers passed, else T1.
    /// Slot 2 → T3 once t3UnlockAfterOffers passed, else T2 (or T1).
    /// Extra  → T1.
    /// </summary>
    private List<int> GetRegularTierSlots(int slotCount)
    {
        bool t2 = _regularOfferCount >= t2UnlockAfterOffers;
        bool t3 = _regularOfferCount >= t3UnlockAfterOffers;

        var slots = new List<int>(slotCount);
        for (int i = 0; i < slotCount; i++)
        {
            switch (i)
            {
                case 0:  slots.Add(1); break;
                case 1:  slots.Add(t2 ? 2 : 1); break;
                case 2:  slots.Add(t3 ? 3 : (t2 ? 2 : 1)); break;
                default: slots.Add(1); break;
            }
        }
        return slots;
    }

    private static List<int> BuildAllT3Slots(int slotCount)
    {
        var slots = new List<int>(slotCount);
        for (int i = 0; i < slotCount; i++) slots.Add(3);
        return slots;
    }

    // ── Tier Picking ───────────────────────────────────────────────────────────

    private AugmentDefinition PickFromTier(
        int tier,
        PlayerAugmentController controller,
        HashSet<AugmentId> usedIds)
    {
        List<AugmentDefinition> candidates = BuildTierCandidates(tier, controller, usedIds);

        // Fallback: step down a tier, then accept any eligible regular augment
        if (candidates.Count == 0 && tier > 1)
            candidates = BuildTierCandidates(tier - 1, controller, usedIds);
        if (candidates.Count == 0)
            candidates = BuildAllRegularCandidates(controller, usedIds);
        if (candidates.Count == 0)
            return null;

        return WeightedRandom(candidates);
    }

    private List<AugmentDefinition> BuildTierCandidates(
        int tier,
        PlayerAugmentController controller,
        HashSet<AugmentId> usedIds)
    {
        var list = new List<AugmentDefinition>();
        if (augmentDatabase?.regularAugments == null) return list;
        foreach (AugmentDefinition aug in augmentDatabase.regularAugments)
        {
            if (aug.rarity != tier) continue;
            if (!IsEligible(aug, controller, usedIds)) continue;
            list.Add(aug);
        }
        return list;
    }

    private List<AugmentDefinition> BuildAllRegularCandidates(
        PlayerAugmentController controller,
        HashSet<AugmentId> usedIds)
    {
        var list = new List<AugmentDefinition>();
        if (augmentDatabase?.regularAugments == null) return list;
        foreach (AugmentDefinition aug in augmentDatabase.regularAugments)
        {
            if (!IsEligible(aug, controller, usedIds)) continue;
            list.Add(aug);
        }
        return list;
    }

    private bool IsEligible(
        AugmentDefinition aug,
        PlayerAugmentController controller,
        HashSet<AugmentId> usedIds)
    {
        if (aug == null || aug.id == AugmentId.None) return false;
        if (usedIds.Contains(aug.id)) return false;
        if (SharesExclusiveGroupWithUsed(aug.id, usedIds)) return false;
        if (controller != null && !controller.CanApplyAugment(aug)) return false;
        if (controller != null
            && controller.HasRadialLongbowMutationUnlock
            && aug.excludeFromAugmentPickerWhenRadialLongbowMutationComplete) return false;

        // Weapon mutation check — eşik dolmuşsa o silahın tüm unlock augmentlerini havuzdan çıkar
        if (aug is UnlockAugmentDefinition unlockAug)
        {
            WeaponMutationChecker checker = WeaponMutationChecker.Instance;
            if (checker != null && checker.ShouldExcludeWeaponUnlocks(unlockAug.weaponType))
                return false;
        }

        return true;
    }

    // ── Weighted Random ────────────────────────────────────────────────────────

    private AugmentDefinition WeightedRandom(List<AugmentDefinition> candidates)
    {
        if (candidates.Count == 1) return candidates[0];

        float total = 0f;
        var weights = new float[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            weights[i] = Mathf.Max(0f, GetEffectiveWeight(candidates[i]));
            total += weights[i];
        }

        if (total <= 0f) return candidates[Random.Range(0, candidates.Count)];

        float roll       = Random.value * total;
        float cumulative = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative) return candidates[i];
        }
        return candidates[candidates.Count - 1];
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private float GetBaseWeight(AugmentDefinition aug)
    {
        if (aug.baseWeight > 0f) return aug.baseWeight;
        switch (aug.rarity)
        {
            case 1:  return t1BaseWeight;
            case 2:  return t2BaseWeight;
            case 3:  return t3BaseWeight;
            default: return t1BaseWeight;
        }
    }

    private float CurrentRejectionMultiplier(AugmentId id)
    {
        if (!_rejectionMult.TryGetValue(id, out float startMult)) return 1f;
        if (!_rejectedAtOffer.TryGetValue(id, out int rejectedAt))  return 1f;

        int   offersSince = _totalOfferCount - rejectedAt;
        float recovered   = startMult + offersSince * rejectionRecoveryPerRound;
        if (recovered >= 1f)
        {
            _rejectionMult.Remove(id);
            _rejectedAtOffer.Remove(id);
            return 1f;
        }
        return recovered;
    }

    private static readonly AugmentId[][] ExclusiveOfferGroups =
    {
        new[] { AugmentId.LuckIncrease_Common_I, AugmentId.LuckIncrease_Common_II, AugmentId.LuckIncrease_Common_III },
        new[] { AugmentId.DashCooldownReduce_Common_I, AugmentId.DashCooldownReduce_Common_II },
        new[] { AugmentId.DashDistanceIncrease_Uncommon_I, AugmentId.DashDistanceIncrease_Uncommon_II, AugmentId.DashDistanceIncrease_Uncommon_III },
        new[]
        {
            AugmentId.ProjectileCount_PlusOneProjectiles,
            AugmentId.ProjectileCount_PlusOneAndSpeed10Percent,
            AugmentId.ProjectileCount_PlusOneAndSpeed15Percent,
        },
        new[]
        {
            AugmentId.MovementSpeedIncreaseCommon,
            AugmentId.MovementSpeedIncreaseRare,
            AugmentId.MovementSpeedIncreaseExtraordinary,
        },
    };

    private static bool SharesExclusiveGroupWithUsed(AugmentId id, HashSet<AugmentId> usedIds)
    {
        if (usedIds.Count == 0) return false;
        foreach (AugmentId[] group in ExclusiveOfferGroups)
        {
            bool idInGroup = false;
            foreach (AugmentId gid in group)
                if (gid == id) { idInGroup = true; break; }
            if (!idInGroup) continue;
            foreach (AugmentId gid in group)
                if (usedIds.Contains(gid)) return true;
        }
        return false;
    }
}
