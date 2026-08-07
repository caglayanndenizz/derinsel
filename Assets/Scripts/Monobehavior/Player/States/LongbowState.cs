using UnityEngine;

public class LongbowState : PlayerState
{
    private float _longbowCharge;
    private bool  _isLongbowCharging;

    public override bool IsChargingForMovement => _isLongbowCharging;

    public override void Enter(IPlayerContext context)
    {
        _longbowCharge = 0f;
        _isLongbowCharging = false;
    }

    public override void Handle(IPlayerContext context)
    {
        float effective = context.MaxLongbowChargeTime *
            (context.AugmentController != null ? context.AugmentController.BowChargeMultiplier : 1f);
        effective = Mathf.Max(0f, effective);

        if (Input.GetButtonUp("Fire2"))
        {
            // Minimum 5% charge required to fire at all; below that the draw is wasted.
            // When effective charge time is ~0 (max charge-speed augments), any hold counts as 100%.
            float chargeFraction = effective > 0.0001f
                ? Mathf.Clamp01(_longbowCharge / effective)
                : (_isLongbowCharging ? 1f : 0f);

            ResetBowCharge(context);

            if (chargeFraction >= 0.05f)
            {
                context.Animator?.SetTrigger(LightAttackHash);
                Vector2 aim = context.GetLongbowAimWorldPointAtCurrentMouse();
                context.ScheduleLongbowArrow(chargeFraction, aim);
                context.LightAttackInProgress = true;
                context.LightFallbackExecuteAt = Time.time + Mathf.Max(0.03f, context.LightImpactFallbackDelay);
            }

            context.SetState(new IdleState());
            return;
        }

        _isLongbowCharging = Input.GetButton("Fire2");

        // Return to Idle if button is released and charge is zero.
        if (!Input.GetButton("Fire2") && _longbowCharge <= 0f)
        {
            context.SetState(new IdleState());
            return;
        }

        if (_isLongbowCharging)
        {
            if (context.LongbowMeterCanvas != null) context.LongbowMeterCanvas.SetActive(true);
            _longbowCharge += Time.deltaTime;
            _longbowCharge = Mathf.Clamp(_longbowCharge, 0f, effective);
            if (context.LongbowChargeMeter != null)
                context.LongbowChargeMeter.value = effective > 0.0001f ? _longbowCharge / effective : 1f;
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
