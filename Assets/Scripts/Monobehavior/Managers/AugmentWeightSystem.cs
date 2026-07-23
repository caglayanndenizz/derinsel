using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Offer cycle (each level-up is one offer):
///   Every unlockOfferInterval-th offer → unlock-only offer (UnlockAugmentDatabase pool).
///   All other offers                   → T1-only regular offer (AugmentDatabase.regularAugments).
///
/// T2/T3 augments are only available via chests (BuildChestOffer).
///
/// Regular/chest tiers use a "skip weight" system: each time a regular augment is shown in an
/// offer but not picked, its weight rises by <see cref="weightIncrementPerSkip"/> (baseline 1).
/// Once an augment has been skipped <see cref="maxSkipsBeforeExclusion"/> times it is dropped
/// from the pool entirely for the rest of the run. Unlock offers are always pure uniform random —
/// they mark the start of a weapon's mutation path and are intentionally left untouched by this.
/// </summary>
public class AugmentWeightSystem : MonoBehaviour
{
    public static AugmentWeightSystem Instance { get; private set; }

    [Header("References")]
    [SerializeField] private AugmentDatabase      augmentDatabase;
    [Tooltip("Direct override for the unlock pool. Falls back to augmentDatabase.unlockDatabase when null.")]
    [SerializeField] private UnlockAugmentDatabase unlockDatabase;

    [Header("Offer Cycle")]
    [Tooltip("Every Nth level-up is an unlock-only offer. 3 = offers 3, 6, 9... show only unlocks.")]
    [SerializeField] private int unlockOfferInterval = 3;

    [Header("Skip Weight (regular/chest tiers only — unlock offers stay uniform random)")]
    [Tooltip("Weight added each time a regular augment is shown but not picked. Baseline weight is 1.")]
    [SerializeField] private float weightIncrementPerSkip = 0.1f;
    [Tooltip("Once an augment has been skipped this many times, it is removed from the pool for the rest of the run.")]
    [SerializeField] private int maxSkipsBeforeExclusion = 3;

    [Header("Runtime State (Read-Only)")]
    [SerializeField] private int _totalOfferCount;
    [SerializeField] private int _regularOfferCount;

    private readonly Dictionary<AugmentId, int> _skipCounts = new Dictionary<AugmentId, int>();

    // ── Public Read ────────────────────────────────────────────────────────────

    public int TotalOfferCount   => _totalOfferCount;
    public int RegularOfferCount => _regularOfferCount;

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
    /// Builds an offer containing only augments of the given rarity tier.
    /// Used by the chest system — does not advance offer counters.
    /// </summary>
    public List<AugmentDefinition> BuildChestOffer(int rarity, PlayerAugmentController controller, int slotCount)
    {
        var result  = new List<AugmentDefinition>(slotCount);
        var usedIds = new HashSet<AugmentId>();

        for (int i = 0; i < slotCount; i++)
        {
            List<AugmentDefinition> candidates = BuildTierCandidates(rarity, controller, usedIds);
            if (candidates.Count == 0) break;
            AugmentDefinition pick = PickWeightedBySkip(candidates);
            result.Add(pick);
            usedIds.Add(pick.id);
        }
        return result;
    }

