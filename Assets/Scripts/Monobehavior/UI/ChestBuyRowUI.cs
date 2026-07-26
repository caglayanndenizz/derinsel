using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChestBuyRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button buyButton;

    public void Setup(string chestName, int price, Action onBuy)
    {
        nameText.text = chestName;
        priceText.text = price.ToString();

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => onBuy());
    }
}
