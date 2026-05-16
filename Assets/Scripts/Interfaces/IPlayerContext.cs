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

    // Bow / archer settings & UI
    float MaxBowChargeTime { get; }
    float LightAttackRate { get; }
    float LightImpactFallbackDelay { get; }
    Slider BowChargeMeter { get; }
    GameObject BowMeterCanvas { get; }

    // Radial bow volley settings
    float RadialBowAutoVolleyIntervalSeconds { get; }
    GameObject ArrowPrefab { get; }

    // Unity references
    Animator Animator { get; }

    // Mutable timing state
    float NextHammerUseTime { get; set; }
    float NextAttackTime { get; set; }
    bool HadRadialBowMutationLastFrame { get; set; }
    float NextRadialBowAutoVolleyTime { get; set; }

    // Mutable attack state (written by states, read by Player fallback handlers)
    bool LightAttackInProgress { get; set; }
    float LightFallbackExecuteAt { get; set; }

    // Methods
    void ScheduleBowArrow(float damage, bool useBowChargedMultiplier, Vector2 aimWorldAtFireInput);
    Vector2 GetBowAimWorldPointAtCurrentMouse();
    void FireRadialBowMutationAutoVolley(float lightDamage);
    void TriggerHeavyAttack();
}
