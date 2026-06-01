using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AugmentOptionButton : MonoBehaviour
{
    [SerializeField] private Button   button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Rarity Colors")]
    [SerializeField] private Color commonColor        = new Color(0.75f, 0.75f, 0.75f, 1f); // Silver
    [SerializeField] private Color rareColor          = new Color(0.40f, 0.70f, 1.00f, 1f); // Blue
    [SerializeField] private Color extraordinaryColor = new Color(0.55f, 0.00f, 0.80f, 1f); // Purple
    [SerializeField] private Color unlockColor        = new Color(0.20f, 0.80f, 0.30f, 1f); // Green

    private AugmentDefinition                   _definition;
    private System.Action<AugmentDefinition>    _onSelected;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    public void SetOption(AugmentDefinition definition, System.Action<AugmentDefinition> onSelected)
    {
        _definition = definition;
        _onSelected = onSelected;

        bool isValid = _definition != null;
        gameObject.SetActive(isValid);
        if (!isValid) return;

        if (titleText != null)
            titleText.text = _definition.displayName;

        if (descriptionText != null)
            descriptionText.text = _definition.description;

        ApplySlotColor(_definition);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClicked);
        }
    }

    private void ApplySlotColor(AugmentDefinition definition)
    {
        if (button == null || button.image == null) return;

        // Unlock augments always use green regardless of any rarity field
        if (definition is UnlockAugmentDefinition)
        {
            button.image.color = unlockColor;
            return;
        }

        button.image.color = definition.rarity switch
        {
            1 => commonColor,
            2 => rareColor,
            3 => extraordinaryColor,
            _ => button.image.color
        };
    }

    private void HandleClicked()
    {
        if (_definition == null) return;
        _onSelected?.Invoke(_definition);
    }
}
