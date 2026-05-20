using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Master augment database with two distinct sections:
///   1. Regular Augments — stat-based T1/T2/T3 augments shown in standard level-up offers.
///   2. Unlock Augments  — one-time ability unlocks organized by weapon, shown every 3rd level-up.
/// </summary>
[CreateAssetMenu(fileName = "AugmentDatabase", menuName = "Scriptable Objects/Augment Database")]
public class AugmentDatabase : ScriptableObject
{
    [Header("── Stat Augments  (T1 / T2 / T3) ──────────────────────────")]
    [Tooltip("Regular augments with tier/rarity. These appear in standard level-up offers.")]
    [FormerlySerializedAs("allAugments")]
    public List<AugmentDefinition> regularAugments = new();

    [Header("── Unlock Augments  (organized by weapon) ────────────────")]
    [Tooltip("One-time ability unlocks. Shown on every 3rd level-up as a dedicated unlock offer.")]
    public UnlockAugmentDatabase unlockDatabase;
}
