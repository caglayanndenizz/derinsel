using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAugmentController : MonoBehaviour
{
    [SerializeField] private float movementSpeedBonus;
    [SerializeField] private bool hasChargedBowAoe;
    [SerializeField] private float chargedBowAoeRadius = 3f;
    [SerializeField] private bool hasDoubleArrowUnlock;
    [SerializeField] private bool hasWallLootsUnlock;
    [SerializeField] private bool hasExtraAugmentSlotUnlock;
    private readonly Dictionary<AugmentId, int> _appliedAugmentCounts = new();

    public event Action<AugmentDefinition> AugmentApplied;

    public float MovementSpeedBonus => Mathf.Max(0f, movementSpeedBonus);
    public bool HasChargedBowAoe => hasChargedBowAoe;
    public float ChargedBowAoeRadius => Mathf.Max(0f, chargedBowAoeRadius);
    public int ArrowShotMultiplier => hasDoubleArrowUnlock ? 2 : 1;
    public bool HasWallLootsUnlock => hasWallLootsUnlock;
    public bool HasExtraAugmentSlotUnlock => hasExtraAugmentSlotUnlock;

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
        }

        _appliedAugmentCounts[augment.id] = GetAppliedCount(augment.id) + 1;
        AugmentApplied?.Invoke(augment);
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
        return id.ToString().Contains("Unlock");
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
            default:
                return true;
        }
    }
}
