using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AugmentDatabase", menuName = "Scriptable Objects/Augment Database")]
public class AugmentDatabase : ScriptableObject
{
    [Tooltip("Single source of truth for all augment definitions.")]
    public List<AugmentDefinition> allAugments = new();
}
