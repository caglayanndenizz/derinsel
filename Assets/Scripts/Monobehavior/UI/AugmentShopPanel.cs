using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Resting-floor shop panel: Sell tab lists owned augments, Buy tab sells the 3 chest rarities.</summary>
public class AugmentShopPanel : MonoBehaviour
{
    [Header("Tabs")]
    [SerializeField] private Button sellTabButton;
    [SerializeField] private Button buyTabButton;
    [SerializeField] private GameObject sellContent;
    [SerializeField] private GameObject buyContent;

    [Header("Sell")]
    [SerializeField] private Transform sellListParent;
    [SerializeField] private AugmentSellRowUI sellRowPrefab;
    [Tooltip("Sell price by augment rarity (index 0 = rarity 1).")]
    [SerializeField] private int[] sellPriceByRarity = { 10, 25, 50 };

    [Header("Buy")]
    [SerializeField] private ChestBuyRowUI woodenRow;
    [SerializeField] private ChestBuyRowUI silverRow;
    [SerializeField] private ChestBuyRowUI goldenRow;
    [SerializeField] private GameObject woodenChestPrefab;
    [SerializeField] private GameObject silverChestPrefab;
    [SerializeField] private GameObject goldenChestPrefab;
    [SerializeField] private int woodenPrice = 25;
    [SerializeField] private int silverPrice = 60;
    [SerializeField] private int goldenPrice = 120;
    [SerializeField] private Vector2 chestSpawnOffset = new Vector2(1.5f, 0f);

    private Player _player;
    private readonly List<AugmentSellRowUI> _spawnedRows = new();
    private float _previousTimeScale = 1f;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        sellTabButton.onClick.AddListener(ShowSellTab);
        buyTabButton.onClick.AddListener(ShowBuyTab);

        woodenRow.Setup("Wooden", woodenPrice, () => TryBuyChest(woodenChestPrefab, woodenPrice));
        silverRow.Setup("Silver", silverPrice, () => TryBuyChest(silverChestPrefab, silverPrice));
        goldenRow.Setup("Golden", goldenPrice, () => TryBuyChest(goldenChestPrefab, goldenPrice));
    }

    public void Show(Player player)
    {
        _previousTimeScale = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
        Time.timeScale = 0f;

        _player = player;
        gameObject.SetActive(true);
        ShowSellTab();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
    }

    private void ShowSellTab()
    {
        sellContent.SetActive(true);
        buyContent.SetActive(false);
        RefreshSellList();
    }

    private void ShowBuyTab()
    {
        sellContent.SetActive(false);
        buyContent.SetActive(true);
    }

    private void RefreshSellList()
    {
        foreach (AugmentSellRowUI row in _spawnedRows)
            if (row != null) Destroy(row.gameObject);
        _spawnedRows.Clear();

        if (_player == null || _player.PlayerAugmentController == null) return;

        foreach (AugmentDefinition augment in _player.PlayerAugmentController.AppliedAugments)
        {
            int price = GetSellPrice(augment);
            AugmentSellRowUI row = Instantiate(sellRowPrefab, sellListParent);
            row.Setup(augment, price, () => SellAugment(augment, price));
            _spawnedRows.Add(row);
        }
    }

    private int GetSellPrice(AugmentDefinition augment)
    {
        int index = Mathf.Clamp(augment.rarity - 1, 0, sellPriceByRarity.Length - 1);
        return sellPriceByRarity[index];
    }

    private void SellAugment(AugmentDefinition augment, int price)
    {
        if (_player == null) return;
        if (!_player.PlayerAugmentController.RemoveAugment(augment)) return;

        _player.PlayerCurrency.AddGold(price);
        RefreshSellList();
    }

    private void TryBuyChest(GameObject chestPrefab, int price)
    {
        if (_player == null || chestPrefab == null) return;
        if (!_player.PlayerCurrency.TrySpendGold(price)) return;

        Hide();

        Vector3 spawnPos = _player.transform.position + (Vector3)chestSpawnOffset;
        GameObject chest = Instantiate(chestPrefab, spawnPos, Quaternion.identity);

        ChestInteractable interactable = chest.GetComponent<ChestInteractable>();
        interactable?.OpenNow();

        AugmentSelectionUI augmentUI = FindAnyObjectByType<AugmentSelectionUI>();
        if (augmentUI == null) return;

        System.Action onSelected = null;
        onSelected = () =>
        {
            augmentUI.OnChestAugmentSelected -= onSelected;
            if (chest != null) Destroy(chest);
        };
        augmentUI.OnChestAugmentSelected += onSelected;
    }
}
