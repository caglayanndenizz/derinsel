using UnityEngine;

public class HammerState : PlayerState
{
    private float _currentCharge;
    private bool _isCharging;

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
        _isCharging = false;
    }

    public override void Handle(IPlayerContext context)
    {
        if (Time.time < context.NextHammerUseTime)
        {
            if (_isCharging) ResetCharge(context);
            context.SetState(new IdleState());
            return;
        }

        float effective = context.MaxChargeTime *
            (context.AugmentController != null ? context.AugmentController.HammerChargeMultiplier : 1f);

        if (Input.GetButton("Fire1"))
        {
            _isCharging = true;
            if (context.MeterCanvas != null) context.MeterCanvas.SetActive(true);
            _currentCharge += Time.deltaTime;
            _currentCharge = Mathf.Clamp(_currentCharge, 0f, effective);
            if (context.ChargeMeter != null)
                context.ChargeMeter.value = _currentCharge / Mathf.Max(0.0001f, effective);
            UpdateAnimator(context);
        }

        if (Input.GetButtonUp("Fire1"))
        {
            if (_currentCharge >= effective)
                context.TriggerHeavyAttack();
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
        _isCharging = false;
        _currentCharge = 0f;
        if (context.ChargeMeter != null) context.ChargeMeter.value = 0f;
        if (context.MeterCanvas != null) context.MeterCanvas.SetActive(false);
        UpdateAnimator(context);
    }

    private void UpdateAnimator(IPlayerContext context)
    {
        if (context.Animator == null) return;
        context.Animator.SetBool(IsChargingHash, _isCharging);
        context.Animator.SetBool(BowChargeHash, false);
    }
}
