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
    [FormerlySerializedAs("longbowIcon")] [SerializeField] private Image weaponIcon;
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Badge Images (one per tier — shown/hidden, not tinted)")]
    [SerializeField] private GameObject coalBadge;
    [SerializeField] private GameObject goldBadge;
    [SerializeField] private GameObject diamondBadge;
    [SerializeField] private GameObject obsidianBadge;

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

        if (countText != null) countText.text = $"{count}/{PlayerAugmentController.GemObsidianThreshold}";

        SetActiveBadge(tier);
    }

    private void SetActiveBadge(GemTier tier)
    {
        if (coalBadge     != null) coalBadge.SetActive(tier == GemTier.Coal);
        if (goldBadge     != null) goldBadge.SetActive(tier == GemTier.Gold);
        if (diamondBadge  != null) diamondBadge.SetActive(tier == GemTier.Diamond);
        if (obsidianBadge != null) obsidianBadge.SetActive(tier == GemTier.Obsidian);
    }
}
