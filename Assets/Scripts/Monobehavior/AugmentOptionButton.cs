using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AugmentOptionButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

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

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClicked);
        }
    }

    private void HandleClicked()
    {
        if (_definition == null) return;
        _onSelected?.Invoke(_definition);
    }
}
