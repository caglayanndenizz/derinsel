using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AugmentOptionButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Rarity Colors")]
    [SerializeField] private Color commonColor    = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] private Color rareColor      = new Color(0.40f, 0.70f, 1.00f, 1f);
    [SerializeField] private Color extraordinaryColor = new Color(0.40f, 0.00f, 0.60f, 1f);

    private AugmentDefinition _definition;
    private System.Action<AugmentDefinition> _onSelected;

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

        ApplyRarityColor(_definition.rarity);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClicked);
        }
    }

    private void ApplyRarityColor(int rarity)
    {
        if (button == null || button.image == null) return;

        button.image.color = rarity switch
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
