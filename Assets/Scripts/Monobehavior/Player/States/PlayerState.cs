using UnityEngine;

public abstract class PlayerState
{
    protected static readonly int IsChargingHash    = Animator.StringToHash("IsCharging");
    protected static readonly int LongbowChargeHash = Animator.StringToHash("BowCharge");
    protected static readonly int LightAttackHash   = Animator.StringToHash("LightAttack");
    protected static readonly int HeavyAttackHash   = Animator.StringToHash("HeavyAttack");
    protected static readonly int CrossbowShootHash = Animator.StringToHash("CrossbowShoot");

    public virtual bool IsChargingForMovement => false;
    public virtual bool IsChargeMeterFull(IPlayerContext context) => false;

    public abstract void Enter(IPlayerContext context);
    public abstract void Handle(IPlayerContext context);
    public abstract void Exit(IPlayerContext context);
}
