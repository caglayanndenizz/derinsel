using UnityEngine;

/// <summary>
/// Mutated Hammer state — unlocked once <see cref="PlayerAugmentController.HasHammerMutationUnlock"/> is true
/// (Obsidian hammer gem tier / hammer pool completion), mirroring how <see cref="CrossbowState"/> mutates
/// out of <see cref="LongbowState"/>.
///
/// Foundation-only: this currently reuses the existing Hammer charge/attack flow (see
/// <see cref="HammerState"/> and Player.Hammer.cs) as a functional starting point, with one small
/// cosmetic differentiator — faster charge fill — so the mutated weapon already feels distinct while
/// the real attack feel/animation/VFX are designed later.
/// </summary>
public class GreatHammerState : PlayerState
{
    // Cosmetic differentiator — tune freely, purely local to this state.
    private const float MutatedChargeSpeedMultiplier = 1.5f; // charges faster than the base HammerState

    private float _currentCharge;
    private bool  _isCharging;

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
    }

    public override void Handle(IPlayerContext context)
    {
        float effective = context.MaxChargeTime *
            (context.AugmentController != null ? context.AugmentController.HammerChargeMultiplier : 1f);
        effective = Mathf.Max(0f, effective);

        if (Input.GetButton("Fire1"))
        {
            _isCharging = true;
            if (context.MeterCanvas != null) context.MeterCanvas.SetActive(true);
            _currentCharge += Time.deltaTime * MutatedChargeSpeedMultiplier;
            _currentCharge = Mathf.Clamp(_currentCharge, 0f, effective);
            if (context.ChargeMeter != null)
                context.ChargeMeter.value = effective > 0.0001f ? _currentCharge / effective : 1f;
            if (context.Animator != null)
                context.Animator.speed = Mathf.Clamp(context.MaxChargeTime / Mathf.Max(0.05f, effective), 0.1f, 20f) * MutatedChargeSpeedMultiplier;
            UpdateAnimator(context);
        }

        if (Input.GetButtonUp("Fire1"))
        {
            // Minimum 5% charge required to attack at all; below that the swing is wasted.
            // When effective charge time is ~0 (max charge-speed augments), any hold counts as 100%.
            float chargeFraction = effective > 0.0001f
                ? Mathf.Clamp01(_currentCharge / effective)
                : (_isCharging ? 1f : 0f);

            if (chargeFraction >= 0.05f)
                context.TriggerHeavyAttack(chargeFraction);

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
