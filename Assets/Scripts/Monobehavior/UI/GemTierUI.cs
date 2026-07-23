using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;

/// <summary>
/// Updates the tier badge (color/count/visibility) for one weapon's gem-augment progress.
/// One instance tracks the Longbow, another instance of this same script (different Inspector
/// config, <see cref="weaponToTrack"/> set to Hammer) tracks the Hammer.
/// Thresholds come from PlayerAugmentController's shared Gem*Threshold constants.
/// </summary>
public class GemTierUI : MonoBehaviour
{
    [Header("Weapon")]
    [Tooltip("Which weapon's gem tier this badge tracks.")]
    [SerializeField] private WeaponType weaponToTrack = WeaponType.Longbow;

    [Header("UI Elements")]
    [FormerlySerializedAs("longbowTrait")] [SerializeField] private GameObject traitRoot;
    [SerializeField] private Image           badgeImage;
    [FormerlySerializedAs("longbowIcon")] [SerializeField] private Image weaponIcon;
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

        GemTier tier  = weaponToTrack == WeaponType.Hammer ? augmentController.HammerGemTier        : augmentController.LongbowGemTier;
        int     count = weaponToTrack == WeaponType.Hammer ? augmentController.HammerGemAugmentCount : augmentController.LongbowGemAugmentCount;

        if (tier == _cachedTier && count == _cachedCount) return;
        _cachedTier  = tier;
        _cachedCount = count;

        if (traitRoot != null)
            traitRoot.SetActive(count > 0);
        else
            Debug.LogWarning("[GemTierUI] traitRoot is not assigned in the Inspector!", this);

        Color c = TierColor(tier);
        if (badgeImage != null) badgeImage.color = c;
        if (weaponIcon != null) weaponIcon.color = c;
        if (countText  != null) countText.text   = $"{count}/{PlayerAugmentController.GemObsidianThreshold}";
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
