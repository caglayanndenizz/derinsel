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
    float LightAttackRate { get; }
    float LightImpactFallbackDelay { get; }
    Slider LongbowChargeMeter { get; }
    GameObject LongbowMeterCanvas { get; }

    // Crossbow / bolt settings
    float CrossbowBoltSpeedMultiplier { get; }
    float CrossbowBoltDamageMultiplier { get; }
    float CrossbowAttackRate { get; }
    float CrossbowBoltReleaseDelay { get; }
    GameObject CrossbowBoltPrefab { get; }
    float CrossbowBoltMaxLifetime { get; }

    // Unity references
    Animator Animator { get; }

    // Mutable timing state
    float NextHammerUseTime { get; set; }
    float NextAttackTime { get; set; }

    // Mutable attack state (written by states, read by Player fallback handlers)
    bool LightAttackInProgress { get; set; }
    float LightFallbackExecuteAt { get; set; }

    // Rigidbody (required by GrappleSwingState)
    Rigidbody2D Rb { get; }

    // Methods
    void ScheduleLongbowArrow(float damage, bool useBowChargedMultiplier, Vector2 aimWorldAtFireInput);
    void ScheduleCrossbowBolt(float damage, Vector2 aimWorldAtFireInput);
    Vector2 GetLongbowAimWorldPointAtCurrentMouse();
    void TriggerHeavyAttack();

    // Grapple hook
    void SpawnGrappleBolt(Vector2 aimWorldPoint);
    void EnterGrappleSwing(Vector2 anchorPoint, float ropeLength, GrappleBolt bolt);
}
