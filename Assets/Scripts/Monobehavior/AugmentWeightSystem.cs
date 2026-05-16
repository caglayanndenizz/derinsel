using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime augment weight system.
/// effective_weight = base_weight × pity_multiplier × rejection_multiplier × floor_multiplier
///
/// Floor gates:
///   T1 only    : floors 1 … (t2UnlockFloor - 1)
///   T2 enters  : floor t2UnlockFloor+ with slot probability = (floor/t2UnlockFloor) × t2SlotChancePerInterval
///   T3 enters  : floor t3UnlockFloor+  (milestone composition fully active)
///   Unlock augments bypass ALL floor gates.
///
/// Pity      — T3 only. After pityThresholdRounds offers without a T3 in the pool, T3 weight
///             increases by pityIncrementPerRound each subsequent offer. Resets when T3 appears.
/// Rejection — Per augment ID. When skipped, multiplier drops to rejectionStartMultiplier
///             and recovers by rejectionRecoveryPerRound per offer back towards 1.0.
/// Floor     — T3 only. Soft linear scale: 1 + floor * t3FloorScalePerFloor.
/// </summary>
public class AugmentWeightSystem : MonoBehaviour
{
    public static AugmentWeightSystem Instance { get; private set; }

    [Header("References")]
    [SerializeField] private AugmentDatabase augmentDatabase;

    [Header("Tier Base Weights (rarity fallback)")]
    [SerializeField] private float t1BaseWeight = 100f;
    [SerializeField] private float t2BaseWeight = 35f;
    [SerializeField] private float t3BaseWeight = 8f;

    [Header("Floor Gates")]
    [Tooltip("First floor where T2 augments can appear.")]
    [SerializeField] private int t2UnlockFloor = 5;
    [Tooltip("Probability that a T2 slot is granted per interval above t2UnlockFloor. 0.15 = +15% per interval of 5 floors.")]
    [SerializeField] private float t2SlotChancePerInterval = 0.15f;
    [Tooltip("First floor where T3 augments can appear. Milestone composition fully active from here.")]
    [SerializeField] private int t3UnlockFloor = 15;

    [Header("Pity (T3 only)")]
    [Tooltip("Offers without any T3 in pool before pity activates.")]
    [SerializeField] private int pityThresholdRounds = 5;
    [Tooltip("Pity multiplier added per offer after threshold. +0.20 = +20% each offer.")]
    [SerializeField] private float pityIncrementPerRound = 0.20f;

    [Header("Rejection (per augment ID)")]
    [Tooltip("Multiplier immediately after rejection.")]
    [SerializeField] private float rejectionStartMultiplier = 0.30f;
    [Tooltip("Recovery per offer back toward 1.0. At 0.14 with start=0.30 → full in 5 offers.")]
    [SerializeField] private float rejectionRecoveryPerRound = 0.14f;

    [Header("Floor Scaling (T3 only)")]
    [Tooltip("Per-floor linear bonus on T3 weight. 0.05 = +5% per floor.")]
    [SerializeField] private float t3FloorScalePerFloor = 0.05f;

    [Header("Milestone Floors")]
    [SerializeField] private int milestoneFloorInterval = 5;

    // ── Runtime State (visible in Inspector for in-editor monitoring) ──────
    [Header("Runtime State (Read-Only)")]
    [SerializeField] private int offersWithoutT3;
    [SerializeField] private int totalOffers;

    private readonly Dictionary<AugmentId, float> _rejectionMult   = new Dictionary<AugmentId, float>();
    private readonly Dictionary<AugmentId, int>   _rejectedAtOffer = new Dictionary<AugmentId, int>();

    // ── Public Read Access ─────────────────────────────────────────────────
    public IReadOnlyList<AugmentDefinition> AllAugments =>
        (IReadOnlyList<AugmentDefinition>)(augmentDatabase?.allAugments) ??
        System.Array.Empty<AugmentDefinition>();

    public int OffersWithoutT3 => offersWithoutT3;
    public int TotalOffers     => totalOffers;

    public float CurrentPityMultiplier
    {
        get
        {
            if (offersWithoutT3 < pityThresholdRounds) return 1f;
            int excess = offersWithoutT3 - pityThresholdRounds + 1;
            return 1f + excess * pityIncrementPerRound;
        }
    }

    // ── Unity ──────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Main API ───────────────────────────────────────────────────────────

    public bool IsMilestoneFloor(int floor) =>
        floor > 0 && milestoneFloorInterval > 0 && floor % milestoneFloorInterval == 0;

