using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Updates the tier color on a single badge image.
/// Thresholds: Coal≥1 · Gold≥2 · Diamond≥4 · Obsidian≥6
/// </summary>
public class GemTierUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject      longbowTrait;
    [SerializeField] private Image           badgeImage;
    [SerializeField] private Image           longbowIcon;
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Colors")]
    [SerializeField] private Color inactiveColor = new Color(0.22f, 0.22f, 0.22f, 1.00f);
    [SerializeField] private Color coalColor     = new Color(0.45f, 0.40f, 0.35f, 1.00f);
    [SerializeField] private Color goldColor     = new Color(1.00f, 0.84f, 0.00f, 1.00f);
    [SerializeField] private Color diamondColor  = new Color(0.00f, 0.75f, 1.00f, 1.00f);
    [SerializeField] private Color obsidianColor = new Color(0.55f, 0.00f, 1.00f, 1.00f);

    [Header("References")]
    [SerializeField] private PlayerAugmentController augmentController;

    private GemTier _cachedTier  = (GemTier)(-1);
    private int     _cachedCount = -1;
    private bool    _subscribed;

    private void Awake()
    {
        TryFindController();
    }

    private void Start()
    {
        TryFindController();
        EnsureSubscribed();
        RefreshUI();
    }

    private void TryFindController()
    {
        if (augmentController != null) return;
        augmentController = Object.FindAnyObjectByType<PlayerAugmentController>(FindObjectsInactive.Include);
    }

    private void EnsureSubscribed()
    {
        if (_subscribed || augmentController == null) return;
        augmentController.AugmentApplied += HandleAugmentApplied;
        _subscribed = true;
    }

    private void OnEnable()
    {
        TryFindController();
        EnsureSubscribed();
        RefreshUI();
    }

    private void OnDisable()
    {
        if (_subscribed && augmentController != null)
        {
            augmentController.AugmentApplied -= HandleAugmentApplied;
            _subscribed = false;
        }
    }

    private void HandleAugmentApplied(AugmentDefinition _) => RefreshUI();

    public void RefreshUI()
    {
        if (augmentController == null) return;

        GemTier tier  = augmentController.LongbowGemTier;
        int     count = augmentController.LongbowGemAugmentCount;

        if (tier == _cachedTier && count == _cachedCount) return;
        _cachedTier  = tier;
        _cachedCount = count;

        if (longbowTrait != null)
            longbowTrait.SetActive(count > 0);
        else
            Debug.LogWarning("[GemTierUI] longbowTrait is not assigned in the Inspector!", this);

        Color c = TierColor(tier);
        if (badgeImage  != null) badgeImage.color  = c;
        if (longbowIcon != null) longbowIcon.color  = c;
        if (countText   != null) countText.text     = $"{count}/{PlayerAugmentController.GemObsidianThreshold}";
    }

    private Color TierColor(GemTier tier)
    {
        switch (tier)
        {
            case GemTier.Coal:     return coalColor;
            case GemTier.Gold:     return goldColor;
            case GemTier.Diamond:  return diamondColor;
            case GemTier.Obsidian: return obsidianColor;
            default:               return inactiveColor;
        }
    }
}
