using UnityEngine;

public class HammerState : PlayerState
{
    private float _currentCharge;
    private bool  _isCharging;
    private float _holdTime;       // tracks how long the left mouse button has been held

    public override bool IsChargingForMovement => _isCharging;

    public override bool IsChargeMeterFull(IPlayerContext context)
    {
        if (!_isCharging) return false;
        if (context.ChargeMeter != null)
            return context.ChargeMeter.value >= 1f - 0.0001f;
        float effective = context.MaxChargeTime *
            (context.AugmentController != null ? context.AugmentController.HammerChargeMultiplier : 1f);
        return _currentCharge >= effective - 0.0001f;
    }

    public override void Enter(IPlayerContext context)
    {
        _currentCharge = 0f;
        _isCharging    = false;
        _holdTime      = 0f;
    }

    public override void Handle(IPlayerContext context)
    {
        bool heavyReady = Time.time >= context.NextHammerUseTime;

        float effective = context.MaxChargeTime *
            (context.AugmentController != null ? context.AugmentController.HammerChargeMultiplier : 1f);

        bool canCharge = context.AugmentController != null && context.AugmentController.HasHammerChargeUnlock;

        // Count hold time while button is pressed; switch to charge mode when cooldown is ready and augment is present.
        if (Input.GetButton("Fire1") && heavyReady && canCharge)
        {
            _holdTime += Time.deltaTime;

            // Threshold exceeded → charge mode active.
            if (_holdTime >= context.HammerChargeStartDelay)
            {
                _isCharging = true;
                if (context.MeterCanvas != null) context.MeterCanvas.SetActive(true);
                _currentCharge += Time.deltaTime;
                _currentCharge = Mathf.Clamp(_currentCharge, 0f, effective);
                if (context.ChargeMeter != null)
                    context.ChargeMeter.value = _currentCharge / Mathf.Max(0.0001f, effective);
                if (context.Animator != null)
                    context.Animator.speed = context.MaxChargeTime / Mathf.Max(0.0001f, effective);
                UpdateAnimator(context);
            }
        }

        if (Input.GetButtonUp("Fire1"))
        {
            // Fully charged + heavy ready → heavy attack; otherwise → light attack.
            if (_isCharging && _currentCharge >= effective)
                context.TriggerHeavyAttack();
            else
                context.TriggerHammerLightAttack();

            ResetCharge(context);
            context.SetState(new IdleState());
        }
    }

    public override void Exit(IPlayerContext context)
    {
        ResetCharge(context);
    }

    private void ResetCharge(IPlayerContext context)
    {
        _isCharging    = false;
        _currentCharge = 0f;
        _holdTime      = 0f;
        if (context.ChargeMeter != null) context.ChargeMeter.value = 0f;
        if (context.MeterCanvas != null) context.MeterCanvas.SetActive(false);
        if (context.Animator != null) context.Animator.speed = 1f;
        UpdateAnimator(context);
    }

    private void UpdateAnimator(IPlayerContext context)
    {
        if (context.Animator == null) return;
        context.Animator.SetBool(IsChargingHash, _isCharging);
        context.Animator.SetBool(LongbowChargeHash, false);
    }
}
