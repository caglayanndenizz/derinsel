using UnityEngine;

public class CrossbowState : PlayerState
{
    public override void Enter(IPlayerContext context)
    {
        ResetAnimator(context);
        TryFireBolt(context);
    }

    public override void Handle(IPlayerContext context)
    {
        if (!Input.GetButton("Fire2"))
        {
            context.SetState(new IdleState());
            return;
        }

        TryFireBolt(context);
    }

    public override void Exit(IPlayerContext context)
    {
        ResetAnimator(context);
    }

    void TryFireBolt(IPlayerContext context)
    {
        if (Time.time < context.NextAttackTime) return;

        float dmg = context.Stats != null
            ? context.Stats.crossbowAp
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
