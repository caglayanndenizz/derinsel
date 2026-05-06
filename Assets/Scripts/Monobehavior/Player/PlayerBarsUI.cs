using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerBarsUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private PlayerLevel playerLevel;
    [SerializeField] private PlayerCurrency playerCurrency;
    [SerializeField] private Slider healthBar;
    [SerializeField] private Slider experienceBar;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text goldText;

    private void Awake()
    {
        if (player == null)
            player = Object.FindAnyObjectByType<Player>();
        if (playerLevel == null && player != null)
            playerLevel = player.PlayerLevel;
        if (playerLevel == null)
            playerLevel = Object.FindAnyObjectByType<PlayerLevel>();
        if (playerCurrency == null && player != null)
            playerCurrency = player.PlayerCurrency;
        if (playerCurrency == null)
            playerCurrency = Object.FindAnyObjectByType<PlayerCurrency>();
    }

    private void OnEnable()
    {
        BindPlayer();
        PushInitialValues();
    }

    private void OnDisable()
    {
        UnbindPlayer();
    }

    private void BindPlayer()
    {
        if (player == null) return;
        player.HealthChanged += HandleHealthChanged;
        if (playerCurrency != null)
            playerCurrency.GoldChanged += HandleGoldChanged;
        if (playerLevel != null)
        {
            playerLevel.ExperienceChanged += HandleExperienceChanged;
            playerLevel.LevelChanged += HandleLevelChanged;
        }
    }

    private void UnbindPlayer()
    {
        if (player == null) return;
        player.HealthChanged -= HandleHealthChanged;
        if (playerCurrency != null)
            playerCurrency.GoldChanged -= HandleGoldChanged;
        if (playerLevel != null)
        {
            playerLevel.ExperienceChanged -= HandleExperienceChanged;
            playerLevel.LevelChanged -= HandleLevelChanged;
        }
    }

    private void PushInitialValues()
    {
        if (player == null) return;
        player.NotifyHealthChanged();
        if (playerCurrency != null)
            playerCurrency.NotifyGoldChanged();
        if (playerLevel != null)
        {
            playerLevel.NotifyExperienceChanged();
            playerLevel.NotifyLevelChanged();
        }
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        if (healthBar == null) return;
        healthBar.maxValue = Mathf.Max(1f, maxHealth);
        healthBar.value = Mathf.Clamp(currentHealth, 0f, healthBar.maxValue);
    }

    private void HandleExperienceChanged(float currentExperience, float requiredExperience)
    {
        if (experienceBar == null) return;
        experienceBar.maxValue = Mathf.Max(1f, requiredExperience);
        experienceBar.value = Mathf.Clamp(currentExperience, 0f, experienceBar.maxValue);
    }

    private void HandleLevelChanged(int level)
    {
        if (levelText == null) return;
        levelText.text = $"Level: {Mathf.Max(1, level)}";
    }

    private void HandleGoldChanged(float gold)
    {
        if (goldText == null) return;
        goldText.text = $"Gold: {Mathf.FloorToInt(Mathf.Max(0f, gold))}";
    }
}