    /// <summary>
    /// Builds a weighted augment offer following composition rules.
    /// Updates pity state after building (current offer uses pre-update state).
    /// </summary>
    public List<AugmentDefinition> BuildOffer(
        PlayerAugmentController controller,
        int slotCount,
        int currentFloor)
    {
        if (slotCount <= 0) return new List<AugmentDefinition>();

        List<int> tierSlots = GetTierSlotComposition(slotCount, IsMilestoneFloor(currentFloor), currentFloor);
        var result  = new List<AugmentDefinition>(slotCount);
        var usedIds = new HashSet<AugmentId>();

        foreach (int tier in tierSlots)
        {
            AugmentDefinition pick = PickFromTier(tier, controller, usedIds, currentFloor);
            if (pick == null) break;
            result.Add(pick);
            usedIds.Add(pick.id);
        }

        bool hadT3 = false;
        foreach (AugmentDefinition a in result)
        {
            if (a != null && a.rarity == 3) { hadT3 = true; break; }
        }
        offersWithoutT3 = hadT3 ? 0 : offersWithoutT3 + 1;
        totalOffers++;

        return result;
    }

    /// <summary>
    /// Call when player selects an augment. All other augments in the offer are marked rejected.
    /// </summary>
    public void NotifySelection(AugmentDefinition selected, List<AugmentDefinition> fullOffer)
    {
        if (fullOffer == null) return;
        foreach (AugmentDefinition aug in fullOffer)
        {
            if (aug == null || aug.id == AugmentId.None) continue;
            if (selected != null && aug.id == selected.id) continue;
            _rejectionMult[aug.id]   = rejectionStartMultiplier;
            _rejectedAtOffer[aug.id] = totalOffers;
        }
    }

    /// <summary>effective_weight = base × pity × rejection × floor</summary>
    public float GetEffectiveWeight(AugmentDefinition augment, int currentFloor)
    {
        if (augment == null || augment.id == AugmentId.None) return 0f;
        return GetBaseWeight(augment)
            * PityMultiplier(augment.rarity)
            * CurrentRejectionMultiplier(augment.id)
            * FloorMultiplier(augment.rarity, currentFloor);
    }

    // ── Private Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Returns slot tier list respecting floor gates.
    ///   Floor  1 – (t2UnlockFloor-1) : all T1
    ///   Floor  t2UnlockFloor+        : T2 slots gated by probability (15% per 5-floor interval)
    ///   Floor  t3UnlockFloor+        : full milestone / normal composition
    /// Unlock augments bypass floor gates via IsEligible, so they can appear in any slot.
    /// </summary>
    private List<int> GetTierSlotComposition(int slotCount, bool isMilestone, int floor)
    {
        var slots = new List<int>(slotCount);

        bool t2Available = floor >= t2UnlockFloor;
        bool t3Available = floor >= t3UnlockFloor;

        if (!t2Available)
        {
            for (int i = 0; i < slotCount; i++) slots.Add(1);
            return slots;
        }

        if (t3Available)
        {
            // Full composition rules
            if (isMilestone)
            {
                slots.Add(1); slots.Add(2); slots.Add(3);
                for (int i = 3; i < slotCount; i++) slots.Add(1);
            }
            else
            {
                slots.Add(1); slots.Add(1); slots.Add(2);
                for (int i = 3; i < slotCount; i++) slots.Add(2);
            }
            return slots;
        }

        // T2 available, T3 gated: T2 slots determined by probability roll
        // First two slots always T1; remaining slots probabilistically T2
        slots.Add(1);
        if (slotCount >= 2) slots.Add(1);
        for (int i = 2; i < slotCount; i++)
            slots.Add(RollT2Slot(floor) ? 2 : 1);

        return slots;
    }

    // floor=5 → interval=1 → 15%, floor=10 → interval=2 → 30%, etc.
    private bool RollT2Slot(int floor)
    {
        int interval = floor / t2UnlockFloor;
        float probability = Mathf.Clamp01(interval * t2SlotChancePerInterval);
        return Random.value < probability;
    }

    private AugmentDefinition PickFromTier(
        int tier,
        PlayerAugmentController controller,
        HashSet<AugmentId> usedIds,
        int currentFloor)
    {
        List<AugmentDefinition> candidates = CandidatesForTier(tier, controller, usedIds, currentFloor);

        // Fallback cascade: adjacent tier → any available
        if (candidates.Count == 0)
        {
            int fallback = tier < 3 ? tier + 1 : tier - 1;
            candidates = CandidatesForTier(fallback, controller, usedIds, currentFloor);
        }
        if (candidates.Count == 0)
            candidates = AllCandidates(controller, usedIds, currentFloor);
        if (candidates.Count == 0)
            return null;

        return WeightedRandom(candidates, currentFloor);
    }

