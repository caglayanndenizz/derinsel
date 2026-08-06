using System.Collections.Generic;
using UnityEngine;

public class AugmentSelectionUI : MonoBehaviour
{
    private enum PanelSource
    {
        UnlockOffer,
        ChestWooden,
        ChestSilver,
        ChestGold,
    }

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private PlayerAugmentController playerAugmentController;
    [SerializeField] private AugmentDatabase augmentDatabase;
    [SerializeField] private AugmentWeightSystem weightSystem;

    [Header("Chest tier panel roots")]
    [Tooltip("Each root already contains its own hand-placed AugmentOptionButton cards (and its own reroll Button child). Only the matching tier's root is shown; the others stay inactive.")]
    [SerializeField] private GameObject woodenPanelRoot;
    [SerializeField] private GameObject silverPanelRoot;
    [SerializeField] private GameObject goldPanelRoot;
    [SerializeField] private GameObject unlockPanelRoot;

    [Header("Augment cards")]
    [SerializeField] private int baseOptionCount = 3;
    [SerializeField] private int unlockedOptionCount = 4;

    [Header("Behavior")]
    [SerializeField] private bool pauseGameWhenPanelOpen = true;
    [SerializeField] private bool deactivatePanelRootWhenHidden = true;
    [Tooltip("Seconds of real time after the panel opens before buttons become clickable. Prevents accidental selection.")]
    [SerializeField][Range(0.5f, 2f)] private float buttonActivationDelay = 1f;

    [Header("Reroll")]
    [Tooltip("Name of the reroll Button child inside each panel root. Looked up per active tier so each panel's own reroll button is used.")]
    [SerializeField] private string rerollButtonChildName = "Button";

    private float _previousTimeScale = 1f;
    private float _panelOpenedAt = -999f;
    private bool _isPanelOpen;
    public bool IsPanelOpen => _isPanelOpen;
    private List<AugmentDefinition> _currentOffer;
    private bool _rerollUsedForCurrentOffer;
    private bool _rerollLockedAsUnlockOffer;
    private int _rerollLockedChestRarity;
    private bool _rerollAllowedForCurrentOffer = true;

    public bool CanRerollCurrentOffer => _isPanelOpen && !_rerollUsedForCurrentOffer && _rerollAllowedForCurrentOffer;

    private GameObject _activeRoot;
    private CanvasGroup _activeCanvasGroup;
    private AugmentOptionButton[] _activeButtons;
    private GameObject _activeRerollButton;

    private int _chestRarityFilter;
    private bool _isChestPanel;
    private bool _isUnlockChestPanel;
    public event System.Action OnChestAugmentSelected;

    private void Awake()
    {
        TryResolvePlayer();
        TryResolveAugmentController();
        HideAllPanelsImmediately();
    }

