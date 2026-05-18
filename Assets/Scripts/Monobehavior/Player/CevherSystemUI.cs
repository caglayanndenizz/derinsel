using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tek badge image üzerinden tier rengini günceller.
/// Eşikler: Kömür≥1 · Altın≥2 · Elmas≥4 · Obsidyen≥6
/// </summary>
public class CevherSystemUI : MonoBehaviour
{
    [Header("UI Elemanları")]
    [SerializeField] private GameObject      longbowTrait;
    [SerializeField] private Image           badgeImage;
    [SerializeField] private Image           longbowIcon;
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Renkler")]
    [SerializeField] private Color inactiveColor  = new Color(0.22f, 0.22f, 0.22f, 1.00f);
    [SerializeField] private Color komurColor     = new Color(0.45f, 0.40f, 0.35f, 1.00f);
    [SerializeField] private Color altinColor     = new Color(1.00f, 0.84f, 0.00f, 1.00f);
    [SerializeField] private Color elmasColor     = new Color(0.00f, 0.75f, 1.00f, 1.00f);
    [SerializeField] private Color obsidyenColor  = new Color(0.55f, 0.00f, 1.00f, 1.00f);

    [Header("Referans")]
    [SerializeField] private PlayerAugmentController augmentController;

    private CevherTier _cachedTier  = (CevherTier)(-1);
    private int        _cachedCount = -1;

    private void Awake()
    {
        TryFindController();
    }

    private void Start()
    {
        if (augmentController != null) return;
        TryFindController();
        if (augmentController != null)
            augmentController.AugmentApplied += HandleAugmentApplied;
        RefreshUI();
    }

    private void TryFindController()
    {
        if (augmentController != null) return;
        augmentController = Object.FindAnyObjectByType<PlayerAugmentController>();
    }

    private void OnEnable()
    {
        if (augmentController != null)
            augmentController.AugmentApplied += HandleAugmentApplied;
        RefreshUI();
    }

    private void OnDisable()
    {
        if (augmentController != null)
            augmentController.AugmentApplied -= HandleAugmentApplied;
    }

    private void HandleAugmentApplied(AugmentDefinition _) => RefreshUI();

    public void RefreshUI()
    {
        if (augmentController == null) return;

        CevherTier tier  = augmentController.LongbowCevherTier;
        int        count = augmentController.LongbowCevherAugmentCount;

        if (tier == _cachedTier && count == _cachedCount) return;
        _cachedTier  = tier;
        _cachedCount = count;

        if (longbowTrait != null) longbowTrait.SetActive(count > 0);

        Color c = TierColor(tier);
        if (badgeImage != null) badgeImage.color = c;
        if (longbowIcon    != null) longbowIcon.color    = c;
        if (countText  != null) countText.text   = $"{count}/{PlayerAugmentController.CevherObsidyenThreshold}";
    }

    private Color TierColor(CevherTier tier)
    {
        switch (tier)
        {
            case CevherTier.Komur:    return komurColor;
            case CevherTier.Altin:    return altinColor;
            case CevherTier.Elmas:    return elmasColor;
            case CevherTier.Obsidyen: return obsidyenColor;
            default:                  return inactiveColor;
        }
    }
}
