using UnityEngine;

public class IdleState : PlayerState
{
    public override void Enter(IPlayerContext context)
    {
        if (context.Animator == null) return;
        context.Animator.SetBool(IsChargingHash, false);
        context.Animator.SetBool(LongbowChargeHash, false);
    }

    public override void Handle(IPlayerContext context)
    {
        if (Input.GetButton("Fire1") && Time.time >= context.NextHammerUseTime)
        {
            context.SetState(new HammerState());
            return;
        }

        if (Input.GetButton("Fire2"))
        {
            bool mutationActive = context.AugmentController != null && context.AugmentController.HasRadialLongbowMutationUnlock;
            context.SetState(mutationActive ? (PlayerState)new CrossbowState() : new ArcherState());
            return;
        }
    }

    public override void Exit(IPlayerContext context) { }
}
