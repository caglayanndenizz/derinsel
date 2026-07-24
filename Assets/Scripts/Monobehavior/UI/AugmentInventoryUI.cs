using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows every augment the player currently has (grouped by id with a stack count).
/// Two modes: read-only (opened via the L-key hotkey on any floor) and sellable
/// (opened by a resting-floor NPC) — selling is only wired up when sellable is true.
/// </summary>
public class AugmentInventoryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerAugmentController playerAugmentController;
    [SerializeField] private PlayerCurrency playerCurrency;
    [SerializeField] private GameObject panelRoot;

    [Header("Rows")]
    [Tooltip("Required. Rows are instantiated from this prefab under rowsContainer.")]
    [SerializeField] private AugmentInventoryRow rowPrefab;
    [SerializeField] private Transform rowsContainer;

    [Header("Buy Tab")]
    [Tooltip("The 2nd tab's button/content (chest purchase). Only shown when opened with sellable=true (resting-floor NPC) — hidden for the read-only L-key view everywhere else.")]
    [SerializeField] private GameObject buyTabRoot;

    [Header("Sell Pricing")]
    [Tooltip("Gold received when selling, indexed by AugmentDefinition.rarity (1=Common, 2=Rare, 3=Extraordinary). Values outside range clamp to the nearest end.")]
    [SerializeField] private float[] sellPriceByRarity = { 10f, 10f, 25f, 50f };

    [Header("Behavior")]
    [Tooltip("Pause Time.timeScale while open in sellable mode (NPC shop). Read-only L-key views never pause.")]
    [SerializeField] private bool pauseGameWhenSellable = true;

    private readonly List<AugmentInventoryRow> _rows = new();
    private float _previousTimeScale = 1f;
    private bool _isOpen;
    private bool _sellable;

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        TryResolveReferences();
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void Update()
    {
        if (!_isOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape))
            Hide();
    }

    private void TryResolveReferences()
    {
        if (playerAugmentController == null)
            playerAugmentController = Object.FindAnyObjectByType<PlayerAugmentController>();
        if (playerCurrency == null)
            playerCurrency = Object.FindAnyObjectByType<PlayerCurrency>();
    }

    public bool Show(bool sellable)
    {
        if (_isOpen) return false;
        if (panelRoot == null || rowPrefab == null || rowsContainer == null) return false;

        TryResolveReferences();
        if (playerAugmentController == null) return false;

        _sellable = sellable;
        RefreshRows();

        if (buyTabRoot != null)
            buyTabRoot.SetActive(sellable);

        panelRoot.SetActive(true);
        _isOpen = true;

        if (sellable && pauseGameWhenSellable)
        {
            _previousTimeScale = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
            Time.timeScale = 0f;
        }

        return true;
    }

    public void Hide()
    {
        if (!_isOpen) return;

        if (panelRoot != null)
            panelRoot.SetActive(false);
        _isOpen = false;

        if (_sellable && pauseGameWhenSellable)
            Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
    }

    private void RefreshRows()
    {
        List<(AugmentDefinition definition, int count)> grouped = BuildGroupedList();
        EnsureRowPool(grouped.Count);

        for (int i = 0; i < _rows.Count; i++)
        {
            if (i < grouped.Count)
                _rows[i].SetEntry(grouped[i].definition, grouped[i].count, _sellable, HandleSellClicked);
            else
                _rows[i].SetEntry(null, 0, false, null);
        }
    }

    private List<(AugmentDefinition definition, int count)> BuildGroupedList()
    {
        var result = new List<(AugmentDefinition definition, int count)>();
        IReadOnlyList<AugmentDefinition> applied = playerAugmentController.AppliedAugments;

        for (int i = 0; i < applied.Count; i++)
        {
            AugmentDefinition def = applied[i];
            if (def == null) continue;

            int existingIndex = -1;
            for (int j = 0; j < result.Count; j++)
            {
                if (result[j].definition.id == def.id) { existingIndex = j; break; }
            }

            if (existingIndex >= 0)
                result[existingIndex] = (result[existingIndex].definition, result[existingIndex].count + 1);
            else
                result.Add((def, 1));
        }

        return result;
    }

    private void EnsureRowPool(int needed)
    {
        while (_rows.Count < needed)
            _rows.Add(Instantiate(rowPrefab, rowsContainer));
    }

    private void HandleSellClicked(AugmentDefinition definition)
    {
        if (!_sellable || definition == null) return;
        if (playerAugmentController == null) return;
        if (!playerAugmentController.RemoveAugment(definition)) return;

        if (playerCurrency != null)
            playerCurrency.AddGold(GetSellPrice(definition));

        RefreshRows();
    }

    private float GetSellPrice(AugmentDefinition definition)
    {
        if (sellPriceByRarity == null || sellPriceByRarity.Length == 0) return 0f;
        int index = Mathf.Clamp(definition.rarity, 0, sellPriceByRarity.Length - 1);
        return Mathf.Max(0f, sellPriceByRarity[index]);
    }
}
