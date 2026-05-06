using System;
using UnityEngine;

public class PlayerAugmentController : MonoBehaviour
{
    [SerializeField] private float movementSpeedBonus;

    public event Action<AugmentDefinition> AugmentApplied;

    public float MovementSpeedBonus => Mathf.Max(0f, movementSpeedBonus);

    public void ApplyAugment(AugmentDefinition augment)
    {
        if (augment == null) return;

        switch (augment.id)
        {
            case AugmentId.MovementSpeedIncreaseCommon:
            case AugmentId.MovementSpeedIncreaseRare:
            case AugmentId.MovementSpeedIncreaseExtraordinary:
                movementSpeedBonus += Mathf.Max(0f, augment.value);
                break;
        }

        AugmentApplied?.Invoke(augment);
    }
}
