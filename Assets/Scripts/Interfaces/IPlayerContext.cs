using UnityEngine;
using UnityEngine.UI;

public interface IPlayerContext
{
    void SetState(PlayerState newState);

    // Stats & controller
    EntityStats Stats { get; }
    PlayerAugmentController AugmentController { get; }

    // Hammer charge settings & UI
    float MaxChargeTime { get; }
    Slider ChargeMeter { get; }
    GameObject MeterCanvas { get; }

    // Longbow / archer settings & UI
    float MaxLongbowChargeTime { get; }
    float LightImpactFallbackDelay { get; }
    Slider LongbowChargeMeter { get; }
    GameObject LongbowMeterCanvas { get; }
    float ArrowShotCooldown { get; }
    float NextArrowShotTime { get; set; }

    // Crossbow / bolt settings
    float CrossbowBoltSpeedMultiplier { get; }
    float CrossbowAttackRate { get; }
    float CrossbowBoltReleaseDelay { get; }
    GameObject CrossbowBoltPrefab { get; }
    float CrossbowBoltMaxLifetime { get; }
    float NextCrossbowAttackTime { get; set; }

    // Unity references
    Animator Animator { get; }

    // Mutable attack state (written by states, read by Player fallback handlers)
    bool LightAttackInProgress { get; set; }
    float LightFallbackExecuteAt { get; set; }

    // Methods
    void ScheduleLongbowArrow(float chargeFraction, Vector2 aimWorldAtFireInput);
    void ScheduleCrossbowBolt(Vector2 aimWorldAtFireInput);
    Vector2 GetLongbowAimWorldPointAtCurrentMouse();
    void TriggerHeavyAttack(float chargeFraction);
}
