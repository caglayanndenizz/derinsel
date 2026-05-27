using System;
using UnityEngine;

/// <summary>
/// Her augment seçiminden sonra silah mutasyonunu kontrol eder.
/// Bir silah için eşik kadar unlock augment alındığında o silahın
/// unlock augmentleri bir daha oyuncuya sunulmaz.
///
/// Kullanım:
///   – Sahnede tek bir objeye yerleştir (singleton).
///   – Inspector'dan <see cref="longbowMutationThreshold"/> değerini ayarla (varsayılan: 6).
///   – <see cref="AugmentWeightSystem"/> IsEligible içinde
///     <see cref="ShouldExcludeWeaponUnlocks"/> sorgular; eşik aşılmışsa
///     o silahın tüm unlock augmentleri havuzdan çıkar.
/// </summary>
public class WeaponMutationChecker : MonoBehaviour
{
    public static WeaponMutationChecker Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Mutation Thresholds")]
    [Tooltip("Longbow unlock augmentlerinden kaç tanesi alınınca bow unlock augmentleri bir daha sunulmaz. 0 = devre dışı.")]
    [SerializeField] private int longbowMutationThreshold  = 6;

    [Tooltip("Crossbow unlock augmentlerinden kaç tanesi alınınca crossbow unlock augmentleri bir daha sunulmaz. 0 = devre dışı.")]
    [SerializeField] private int crossbowMutationThreshold = 0;

    [Tooltip("Hammer unlock augmentlerinden kaç tanesi alınınca hammer unlock augmentleri bir daha sunulmaz. 0 = devre dışı.")]
    [SerializeField] private int hammerMutationThreshold   = 0;

    [Header("Runtime State (Read-Only)")]
    [SerializeField] private int _longbowUnlocksApplied;
    [SerializeField] private int _crossbowUnlocksApplied;
    [SerializeField] private int _hammerUnlocksApplied;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Longbow mutasyonu ilk kez tamamlandığında tetiklenir.</summary>
    public event Action OnLongbowMutationComplete;
    /// <summary>Crossbow mutasyonu ilk kez tamamlandığında tetiklenir.</summary>
    public event Action OnCrossbowMutationComplete;
    /// <summary>Hammer mutasyonu ilk kez tamamlandığında tetiklenir.</summary>
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
    /// Verilen silah türünün unlock augmentleri sunulmamalı mı?
    /// Mutasyon tamamlandıysa <c>true</c> döner.
    /// </summary>
    public bool ShouldExcludeWeaponUnlocks(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Longbow:  return IsLongbowMutationComplete;
            case WeaponType.Crossbow: return IsCrossbowMutationComplete;
            case WeaponType.Hammer:   return IsHammerMutationComplete;
            default:                  return false; // Universal asla filtrelenmez
        }
    }

    /// <summary>
    /// Dışarıdan controller atamak için (örn. yeni run başladığında).
    /// </summary>
    public void RegisterController(PlayerAugmentController controller)
    {
        Unsubscribe();
        _controller = controller;
        Subscribe();
    }

    /// <summary>Yeni run başında tüm sayaçları sıfırlar.</summary>
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

        // Eşik geçmeden önce tamamlanma durumunu kaydet
        bool prevLongbow  = IsLongbowMutationComplete;
        bool prevCrossbow = IsCrossbowMutationComplete;
        bool prevHammer   = IsHammerMutationComplete;

        switch (unlockDef.weaponType)
        {
            case WeaponType.Longbow:  _longbowUnlocksApplied++;  break;
            case WeaponType.Crossbow: _crossbowUnlocksApplied++; break;
            case WeaponType.Hammer:   _hammerUnlocksApplied++;   break;
            // Universal: sayılmaz
        }

        // Eşik yeni aşıldıysa event'i tetikle
        if (!prevLongbow  && IsLongbowMutationComplete)  OnLongbowMutationComplete?.Invoke();
        if (!prevCrossbow && IsCrossbowMutationComplete) OnCrossbowMutationComplete?.Invoke();
        if (!prevHammer   && IsHammerMutationComplete)   OnHammerMutationComplete?.Invoke();
    }
}
