using UnityEngine;

public class LongbowState : PlayerState
{
    private float _longbowCharge;
    private bool _isLongbowCharging;

    public override bool IsChargingForMovement => _isLongbowCharging;

    public override void Enter(IPlayerContext context)
    {
        _longbowCharge = 0f;
        _isLongbowCharging = false;
    }

    public override void Handle(IPlayerContext context)
    {
        if (Input.GetButtonUp("Fire2"))
        {
            bool wasFull = _longbowCharge >= context.MaxLongbowChargeTime - 0.0001f;
            ResetBowCharge(context);

            if (Time.time >= context.NextAttackTime)
            {
                context.Animator?.SetTrigger(LightAttackHash);
                Vector2 aim = context.GetLongbowAimWorldPointAtCurrentMouse();
                float dmg = context.Stats != null ? context.Stats.lightAttackDamage : 0f;
                context.ScheduleLongbowArrow(dmg, wasFull, aim);
                context.LightAttackInProgress = true;
                context.LightFallbackExecuteAt = Time.time + Mathf.Max(0.03f, context.LightImpactFallbackDelay);
                context.NextAttackTime = Time.time + context.LightAttackRate;
            }

            context.SetState(new IdleState());
            return;
        }

        bool canCharge = context.AugmentController != null && context.AugmentController.HasChargedLongbowAoe;
        _isLongbowCharging = canCharge && Input.GetButton("Fire2");

        if (!_isLongbowCharging && _longbowCharge <= 0f)
        {
            context.SetState(new IdleState());
            return;
        }

        if (_isLongbowCharging)
        {
            if (context.LongbowMeterCanvas != null) context.LongbowMeterCanvas.SetActive(true);
            _longbowCharge += Time.deltaTime;
            _longbowCharge = Mathf.Clamp(_longbowCharge, 0f, context.MaxLongbowChargeTime);
            if (context.LongbowChargeMeter != null)
                context.LongbowChargeMeter.value = _longbowCharge / Mathf.Max(0.0001f, context.MaxLongbowChargeTime);
        }

        UpdateAnimator(context);
    }

    public override void Exit(IPlayerContext context)
    {
        ResetBowCharge(context);
    }

    private void ResetBowCharge(IPlayerContext context)
    {
        _isLongbowCharging = false;
        _longbowCharge = 0f;
        if (context.LongbowChargeMeter != null) context.LongbowChargeMeter.value = 0f;
        if (context.LongbowMeterCanvas != null) context.LongbowMeterCanvas.SetActive(false);
        UpdateAnimator(context);
    }

    private void UpdateAnimator(IPlayerContext context)
    {
        if (context.Animator == null) return;
        context.Animator.SetBool(IsChargingHash, false);
        context.Animator.SetBool(LongbowChargeHash, _isLongbowCharging);
    }
}
