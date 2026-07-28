using UnityEngine;
using UnityEngine.UI;

public class OptionsPanelController : MonoBehaviour
{
    [SerializeField] private MainMenuController menuController;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle windowedToggle;
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private Button backButton;

    private void Awake()
    {
        fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggleChanged);
        windowedToggle.onValueChanged.AddListener(OnWindowedToggleChanged);
        brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        backButton.onClick.AddListener(OnBackClicked);
    }

    private void OnEnable()
    {
        brightnessSlider.minValue = SettingsManager.MinBrightness;
        brightnessSlider.maxValue = SettingsManager.MaxBrightness;
        brightnessSlider.SetValueWithoutNotify(SettingsManager.GetBrightness());

        bool isFullscreen = SettingsManager.GetFullscreen();
        fullscreenToggle.SetIsOnWithoutNotify(isFullscreen);
        windowedToggle.SetIsOnWithoutNotify(!isFullscreen);
    }

    public void OnFullscreenToggleChanged(bool isOn)
    {
        if (isOn)
            SettingsManager.SetFullscreen(true);
    }

    public void OnWindowedToggleChanged(bool isOn)
    {
        if (isOn)
            SettingsManager.SetFullscreen(false);
    }

    public void OnBrightnessChanged(float value)
    {
        SettingsManager.SetBrightness(value);
    }

    public void OnBackClicked()
    {
        menuController.ShowMainPanel();
    }
}
