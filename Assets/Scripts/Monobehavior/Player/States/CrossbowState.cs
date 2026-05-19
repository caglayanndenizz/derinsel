using UnityEngine;

public class CrossbowState : PlayerState
{
    bool _grappleFired;

    public override void Enter(IPlayerContext context)
    {
        ResetAnimator(context);
        _grappleFired = false;

        if (HasGrappleAugment(context))
            FireGrappleBolt(context);
        else
            TryFireBolt(context);
    }

    public override void Handle(IPlayerContext context)
    {
        if (!Input.GetButton("Fire2"))
        {
            context.SetState(new IdleState());
            return;
        }

        if (HasGrappleAugment(context))
        {
            if (!_grappleFired)
                FireGrappleBolt(context);
            // else: bolt is in flight, waiting for anchor → do nothing
            return;
        }

        TryFireBolt(context);
    }

    public override void Exit(IPlayerContext context)
    {
        ResetAnimator(context);
    }

    static bool HasGrappleAugment(IPlayerContext context) =>
        context.AugmentController != null && context.AugmentController.HasCrossbowGrappleBolt;

    void FireGrappleBolt(IPlayerContext context)
    {
        Vector2 aim = context.GetLongbowAimWorldPointAtCurrentMouse();
        context.SpawnGrappleBolt(aim);

        if (context.Animator != null)
            context.Animator.SetTrigger(CrossbowShootHash);

        _grappleFired = true;
    }

    void TryFireBolt(IPlayerContext context)
    {
        if (Time.time < context.NextAttackTime) return;

        float dmg = context.Stats != null
            ? context.Stats.lightAttackDamage * context.CrossbowBoltDamageMultiplier
            : 0f;

        Vector2 aim = context.GetLongbowAimWorldPointAtCurrentMouse();
        context.ScheduleCrossbowBolt(dmg, aim);

        if (context.Animator != null)
            context.Animator.SetTrigger(CrossbowShootHash);

        context.LightAttackInProgress  = true;
        context.LightFallbackExecuteAt = Time.time + Mathf.Max(0.03f, context.LightImpactFallbackDelay);
        context.NextAttackTime         = Time.time + context.CrossbowAttackRate;
    }

    static void ResetAnimator(IPlayerContext context)
    {
        if (context.Animator == null) return;
        context.Animator.SetBool(IsChargingHash,    false);
        context.Animator.SetBool(LongbowChargeHash, false);
    }
}
