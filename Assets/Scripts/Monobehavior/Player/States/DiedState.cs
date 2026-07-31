using UnityEngine;

public class DiedState : PlayerState
{
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");

    public override void Enter(IPlayerContext context)
    {
        context.Animator?.SetBool(IsDeadHash, true);
    }

    public override void Handle(IPlayerContext context) { }
    public override void Exit(IPlayerContext context) { }
}