    private List<AugmentDefinition> CandidatesForTier(
        int tier,
        PlayerAugmentController controller,
        HashSet<AugmentId> usedIds,
        int floor)
    {
        var list = new List<AugmentDefinition>();
        if (augmentDatabase?.allAugments == null) return list;
        foreach (AugmentDefinition aug in augmentDatabase.allAugments)
        {
            if (!IsEligible(aug, controller, usedIds, floor)) continue;
            // Accept exact tier match OR unlock augments (they bypass tier-slot restriction)
            if (aug.rarity == tier || IsUnlockAugment(aug.id))
                list.Add(aug);
        }
        return list;
    }

    private List<AugmentDefinition> AllCandidates(
        PlayerAugmentController controller,
        HashSet<AugmentId> usedIds,
        int floor)
    {
        var list = new List<AugmentDefinition>();
        if (augmentDatabase?.allAugments == null) return list;
        foreach (AugmentDefinition aug in augmentDatabase.allAugments)
        {
            if (!IsEligible(aug, controller, usedIds, floor)) continue;
            list.Add(aug);
        }
        return list;
    }

    private bool IsEligible(
        AugmentDefinition aug,
        PlayerAugmentController controller,
        HashSet<AugmentId> usedIds,
        int floor)
    {
        if (aug == null || aug.id == AugmentId.None) return false;
        if (usedIds.Contains(aug.id)) return false;
        if (SharesExclusiveGroupWithUsed(aug.id, usedIds)) return false;
        if (controller != null && !controller.CanApplyAugment(aug)) return false;
        if (controller != null
            && controller.HasRadialBowMutationUnlock
            && aug.excludeFromAugmentPickerWhenRadialBowMutationComplete) return false;
        if (!MeetsFloorRequirement(aug, floor)) return false;
        return true;
    }

    private bool MeetsFloorRequirement(AugmentDefinition aug, int floor)
    {
        if (IsUnlockAugment(aug.id)) return true;
        if (aug.rarity >= 3 && floor < t3UnlockFloor) return false;
        if (aug.rarity >= 2 && floor < t2UnlockFloor) return false;
        return true;
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

    // Augments in the same group cannot appear together in a single offer.
    private static readonly AugmentId[][] ExclusiveOfferGroups =
    {
        new[] { AugmentId.LuckIncrease_Common_I, AugmentId.LuckIncrease_Common_II, AugmentId.LuckIncrease_Common_III },
        new[] { AugmentId.DashCooldownReduce_Common_I, AugmentId.DashCooldownReduce_Common_II },
        new[] { AugmentId.HammerChargeReduce_Common_I, AugmentId.HammerChargeReduce_Common_II },
        new[] { AugmentId.DashDistanceIncrease_Uncommon_I, AugmentId.DashDistanceIncrease_Uncommon_II, AugmentId.DashDistanceIncrease_Uncommon_III },
        new[]
        {
            AugmentId.ArrowCount_IncreaseNumberOfArrowsBy1,
            AugmentId.ArrowCount_PlusOneArrows,
            AugmentId.ArrowCount_PlusOneAndSpeed10Percent,
            AugmentId.ArrowCount_PlusOneAndSpeed15Percent,
            AugmentId.ArrowCount_IncreaseYourArrowsBy1,
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

    private AugmentDefinition WeightedRandom(List<AugmentDefinition> candidates, int currentFloor)
    {
        if (candidates.Count == 1) return candidates[0];

        float total = 0f;
        var weights = new float[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            weights[i] = Mathf.Max(0f, GetEffectiveWeight(candidates[i], currentFloor));
            total += weights[i];
        }

        if (total <= 0f) return candidates[Random.Range(0, candidates.Count)];

        float roll = Random.value * total;
        float cumulative = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative) return candidates[i];
        }
        return candidates[candidates.Count - 1];
    }

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

    private float PityMultiplier(int rarity)
    {
        if (rarity != 3 || offersWithoutT3 < pityThresholdRounds) return 1f;
        int excess = offersWithoutT3 - pityThresholdRounds + 1;
        return 1f + excess * pityIncrementPerRound;
    }

    private float CurrentRejectionMultiplier(AugmentId id)
    {
        if (!_rejectionMult.TryGetValue(id, out float startMult)) return 1f;
        if (!_rejectedAtOffer.TryGetValue(id, out int rejectedAt))  return 1f;

        int offersSince = totalOffers - rejectedAt;
        float recovered = startMult + offersSince * rejectionRecoveryPerRound;
        if (recovered >= 1f)
        {
            _rejectionMult.Remove(id);
            _rejectedAtOffer.Remove(id);
            return 1f;
        }
        return recovered;
    }

    private float FloorMultiplier(int rarity, int currentFloor) =>
        rarity == 3 ? 1f + Mathf.Max(0, currentFloor) * t3FloorScalePerFloor : 1f;
}