    /// <summary>
    /// Builds an augment offer for one level-up selection.
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
                _regularOfferCount++;
                result = BuildRegularOffer(controller, slotCount);
            }
        }
        else
        {
            _regularOfferCount++;
            result = BuildRegularOffer(controller, slotCount);
        }

        return result;
    }

    /// <summary>
    /// Call after the player resolves an offer. Every regular augment that was shown but not
    /// picked accumulates skip weight. Unlock offers are ignored (their items are
    /// <see cref="UnlockAugmentDefinition"/> and are filtered out below), so they stay untouched.
    /// </summary>
    public void RecordOfferOutcome(AugmentDefinition selected, List<AugmentDefinition> fullOffer)
    {
        if (fullOffer == null) return;
        foreach (AugmentDefinition aug in fullOffer)
        {
            if (aug == null || aug.id == AugmentId.None) continue;
            if (aug is UnlockAugmentDefinition) continue;
            if (selected != null && aug.id == selected.id) continue;

            _skipCounts.TryGetValue(aug.id, out int count);
            _skipCounts[aug.id] = count + 1;
        }
    }

    /// <summary>Resets all offer counters and skip weights. Call at the start of a new run.</summary>
    public void Reset()
    {
        _totalOfferCount   = 0;
        _regularOfferCount = 0;
        _skipCounts.Clear();

        // Also reset weapon mutation counters
        WeaponMutationChecker.Instance?.ResetAll();
    }

    // ── Offer Builders ─────────────────────────────────────────────────────────

    public List<AugmentDefinition> BuildUnlockOffer(PlayerAugmentController controller, int slotCount)
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
            AugmentDefinition pick = PickUniformRandom(candidates);
            result.Add(pick);
            candidates.Remove(pick);
            usedIds.Add(pick.id);
        }
        return result;
    }

    private List<AugmentDefinition> BuildRegularOffer(PlayerAugmentController controller, int slotCount)
    {
        var result  = new List<AugmentDefinition>(slotCount);
        var usedIds = new HashSet<AugmentId>();

        for (int i = 0; i < slotCount; i++)
        {
            AugmentDefinition pick = PickFromTier(1, controller, usedIds);
            if (pick == null) break;
            result.Add(pick);
            usedIds.Add(pick.id);
        }
        return result;
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

        return PickWeightedBySkip(candidates);
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
            if (aug is UnlockAugmentDefinition) continue;
            if (aug.rarity != tier) continue;
            if (!IsEligible(aug, controller, usedIds)) continue;
            if (IsExcludedBySkipLimit(aug.id)) continue;
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
            if (aug is UnlockAugmentDefinition) continue;
            if (!IsEligible(aug, controller, usedIds)) continue;
            if (IsExcludedBySkipLimit(aug.id)) continue;
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

        // Weapon mutation check — remove all unlock augments for the weapon if its threshold is reached
        if (aug is UnlockAugmentDefinition unlockAug)
        {
            WeaponMutationChecker checker = WeaponMutationChecker.Instance;
            if (checker != null && checker.ShouldExcludeWeaponUnlocks(unlockAug.weaponType))
                return false;

            // Do not show charge-dependent hammer augments without the hammer charge unlock
            if (unlockAug.weaponType == WeaponType.Hammer &&
                unlockAug.id != AugmentId.HammerChargeUnlock &&
                controller != null && !controller.HasHammerChargeUnlock)
                return false;
        }

        return true;
    }

    // ── Skip Weight ───────────────────────────────────────────────────────────

    private float GetSkipWeight(AugmentId id)
    {
        _skipCounts.TryGetValue(id, out int skips);
        return 1f + weightIncrementPerSkip * skips;
    }

    private bool IsExcludedBySkipLimit(AugmentId id)
    {
        _skipCounts.TryGetValue(id, out int skips);
        return maxSkipsBeforeExclusion > 0 && skips >= maxSkipsBeforeExclusion;
    }

    /// <summary>Weighted pick (without replacement) — augments skipped more often are more likely.</summary>
    private AugmentDefinition PickWeightedBySkip(List<AugmentDefinition> candidates)
    {
        if (candidates.Count == 1) return candidates[0];

        float total = 0f;
        var weights = new float[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            weights[i] = GetSkipWeight(candidates[i].id);
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

    // ── Uniform Random (unlock offers only) ─────────────────────────────────────

    /// <summary>Picks one candidate with equal probability — no weighting, no bias.</summary>
    private static AugmentDefinition PickUniformRandom(List<AugmentDefinition> candidates)
    {
        if (candidates.Count == 1) return candidates[0];
        return candidates[Random.Range(0, candidates.Count)];
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static readonly AugmentId[][] ExclusiveOfferGroups =
    {
        new[] { AugmentId.LuckIncrease_Common_I, AugmentId.LuckIncrease_Common_II, AugmentId.LuckIncrease_Common_III },
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
