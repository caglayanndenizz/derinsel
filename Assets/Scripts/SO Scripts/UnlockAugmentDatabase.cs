using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Source of truth for all unlock augments, organized by weapon type.
/// Referenced by AugmentDatabase. Populated in the Unity Inspector.
/// </summary>
[CreateAssetMenu(fileName = "UnlockAugmentDatabase", menuName = "Scriptable Objects/Unlock Augment Database")]
public class UnlockAugmentDatabase : ScriptableObject
{
    [Header("Longbow Unlocks")]
    public List<UnlockAugmentDefinition> longbowUnlocks = new();

    [Header("Crossbow Unlocks")]
    public List<UnlockAugmentDefinition> crossbowUnlocks = new();

    [Header("Hammer Unlocks")]
    public List<UnlockAugmentDefinition> hammerUnlocks = new();

    [Header("Universal Unlocks")]
    public List<UnlockAugmentDefinition> universalUnlocks = new();

    /// <summary>Iterates all unlock augments across every weapon group.</summary>
    public IEnumerable<UnlockAugmentDefinition> GetAllUnlocks()
    {
        foreach (var u in longbowUnlocks)   if (u != null) yield return u;
        foreach (var u in crossbowUnlocks)  if (u != null) yield return u;
        foreach (var u in hammerUnlocks)    if (u != null) yield return u;
        foreach (var u in universalUnlocks) if (u != null) yield return u;
    }
}
