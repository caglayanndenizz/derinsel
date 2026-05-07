using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AugmentSelectionUI : MonoBehaviour
{
    private enum PanelSource
    {
        LevelUp,
        WallLootGift
    }

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private PlayerLevel playerLevel;
    [SerializeField] private PlayerAugmentController playerAugmentController;
    [SerializeField] private AugmentDatabase augmentDatabase;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private AugmentOptionButton[] optionButtons;

    [Header("Behavior")]
    [SerializeField] private bool pauseGameWhenPanelOpen = true;
    [SerializeField] private bool deactivatePanelRootWhenHidden = true;

    [Header("Visual Theme")]
    [SerializeField] private Image panelThemeTargetImage;
    [SerializeField] private Color levelUpPanelThemeColor = Color.white;
    [SerializeField] private Color wallLootGiftPanelThemeColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    private float _previousTimeScale = 1f;
    private bool _isSubscribed;
    private CanvasGroup _panelCanvasGroup;
    private bool _isPanelOpen;
    private PanelSource _currentPanelSource = PanelSource.LevelUp;

    private void Awake()
    {
        ResolvePanelRootIfNeeded();

        AutoResolveOptionButtonsIfNeeded();
        TryResolvePlayer();
        TryResolvePlayerLevel();
        TryResolveAugmentController();
        EnsurePanelCanvasGroup();
        HidePanel();
    }

    private void OnEnable()
    {
        TrySubscribeToPlayer();
    }

    private void Start()
    {
        // In some scene setups Player is created/enabled after this UI.
        TrySubscribeToPlayer();
        AutoResolveOptionButtonsIfNeeded();
    }

    private void OnDisable()
    {
        UnsubscribeFromPlayer();

        if (pauseGameWhenPanelOpen && Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = _previousTimeScale;
    }

    private bool ShowPanel(PanelSource source)
    {
        if (_isPanelOpen) return false;

        if (panelRoot == null)
        {
            Debug.LogWarning("AugmentSelectionUI: panelRoot is not assigned.");
            return false;
        }

        if (optionButtons == null || optionButtons.Length == 0)
        {
            Debug.LogWarning("AugmentSelectionUI: optionButtons are not assigned.");
            return false;
        }

        List<AugmentDefinition> options = BuildRandomAugmentOptions(optionButtons.Length);
        if (options.Count == 0)
        {
            Debug.LogWarning("AugmentSelectionUI: no matching available augments found in AugmentDatabase. Check database assignment and entries.");
            return false;
        }

        _currentPanelSource = source;
        ApplyPanelTheme(_currentPanelSource);

        for (int i = 0; i < optionButtons.Length; i++)
        {
            AugmentDefinition option = i < options.Count ? options[i] : null;
            optionButtons[i].SetOption(option, HandleAugmentSelected);
        }

        ShowPanelRoot();
        _isPanelOpen = true;

        if (pauseGameWhenPanelOpen)
        {
            _previousTimeScale = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
            Time.timeScale = 0f;
        }

        return true;
    }

    private void ShowPanel()
    {
        ShowPanel(PanelSource.LevelUp);
    }

    public bool TryShowWallLootGiftPanel()
    {
        return ShowPanel(PanelSource.WallLootGift);
    }

    private void HidePanel()
    {
        if (panelRoot == null) return;
        EnsurePanelCanvasGroup();
        ApplyPanelTheme(PanelSource.LevelUp);
        _panelCanvasGroup.alpha = 0f;
        _panelCanvasGroup.interactable = false;
        _panelCanvasGroup.blocksRaycasts = false;
        if (deactivatePanelRootWhenHidden && CanDeactivatePanelRootWithoutDisablingListener() && panelRoot.activeSelf)
            panelRoot.SetActive(false);
    }

    private List<AugmentDefinition> BuildRandomAugmentOptions(int maxOptions)
    {
        List<AugmentDefinition> candidates = BuildAvailableAugmentOptions();
        if (candidates.Count <= 1)
            return candidates;

        ShuffleInPlace(candidates);

        int takeCount = Mathf.Clamp(maxOptions, 1, candidates.Count);
        if (takeCount >= candidates.Count)
            return candidates;

        return candidates.GetRange(0, takeCount);
    }

    private List<AugmentDefinition> BuildAvailableAugmentOptions()
    {
        List<AugmentDefinition> options = new List<AugmentDefinition>(augmentDatabase != null && augmentDatabase.allAugments != null
            ? augmentDatabase.allAugments.Count
            : 0);
        if (augmentDatabase == null || augmentDatabase.allAugments == null)
        {
            Debug.LogWarning("AugmentSelectionUI: augmentDatabase is not assigned.");
            return options;
        }

        for (int i = 0; i < augmentDatabase.allAugments.Count; i++)
        {
            AugmentDefinition definition = augmentDatabase.allAugments[i];
            if (definition == null || definition.id == AugmentId.None)
                continue;
            if (playerAugmentController != null && !playerAugmentController.CanApplyAugment(definition))
                continue;
            options.Add(definition);
        }

        return options;
    }

    private static void ShuffleInPlace(List<AugmentDefinition> list)
    {
        if (list == null || list.Count <= 1) return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void ApplyPanelTheme(PanelSource source)
    {
        if (panelThemeTargetImage == null) return;
        panelThemeTargetImage.color = source == PanelSource.WallLootGift
            ? wallLootGiftPanelThemeColor
            : levelUpPanelThemeColor;
    }

    private void HandleAugmentSelected(AugmentDefinition selectedAugment)
    {
        if (playerAugmentController != null)
            playerAugmentController.ApplyAugment(selectedAugment);

        HidePanel();
        _isPanelOpen = false;

        if (pauseGameWhenPanelOpen)
            Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
    }

    private void TryResolvePlayer()
    {
        if (player != null) return;
        player = Object.FindAnyObjectByType<Player>();
    }

    private void TryResolvePlayerLevel()
    {
        if (playerLevel != null) return;
        if (player != null)
            playerLevel = player.PlayerLevel;
        if (playerLevel == null)
            playerLevel = Object.FindAnyObjectByType<PlayerLevel>();
    }

    private void TryResolveAugmentController()
    {
        if (playerAugmentController != null) return;
        if (player != null)
            playerAugmentController = player.PlayerAugmentController;
        if (playerAugmentController == null)
            playerAugmentController = Object.FindAnyObjectByType<PlayerAugmentController>();
    }

    private void TrySubscribeToPlayer()
    {
        if (_isSubscribed) return;

        TryResolvePlayer();
        TryResolvePlayerLevel();
        TryResolveAugmentController();
        if (playerLevel == null)
        {
            Debug.LogWarning("AugmentSelectionUI: PlayerLevel could not be found, level-up event not subscribed.");
            return;
        }

        playerLevel.LevelUpAugmentSelectionRequested += ShowPanel;
        _isSubscribed = true;
    }

    private void UnsubscribeFromPlayer()
    {
        if (!_isSubscribed || playerLevel == null) return;
        playerLevel.LevelUpAugmentSelectionRequested -= ShowPanel;
        _isSubscribed = false;
    }

    private void ShowPanelRoot()
    {
        EnsurePanelCanvasGroup();
        if (!panelRoot.activeSelf)
            panelRoot.SetActive(true);
        _panelCanvasGroup.alpha = 1f;
        _panelCanvasGroup.interactable = true;
        _panelCanvasGroup.blocksRaycasts = true;
    }

    private void EnsurePanelCanvasGroup()
    {
        if (panelRoot == null) return;
        if (_panelCanvasGroup != null) return;
        _panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
        if (_panelCanvasGroup == null)
            _panelCanvasGroup = panelRoot.AddComponent<CanvasGroup>();
    }

    private void ResolvePanelRootIfNeeded()
    {
        if (panelRoot != null) return;

        panelRoot = gameObject;
        AugmentOptionButton[] ownButtons = panelRoot.GetComponentsInChildren<AugmentOptionButton>(true);
        if (ownButtons.Length > 0) return;

        Transform ancestor = transform.parent;
        while (ancestor != null)
        {
            if (ancestor.GetComponentsInChildren<AugmentOptionButton>(true).Length > 0)
            {
                panelRoot = ancestor.gameObject;
                return;
            }

            ancestor = ancestor.parent;
        }
    }

    private bool CanDeactivatePanelRootWithoutDisablingListener()
    {
        if (panelRoot == null || panelRoot == gameObject)
            return false;

        bool thisObjectWillBeDisabled = transform.IsChildOf(panelRoot.transform);
        if (thisObjectWillBeDisabled)
        {
            Debug.LogWarning(
                "AugmentSelectionUI: panelRoot deactivated this object would also be disabled, so level-up events would stop. " +
                "Keep this script on an always-active object (e.g. AugmentCanvas) and assign panelRoot to the visual panel.");
            return false;
        }

        return true;
    }

    private void AutoResolveOptionButtonsIfNeeded()
    {
        if (panelRoot == null) return;
        if (optionButtons != null && optionButtons.Length > 0) return;
        optionButtons = panelRoot.GetComponentsInChildren<AugmentOptionButton>(true);
    }

    [ContextMenu("Debug/Show Augment Panel")]
    private void DebugShowAugmentPanel()
    {
        ShowPanel();
    }

}
