using UnityEngine;

public class ArcherState : PlayerState
{
    private float _bowCharge;
    private bool _isBowCharging;

    public override bool IsChargingForMovement => _isBowCharging;

    public override void Enter(IPlayerContext context)
    {
        _bowCharge = 0f;
        _isBowCharging = false;
    }

    public override void Handle(IPlayerContext context)
    {
        if (Input.GetButtonUp("Fire2"))
        {
            bool wasFull = _bowCharge >= context.MaxBowChargeTime - 0.0001f;
            ResetBowCharge(context);

            if (Time.time >= context.NextAttackTime)
            {
                context.Animator?.SetTrigger(LightAttackHash);
                Vector2 aim = context.GetBowAimWorldPointAtCurrentMouse();
                float dmg = context.Stats != null ? context.Stats.lightAttackDamage : 0f;
                context.ScheduleBowArrow(dmg, wasFull, aim);
                context.LightAttackInProgress = true;
                context.LightFallbackExecuteAt = Time.time + Mathf.Max(0.03f, context.LightImpactFallbackDelay);
                context.NextAttackTime = Time.time + context.LightAttackRate;
            }

            context.SetState(new IdleState());
            return;
        }

        _isBowCharging = Input.GetButton("Fire2");

        if (!_isBowCharging && _bowCharge <= 0f)
        {
            context.SetState(new IdleState());
            return;
        }

        if (_isBowCharging)
        {
            if (context.BowMeterCanvas != null) context.BowMeterCanvas.SetActive(true);
            _bowCharge += Time.deltaTime;
            _bowCharge = Mathf.Clamp(_bowCharge, 0f, context.MaxBowChargeTime);
            if (context.BowChargeMeter != null)
                context.BowChargeMeter.value = _bowCharge / Mathf.Max(0.0001f, context.MaxBowChargeTime);
        }

        UpdateAnimator(context);
    }

    public override void Exit(IPlayerContext context)
    {
        ResetBowCharge(context);
    }

    private void ResetBowCharge(IPlayerContext context)
    {
        _isBowCharging = false;
        _bowCharge = 0f;
        if (context.BowChargeMeter != null) context.BowChargeMeter.value = 0f;
        if (context.BowMeterCanvas != null) context.BowMeterCanvas.SetActive(false);
        UpdateAnimator(context);
    }

    private void UpdateAnimator(IPlayerContext context)
    {
        if (context.Animator == null) return;
        context.Animator.SetBool(IsChargingHash, false);
        context.Animator.SetBool(BowChargeHash, _isBowCharging);
    }
}
