using UnityEngine;

public abstract class PlayerState
{
    protected static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
    protected static readonly int BowChargeHash  = Animator.StringToHash("BowCharge");
    protected static readonly int LightAttackHash = Animator.StringToHash("LightAttack");
    protected static readonly int HeavyAttackHash = Animator.StringToHash("HeavyAttack");

    public virtual bool IsChargingForMovement => false;
    public virtual bool IsChargeMeterFull(IPlayerContext context) => false;

    public abstract void Enter(IPlayerContext context);
    public abstract void Handle(IPlayerContext context);
    public abstract void Exit(IPlayerContext context);

    protected static void UpdateRadialBowAutoVolley(IPlayerContext context)
    {
        if (context.ArrowPrefab == null) return;

        bool active = context.AugmentController != null &&
                      context.AugmentController.ShouldUseRadialBowVolleyMutation(null);

        if (!active)
        {
            context.HadRadialBowMutationLastFrame = false;
            return;
        }

        if (!context.HadRadialBowMutationLastFrame)
            context.NextRadialBowAutoVolleyTime = Time.time;

        context.HadRadialBowMutationLastFrame = true;

        if (Time.time < context.NextRadialBowAutoVolleyTime) return;

        float baseDamage = context.Stats != null ? context.Stats.lightAttackDamage : 0f;
        context.FireRadialBowMutationAutoVolley(baseDamage);
        context.NextRadialBowAutoVolleyTime = Time.time +
            Mathf.Max(0.05f, context.RadialBowAutoVolleyIntervalSeconds);
    }
}
