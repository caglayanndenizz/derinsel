using System;
using UnityEngine;

/// <summary>
/// Checks weapon mutation after each augment selection.
/// Once the threshold of unlock augments is reached for a weapon,
/// that weapon's unlock augments are no longer offered to the player.
///
/// Usage:
///   – Attach to a single object in the scene (singleton).
///   – Set <see cref="longbowMutationThreshold"/> in the Inspector (default: 4).
///   – <see cref="AugmentWeightSystem"/> queries <see cref="ShouldExcludeWeaponUnlocks"/> in IsEligible;
///     all unlock augments for that weapon are removed from the pool once the threshold is exceeded.
/// </summary>
public class WeaponMutationChecker : MonoBehaviour
{
    public static WeaponMutationChecker Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Mutation Thresholds")]
    [Tooltip("Number of longbow unlock augments required before bow unlocks stop being offered. 0 = disabled.")]
    [SerializeField] private int longbowMutationThreshold  = 4;

    [Tooltip("Number of crossbow unlock augments required before crossbow unlocks stop being offered. 0 = disabled.")]
    [SerializeField] private int crossbowMutationThreshold = 0;

    [Tooltip("Number of hammer unlock augments required before hammer unlocks stop being offered. 0 = disabled.")]
    [SerializeField] private int hammerMutationThreshold   = PlayerAugmentController.GemObsidianThreshold;

    [Header("Runtime State (Read-Only)")]
    [SerializeField] private int _longbowUnlocksApplied;
    [SerializeField] private int _crossbowUnlocksApplied;
    [SerializeField] private int _hammerUnlocksApplied;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired the first time the longbow mutation is completed.</summary>
    public event Action OnLongbowMutationComplete;
    /// <summary>Fired the first time the crossbow mutation is completed.</summary>
    public event Action OnCrossbowMutationComplete;
    /// <summary>Fired the first time the hammer mutation is completed.</summary>
    public event Action OnHammerMutationComplete;

    // ── Properties ────────────────────────────────────────────────────────────

    public bool IsLongbowMutationComplete  =>
        longbowMutationThreshold  > 0 && _longbowUnlocksApplied  >= longbowMutationThreshold;

    public bool IsCrossbowMutationComplete =>
        crossbowMutationThreshold > 0 && _crossbowUnlocksApplied >= crossbowMutationThreshold;

    public bool IsHammerMutationComplete   =>
        hammerMutationThreshold   > 0 && _hammerUnlocksApplied   >= hammerMutationThreshold;

    // ── Internal ──────────────────────────────────────────────────────────────

    private PlayerAugmentController _controller;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this)
            Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Should unlock augments for the given weapon type be excluded from the offer pool?
    /// Returns <c>true</c> if the mutation for that weapon is complete.
    /// </summary>
    public bool ShouldExcludeWeaponUnlocks(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Longbow:  return IsLongbowMutationComplete;
            case WeaponType.Crossbow: return IsCrossbowMutationComplete;
            case WeaponType.Hammer:   return IsHammerMutationComplete;
            default:                  return false; // Universal is never filtered
        }
    }

    /// <summary>
    /// Assigns the controller from outside (e.g. when a new run starts).
    /// </summary>
    public void RegisterController(PlayerAugmentController controller)
    {
        Unsubscribe();
        _controller = controller;
        Subscribe();
    }

    /// <summary>Resets all counters at the start of a new run.</summary>
    public void ResetAll()
    {
        _longbowUnlocksApplied  = 0;
        _crossbowUnlocksApplied = 0;
        _hammerUnlocksApplied   = 0;
    }

    // ── Subscription ──────────────────────────────────────────────────────────

    private void TrySubscribe()
    {
        if (_controller != null) return;
        _controller = UnityEngine.Object.FindAnyObjectByType<PlayerAugmentController>();
        Subscribe();
    }

    private void Subscribe()
    {
        if (_controller == null) return;
        _controller.AugmentApplied += OnAugmentApplied;
    }

    private void Unsubscribe()
    {
        if (_controller == null) return;
        _controller.AugmentApplied -= OnAugmentApplied;
        _controller = null;
    }

    // ── Event Handler ─────────────────────────────────────────────────────────

    private void OnAugmentApplied(AugmentDefinition augment)
    {
        UnlockAugmentDefinition unlockDef = augment as UnlockAugmentDefinition;
        if (unlockDef == null) return;

        // Record completion state before the threshold is crossed
        bool prevLongbow  = IsLongbowMutationComplete;
        bool prevCrossbow = IsCrossbowMutationComplete;
        bool prevHammer   = IsHammerMutationComplete;

        switch (unlockDef.weaponType)
        {
            case WeaponType.Longbow:  _longbowUnlocksApplied++;  break;
            case WeaponType.Crossbow: _crossbowUnlocksApplied++; break;
            case WeaponType.Hammer:   _hammerUnlocksApplied++;   break;
            // Universal: not counted
        }

        // Fire event if the threshold was newly crossed
        if (!prevLongbow  && IsLongbowMutationComplete)  OnLongbowMutationComplete?.Invoke();
        if (!prevCrossbow && IsCrossbowMutationComplete) OnCrossbowMutationComplete?.Invoke();
        if (!prevHammer   && IsHammerMutationComplete)   OnHammerMutationComplete?.Invoke();
    }
}
