using UnityEngine;
using UnityEngine.UI;

public class PlayerBarsUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private Slider healthBar;
    [SerializeField] private Slider experienceBar;

    private void Awake()
    {
        if (player == null)
            player = Object.FindAnyObjectByType<Player>();
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
        player.ExperienceChanged += HandleExperienceChanged;
    }

    private void UnbindPlayer()
    {
        if (player == null) return;
        player.HealthChanged -= HandleHealthChanged;
        player.ExperienceChanged -= HandleExperienceChanged;
    }

    private void PushInitialValues()
    {
        if (player == null) return;
        player.NotifyHealthChanged();
        player.NotifyExperienceChanged();
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
}
