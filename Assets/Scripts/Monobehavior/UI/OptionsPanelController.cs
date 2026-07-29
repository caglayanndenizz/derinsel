using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class OptionsPanelController : MonoBehaviour
{
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private Toggle soundToggle;
    [SerializeField] private Button backButton;
    [SerializeField] private UnityEvent onBack;

    private void Awake()
    {
        brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        soundToggle.onValueChanged.AddListener(OnSoundToggleChanged);
        backButton.onClick.AddListener(OnBackClicked);
    }

    private void OnEnable()
    {
        brightnessSlider.minValue = SettingsManager.MinBrightness;
        brightnessSlider.maxValue = SettingsManager.MaxBrightness;
        brightnessSlider.SetValueWithoutNotify(SettingsManager.GetBrightness());

        soundToggle.SetIsOnWithoutNotify(!SettingsManager.GetMuted());
    }

    public void OnBrightnessChanged(float value)
    {
        SettingsManager.SetBrightness(value);
    }

    public void OnSoundToggleChanged(bool isOn)
    {
        SettingsManager.SetMuted(!isOn);
    }

    public void OnBackClicked()
    {
        onBack.Invoke();
    }
}
