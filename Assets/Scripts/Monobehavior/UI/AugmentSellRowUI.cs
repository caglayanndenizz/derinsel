using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AugmentSellRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button sellButton;

    public void Setup(AugmentDefinition augment, int price, Action onSell)
    {
        nameText.text = augment.displayName;
        priceText.text = price.ToString();

        sellButton.onClick.RemoveAllListeners();
        sellButton.onClick.AddListener(() => onSell());
    }
}