    private void OnDisable()
    {
        if (pauseGameWhenPanelOpen && Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = _previousTimeScale;
    }

    private GameObject GetTierRoot(PanelSource source)
    {
        switch (source)
        {
            case PanelSource.ChestWooden: return woodenPanelRoot;
            case PanelSource.ChestSilver: return silverPanelRoot;
            case PanelSource.ChestGold:   return goldPanelRoot;
            case PanelSource.UnlockOffer: return unlockPanelRoot;
            default: return null;
        }
    }

    private void HideAllPanelsImmediately()
    {
        if (woodenPanelRoot != null) woodenPanelRoot.SetActive(false);
        if (silverPanelRoot != null) silverPanelRoot.SetActive(false);
        if (goldPanelRoot   != null) goldPanelRoot.SetActive(false);
        if (unlockPanelRoot != null) unlockPanelRoot.SetActive(false);
    }

    private bool ShowPanel(PanelSource source, bool allowReroll = true)
    {
        if (_isPanelOpen) return false;

        GameObject root = GetTierRoot(source);
        if (root == null)
        {
            Debug.LogWarning($"AugmentSelectionUI: panel root for {source} is not assigned.");
            return false;
        }

        TryResolveAugmentController();

        AugmentOptionButton[] buttons = root.GetComponentsInChildren<AugmentOptionButton>(true);
        if (buttons.Length == 0)
        {
            Debug.LogWarning($"AugmentSelectionUI: {root.name} has no AugmentOptionButton children.");
            return false;
        }

        int slotCount = GetDesiredOptionSlotCount();
        if (buttons.Length < slotCount)
            Debug.LogWarning($"AugmentSelectionUI: {root.name} only has {buttons.Length} AugmentOptionButton slots but {slotCount} were requested.");
        int usableSlots = Mathf.Min(slotCount, buttons.Length);

        List<AugmentDefinition> options = BuildOfferOptions(usableSlots);
        if (options.Count == 0)
        {
            Debug.LogWarning(
                "AugmentSelectionUI: no matching available augments found in AugmentDatabase. Check database assignment and entries.");
            return false;
        }
        _currentOffer = options;
        _rerollUsedForCurrentOffer = false;
        _rerollLockedAsUnlockOffer = _isUnlockChestPanel || (options.Count > 0 && options[0] is UnlockAugmentDefinition);
        _rerollLockedChestRarity   = _chestRarityFilter;
        _rerollAllowedForCurrentOffer = allowReroll;

        GameObject rerollButton = FindDescendantNamed(root.transform, rerollButtonChildName)?.gameObject;
        if (rerollButton != null) rerollButton.SetActive(allowReroll);

        for (int i = 0; i < buttons.Length; i++)
        {
            AugmentDefinition option = i < usableSlots && i < options.Count ? options[i] : null;
            buttons[i].SetOption(option, HandleAugmentSelected);
        }

        ShowPanelRoot(root);
        _activeRoot = root;
        _activeButtons = buttons;
        _activeRerollButton = rerollButton;
        _isPanelOpen = true;

        if (pauseGameWhenPanelOpen)
        {
            _previousTimeScale = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
            Time.timeScale = 0f;
        }

        return true;
    }

    public bool TryShowUnlockChestPanel(bool allowReroll = true)
    {
        _isUnlockChestPanel = true;
        bool result = ShowPanel(PanelSource.UnlockOffer, allowReroll);
        if (!result) _isUnlockChestPanel = false;
        return result;
    }

    public bool ShowChestPanel(int rarity, bool allowReroll = true)
    {
        _chestRarityFilter = rarity;
        PanelSource source = rarity == 3 ? PanelSource.ChestGold
                           : rarity == 2 ? PanelSource.ChestSilver
                           : PanelSource.ChestWooden;
        bool result = ShowPanel(source, allowReroll);
        if (!result) _chestRarityFilter = 0;
        if (result) _isChestPanel = true;
        return result;
    }

    // ── Reroll ──

    public bool TryRerollOffer()
    {
        if (!_isPanelOpen || _rerollUsedForCurrentOffer || !_rerollAllowedForCurrentOffer || _activeButtons == null) return false;

        int slotCount = Mathf.Min(GetDesiredOptionSlotCount(), _activeButtons.Length);
        List<AugmentDefinition> options = BuildRerollOfferOptions(slotCount);
        if (options.Count == 0) return false;

        _rerollUsedForCurrentOffer = true;
        _currentOffer = options;
        if (_activeRerollButton != null) _activeRerollButton.SetActive(false);

        for (int i = 0; i < _activeButtons.Length; i++)
        {
            AugmentDefinition option = i < slotCount && i < options.Count ? options[i] : null;
            _activeButtons[i].SetOption(option, HandleAugmentSelected);
        }

        return true;
    }

    /// <summary>Rerolls within the exact same pool/tier the current offer was locked to at open time — never widens to a different chest rarity or unlock/regular pool.</summary>
    private List<AugmentDefinition> BuildRerollOfferOptions(int slotCount)
    {
        TryResolveWeightSystem();
        if (_rerollLockedAsUnlockOffer && weightSystem != null)
            return weightSystem.BuildUnlockOffer(playerAugmentController, slotCount);
        if (_rerollLockedChestRarity > 0 && weightSystem != null)
            return weightSystem.BuildChestOffer(_rerollLockedChestRarity, playerAugmentController, slotCount);
        if (weightSystem != null)
            return weightSystem.BuildRegularOffer(playerAugmentController, slotCount);
        return BuildRandomAugmentOptions(slotCount);
    }

    /// <summary>UnityEvent-friendly wrapper for UI Button OnClick — Unity's OnClick() dropdown only lists void methods.</summary>
    public void RerollButtonClicked()
    {
        TryRerollOffer();
    }

    private void HidePanel()
    {
        if (_activeRoot == null) return;

        if (_activeCanvasGroup != null)
        {
            _activeCanvasGroup.alpha = 0f;
            _activeCanvasGroup.interactable = false;
            _activeCanvasGroup.blocksRaycasts = false;
        }

        if (deactivatePanelRootWhenHidden && _activeRoot.activeSelf)
            _activeRoot.SetActive(false);

        _activeRoot = null;
        _activeCanvasGroup = null;
        _activeButtons = null;
        _activeRerollButton = null;
    }

    private int GetDesiredOptionSlotCount()
    {
        if (playerAugmentController != null && playerAugmentController.HasExtraAugmentSlotUnlock)
            return Mathf.Max(1, unlockedOptionCount);
        return Mathf.Max(1, baseOptionCount);
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

    private List<AugmentDefinition> BuildOfferOptions(int slotCount)
    {
        TryResolveWeightSystem();
        if (_isUnlockChestPanel && weightSystem != null)
            return weightSystem.BuildUnlockOffer(playerAugmentController, slotCount);
        if (_chestRarityFilter > 0 && weightSystem != null)
            return weightSystem.BuildChestOffer(_chestRarityFilter, playerAugmentController, slotCount);
        if (weightSystem != null)
            return weightSystem.BuildOffer(playerAugmentController, slotCount);
        return BuildRandomAugmentOptions(slotCount);
    }

    private void TryResolveWeightSystem()
    {
        if (weightSystem != null) return;
        weightSystem = AugmentWeightSystem.Instance;
        if (weightSystem == null)
            weightSystem = Object.FindAnyObjectByType<AugmentWeightSystem>();
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
        List<AugmentDefinition> opts = new List<AugmentDefinition>();
        if (augmentDatabase == null)
        {
            Debug.LogWarning("AugmentSelectionUI: augmentDatabase is not assigned.");
            return opts;
        }

        foreach (AugmentDefinition definition in augmentDatabase.AllRegularAugments)
        {
            if (definition == null || definition.id == AugmentId.None)
                continue;
            if (playerAugmentController != null && !playerAugmentController.CanApplyAugment(definition))
                continue;
            if (playerAugmentController != null &&
                playerAugmentController.HasRadialLongbowMutationUnlock &&
                definition.excludeFromAugmentPickerWhenRadialLongbowMutationComplete)
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

    private void HandleAugmentSelected(AugmentDefinition selectedAugment)
    {
        if (Time.unscaledTime - _panelOpenedAt < buttonActivationDelay) return;

        if (playerAugmentController != null)
            playerAugmentController.ApplyAugment(selectedAugment);

        TryResolveWeightSystem();
        if (weightSystem != null)
            weightSystem.RecordOfferOutcome(selectedAugment, _currentOffer);

        bool wasChestPanel = _isChestPanel;
        _isChestPanel = false;
        _isUnlockChestPanel = false;
        _chestRarityFilter = 0;

        HidePanel();
        _isPanelOpen = false;

        if (pauseGameWhenPanelOpen)
            Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;

        if (wasChestPanel)
        {
            OnChestAugmentSelected?.Invoke();
            return;
        }

    }

    private void TryResolvePlayer()
    {
        if (player != null) return;
        player = Object.FindAnyObjectByType<Player>();
    }

    private void TryResolveAugmentController()
    {
        if (playerAugmentController != null) return;
        if (player != null)
            playerAugmentController = player.PlayerAugmentController;
        if (playerAugmentController == null)
            playerAugmentController = Object.FindAnyObjectByType<PlayerAugmentController>();
    }

    private void ShowPanelRoot(GameObject root)
    {
        if (!root.activeSelf)
            root.SetActive(true);

        _activeCanvasGroup = root.GetComponent<CanvasGroup>();
        if (_activeCanvasGroup == null)
            _activeCanvasGroup = root.AddComponent<CanvasGroup>();

        _activeCanvasGroup.alpha = 1f;
        _activeCanvasGroup.interactable = true;
        _activeCanvasGroup.blocksRaycasts = true;
        _panelOpenedAt = Time.unscaledTime;
    }

    [ContextMenu("Debug/Show Wooden Chest Panel")]
    private void DebugShowAugmentPanel()
    {
        ShowChestPanel(1);
    }
}
