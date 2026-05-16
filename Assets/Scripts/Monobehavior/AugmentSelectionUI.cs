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
    [SerializeField] private AugmentWeightSystem weightSystem;
    [SerializeField] private GameObject panelRoot;

    [Header("Augment cards")]
    [Tooltip("Required. Cards are instantiated from this prefab under the resolved row parent.")]
    [SerializeField] private AugmentOptionButton optionButtonPrefab;
    [Tooltip("Optional RectTransform holding the row — e.g. child named CardsContainer. If unset, CardsContainer under panelRoot is searched, else an AugmentCards row is created automatically.")]
    [SerializeField] private Transform optionsContainer;
    [SerializeField] private int baseOptionCount = 3;
    [SerializeField] private int unlockedOptionCount = 4;
    [Tooltip("Runtime augment card size (LayoutElement + prefab root).")]
    [SerializeField] private float runtimeCardPreferredWidth = 400f;
    [SerializeField] private float runtimeCardPreferredHeight = 400f;
    [SerializeField] private float augmentRowSpacing = 24f;
    [SerializeField] private int augmentRowPaddingPx = 8;
    [Tooltip("Row is anchored to the center of PanelRoot and sized via ContentSizeFitter to fit the cards.")]
    [SerializeField] private bool forceAugmentRowToCenterViewport = true;

    [Header("Behavior")]
    [SerializeField] private bool pauseGameWhenPanelOpen = true;
    [SerializeField] private bool deactivatePanelRootWhenHidden = true;

    [Header("Visual theme")]
    [SerializeField] private Image panelThemeTargetImage;
    [SerializeField] private Color levelUpPanelThemeColor = Color.white;
    [SerializeField] private Color wallLootGiftPanelThemeColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    private const string AutoCardsRowName = "AugmentCards";
    private readonly List<AugmentOptionButton> _runtimeButtons = new();
    private RectTransform _runtimeCardsParent;
    private float _previousTimeScale = 1f;
    private bool _isSubscribed;
    private CanvasGroup _panelCanvasGroup;
    private bool _isPanelOpen;
    private List<AugmentDefinition> _currentOffer;
    private DungeonGenerator _dungeonGenerator;

    private bool UsesPrefabAugmentCards =>
        optionButtonPrefab != null && panelRoot != null;

    private void Awake()
    {
        ResolvePanelRootIfNeeded();

        TryResolveAugmentDynamicsOnAwake();
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

        TryResolveAugmentController();

        if (!UsesPrefabAugmentCards)
        {
            Debug.LogWarning(
                "AugmentSelectionUI: Assign panelRoot and optionButtonPrefab — augment cards are always spawned from the prefab.");
            return false;
        }

        int slotCount = GetDesiredOptionSlotCount();

        RefreshAugmentRowPositionAndLayout();
        EnsureButtonPool(slotCount);
        if (_runtimeButtons.Count < slotCount)
        {
            Debug.LogWarning(
                "AugmentSelectionUI: could not instantiate enough augment buttons; check PanelRoot and augment row RectTransform.");
            return false;
        }

        HideUnusedRuntimeButtons(slotCount);

        List<AugmentDefinition> options = BuildOfferOptions(slotCount);
        if (options.Count == 0)
        {
            Debug.LogWarning(
                "AugmentSelectionUI: no matching available augments found in AugmentDatabase. Check database assignment and entries.");
            return false;
        }
        _currentOffer = options;

        ApplyPanelTheme(source);

        for (int i = 0; i < slotCount; i++)
        {
            AugmentDefinition option = i < options.Count ? options[i] : null;
            _runtimeButtons[i].SetOption(option, HandleAugmentSelected);
        }

        ShowPanelRoot();
        if (GetRuntimeCardsParent() != null)
            RefreshRuntimeCardsLayout();
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

    private int GetDesiredOptionSlotCount()
    {
        if (playerAugmentController != null && playerAugmentController.HasExtraAugmentSlotUnlock)
            return Mathf.Max(1, unlockedOptionCount);
        return Mathf.Max(1, baseOptionCount);
    }

    private void TryResolveAugmentDynamicsOnAwake()
    {
        if (!UsesPrefabAugmentCards)
            return;

        RectTransform row = GetRuntimeCardsParent();
        if (row != null)
        {
            if (forceAugmentRowToCenterViewport)
                ApplyPreferredAugmentRowFrame(row);

            EnsureAugmentRowHorizontalLayout(row);
            HideLegacyAugmentButtonsOutsideRow(row);
        }
    }

    /// <summary>Re-run before each picker open so scene-assigned Containers pick up anchors + HorizontalLayout reliably.</summary>
    private void RefreshAugmentRowPositionAndLayout()
    {
        RectTransform row = GetRuntimeCardsParent();
        if (row == null)
            return;

        if (forceAugmentRowToCenterViewport)
            ApplyPreferredAugmentRowFrame(row);

        EnsureAugmentRowHorizontalLayout(row);
    }

    private RectTransform GetRuntimeCardsParent()
    {
        if (!UsesPrefabAugmentCards)
            return null;

        if (_runtimeCardsParent != null && _runtimeCardsParent.gameObject == null)
            _runtimeCardsParent = null;

        if (_runtimeCardsParent != null)
            return _runtimeCardsParent;

        if (optionsContainer != null &&
            (ReferenceEquals(gameObject, optionsContainer.gameObject) ||
             ReferenceEquals(panelRoot != null ? panelRoot.gameObject : null, optionsContainer.gameObject)))
        {
            Debug.LogWarning(
                "AugmentSelectionUI: optionsContainer should reference a PanelRoot child row (CardsContainer/AugmentCards), not PanelRoot nor this UI object.");
        }

        if (optionsContainer != null)
        {
            var explicitRect = optionsContainer as RectTransform;
            if (explicitRect != null)
            {
                _runtimeCardsParent = explicitRect;
                return _runtimeCardsParent;
            }
        }

        Transform named = FindDescendantNamed(panelRoot.transform, "CardsContainer")
                          ?? FindDescendantNamed(panelRoot.transform, "OptionsContainer");

        if (named != null)
        {
            RectTransform rect = named as RectTransform;
            if (rect != null)
            {
                _runtimeCardsParent = rect;
                if (optionsContainer == null)
                    optionsContainer = named;
                return _runtimeCardsParent;
            }

            Debug.LogWarning(
                $"AugmentSelectionUI: found '{named.name}' for augment row parent but it has no RectTransform; add one or use CardsContainer RectTransform.");
        }

        Transform auto = FindDescendantNamed(panelRoot.transform, AutoCardsRowName);
        if (auto != null && auto.TryGetComponent(out RectTransform autoRect))
        {
            _runtimeCardsParent = autoRect;
            return _runtimeCardsParent;
        }

        var rowGo = new GameObject(AutoCardsRowName, typeof(RectTransform));
        RectTransform row = rowGo.GetComponent<RectTransform>();
        row.SetParent(panelRoot.transform, false);
        row.SetSiblingIndex(panelRoot.transform.childCount - 1);
        ApplyPreferredAugmentRowFrame(row);
        row.localScale = Vector3.one;
        row.localRotation = Quaternion.identity;

        _runtimeCardsParent = row;
        return _runtimeCardsParent;
    }

    private static Transform FindDescendantNamed(Transform root, string name)
    {
        if (root == null)
            return null;

        var q = new Queue<Transform>();
        q.Enqueue(root);
        while (q.Count > 0)
        {
            Transform cur = q.Dequeue();
            if (cur.name == name)
                return cur;
            foreach (Transform c in cur)
                q.Enqueue(c);
        }

        return null;
    }

    /// <summary>Places the card row at the center of PanelRoot; width/height come from layout children + ContentSizeFitter.</summary>
    private void ApplyPreferredAugmentRowFrame(RectTransform row)
    {
        if (row == null)
            return;

        row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.anchoredPosition = Vector2.zero;
        row.sizeDelta = Vector2.zero;
        row.offsetMin = Vector2.zero;
        row.offsetMax = Vector2.zero;
        row.localScale = Vector3.one;
    }

    /// <summary>Horizontal stack + PreferredSize shrink-wrap for the centred AugmentCards row.</summary>
    private void EnsureAugmentRowHorizontalLayout(RectTransform row)
    {
        if (row == null)
            return;

        ContentSizeFitter csf = row.GetComponent<ContentSizeFitter>();
        if (csf == null)
            csf = row.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null)
            hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();

        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.reverseArrangement = false;
        hlg.spacing = augmentRowSpacing;
        hlg.childForceExpandHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        int p = Mathf.Max(0, augmentRowPaddingPx);
        hlg.padding = new RectOffset(p, p, p, p);
    }

    /// <summary>Hides old scene augment buttons left under PanelRoot outside the spawned row.</summary>
    private void HideLegacyAugmentButtonsOutsideRow(RectTransform rowParent)
    {
        if (panelRoot == null || rowParent == null)
            return;

        AugmentOptionButton[] all = panelRoot.GetComponentsInChildren<AugmentOptionButton>(true);
        for (int i = 0; i < all.Length; i++)
        {
            AugmentOptionButton b = all[i];
            if (b == null || b.transform.IsChildOf(rowParent))
                continue;
            b.gameObject.SetActive(false);
        }
    }

    private static void ConfigureRuntimeAugmentCardTransform(AugmentOptionButton btn, float prefW, float prefH)
    {
        Transform t = btn.transform;
        t.localScale = Vector3.one;
        t.localRotation = Quaternion.identity;

        LayoutElement le = btn.GetComponent<LayoutElement>();
        if (le == null)
            le = btn.gameObject.AddComponent<LayoutElement>();
        float w = Mathf.Max(1f, prefW);
        float h = Mathf.Max(1f, prefH);
        le.minWidth = w;
        le.preferredWidth = w;
        le.flexibleWidth = 0f;
        le.minHeight = h;
        le.preferredHeight = h;
        le.flexibleHeight = 0f;
    }

    private void RefreshRuntimeCardsLayout()
    {
        if (_runtimeCardsParent == null)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_runtimeCardsParent);
    }

    private void EnsureButtonPool(int needed)
    {
        RectTransform row = GetRuntimeCardsParent();
        if (row == null)
        {
            Debug.LogError(
                "AugmentSelectionUI: augment row RectTransform could not be resolved (PanelRoot?) — cannot instantiate option cards.");
            return;
        }

        EnsureAugmentRowHorizontalLayout(row);

        while (_runtimeButtons.Count < needed)
        {
            AugmentOptionButton btn = Instantiate(optionButtonPrefab, row);
            ConfigureRuntimeAugmentCardTransform(btn, runtimeCardPreferredWidth, runtimeCardPreferredHeight);
            _runtimeButtons.Add(btn);
        }

        for (int i = 0; i < _runtimeButtons.Count; i++)
        {
            if (_runtimeButtons[i] != null)
                ConfigureRuntimeAugmentCardTransform(_runtimeButtons[i], runtimeCardPreferredWidth, runtimeCardPreferredHeight);
        }
    }

    private void HideUnusedRuntimeButtons(int usedCount)
    {
        for (int i = usedCount; i < _runtimeButtons.Count; i++)
            _runtimeButtons[i].SetOption(null, HandleAugmentSelected);
    }

    private List<AugmentDefinition> BuildOfferOptions(int slotCount)
    {
        TryResolveWeightSystem();
        if (weightSystem != null)
            return weightSystem.BuildOffer(playerAugmentController, slotCount, GetCurrentFloor(), GetCurrentPlayerLevel());
        return BuildRandomAugmentOptions(slotCount);
    }

    private void TryResolveWeightSystem()
    {
        if (weightSystem != null) return;
        weightSystem = AugmentWeightSystem.Instance;
        if (weightSystem == null)
            weightSystem = Object.FindAnyObjectByType<AugmentWeightSystem>();
    }

    private int GetCurrentFloor()
    {
        if (_dungeonGenerator == null)
            _dungeonGenerator = Object.FindAnyObjectByType<DungeonGenerator>();
        return _dungeonGenerator != null ? _dungeonGenerator.CurrentFloor : 0;
    }

    private int GetCurrentPlayerLevel()
    {
        TryResolvePlayerLevel();
        return playerLevel != null ? playerLevel.CurrentLevel : 1;
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
        List<AugmentDefinition> opts = new List<AugmentDefinition>(
            augmentDatabase != null && augmentDatabase.allAugments != null
                ? augmentDatabase.allAugments.Count
                : 0);
        if (augmentDatabase == null || augmentDatabase.allAugments == null)
        {
            Debug.LogWarning("AugmentSelectionUI: augmentDatabase is not assigned.");
            return opts;
        }

        for (int i = 0; i < augmentDatabase.allAugments.Count; i++)
        {
            AugmentDefinition definition = augmentDatabase.allAugments[i];
            if (definition == null || definition.id == AugmentId.None)
                continue;
            if (playerAugmentController != null && !playerAugmentController.CanApplyAugment(definition))
                continue;
            if (playerAugmentController != null &&
                playerAugmentController.HasRadialBowMutationUnlock &&
                definition.excludeFromAugmentPickerWhenRadialBowMutationComplete)
                continue;
            opts.Add(definition);
        }

        return opts;
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

        TryResolveWeightSystem();
        if (weightSystem != null)
            weightSystem.NotifySelection(selectedAugment, _currentOffer);

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

    [ContextMenu("Debug/Show Augment Panel")]
    private void DebugShowAugmentPanel()
    {
        ShowPanel();
    }
}
