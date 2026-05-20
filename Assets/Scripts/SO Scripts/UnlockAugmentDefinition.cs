using UnityEngine;

/// <summary>
/// One-time ability unlock augment. Shown every unlockOfferInterval level-ups as a dedicated offer.
/// Has no rarity/tier — color in the UI is always green (unlock slot).
/// </summary>
[CreateAssetMenu(fileName = "UnlockAugmentDefinition", menuName = "Scriptable Objects/Unlock Augment Definition")]
public class UnlockAugmentDefinition : AugmentDefinition
{
    [Header("Unlock Info")]
    [Tooltip("Which weapon / system this unlock belongs to. Used to organize the UnlockAugmentDatabase.")]
    public WeaponType weaponType;
}
