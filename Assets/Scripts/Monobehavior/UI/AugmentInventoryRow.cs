using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One row in the augment inventory list — mirrors AugmentOptionButton but adds a stack count and a sellable-gated Sell button.</summary>
public class AugmentInventoryRow : MonoBehaviour
{
    [SerializeField] private Button   sellButton;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text countText;

    private AugmentDefinition _definition;
    private System.Action<AugmentDefinition> _onSellClicked;

    private void Awake()
    {
        if (sellButton == null)
            sellButton = GetComponent<Button>();
    }

    public void SetEntry(AugmentDefinition definition, int count, bool sellable, System.Action<AugmentDefinition> onSellClicked)
    {
        _definition    = definition;
        _onSellClicked = onSellClicked;

        bool isValid = _definition != null;
        gameObject.SetActive(isValid);
        if (!isValid) return;

        if (titleText != null)
            titleText.text = _definition.displayName;
        if (descriptionText != null)
            descriptionText.text = _definition.description;
        if (countText != null)
            countText.text = count > 1 ? $"x{count}" : string.Empty;

        if (sellButton != null)
        {
            sellButton.onClick.RemoveAllListeners();
            sellButton.gameObject.SetActive(sellable);
            if (sellable)
                sellButton.onClick.AddListener(HandleSellClicked);
        }
    }

    private void HandleSellClicked()
    {
        if (_definition == null) return;
        _onSellClicked?.Invoke(_definition);
    }
}
