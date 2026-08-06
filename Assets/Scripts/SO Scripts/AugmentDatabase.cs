using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Master augment database with two distinct sections:
///   1. Regular Augments — stat-based augments shown in standard level-up offers and chests,
///      split by tier: Tier 1 = Wooden chest, Tier 2 = Silver chest, Tier 3 = Golden chest.
///   2. Unlock Augments  — one-time ability unlocks organized by weapon, shown every 3rd level-up.
/// </summary>
[CreateAssetMenu(fileName = "AugmentDatabase", menuName = "Scriptable Objects/Augment Database")]
public class AugmentDatabase : ScriptableObject
{
    [Header("── Tier 1 Augments (Wooden Chest) ──────────────────────────")]
    public List<AugmentDefinition> tier1Augments = new();

    [Header("── Tier 2 Augments (Silver Chest) ──────────────────────────")]
    public List<AugmentDefinition> tier2Augments = new();

    [Header("── Tier 3 Augments (Golden Chest) ──────────────────────────")]
    public List<AugmentDefinition> tier3Augments = new();

    [Header("── Unlock Augments  (organized by weapon) ────────────────")]
    [Tooltip("One-time ability unlocks. Shown on every 3rd level-up as a dedicated unlock offer.")]
    public UnlockAugmentDatabase unlockDatabase;

    /// <summary>All regular (non-unlock) augments across the three tiers, combined. Used by fallback paths that don't need tier separation.</summary>
    public IEnumerable<AugmentDefinition> AllRegularAugments
    {
        get
        {
            foreach (AugmentDefinition a in tier1Augments) yield return a;
            foreach (AugmentDefinition a in tier2Augments) yield return a;
            foreach (AugmentDefinition a in tier3Augments) yield return a;
        }
    }
}
