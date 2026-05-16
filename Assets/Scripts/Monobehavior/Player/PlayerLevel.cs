using System;
using UnityEngine;

public class PlayerLevel : MonoBehaviour
{
    [Header("Progression")]
    [SerializeField] private float baseExpForLevel2 = 60f;
    [SerializeField] private float xpGrowthRatePerLevel = 1.30f;

    [Header("Runtime State (Read-Only)")]
    [SerializeField] private float experienceCount;
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private float requiredExperienceForNextLevel;

    public event Action<float, float> ExperienceChanged;
    public event Action<int> LevelChanged;
    public event Action LevelUp;
    public event Action LevelUpAugmentSelectionRequested;

    public float ExperienceCount => experienceCount;
    public int CurrentLevel => currentLevel;
    public float RequiredExperienceForNextLevel => requiredExperienceForNextLevel;

    private void Awake()
    {
        currentLevel = Mathf.Max(1, currentLevel);
        requiredExperienceForNextLevel = ComputeRequiredXp(currentLevel);
    }

    // XP required to go from `level` to `level + 1`
    private float ComputeRequiredXp(int level)
    {
        return Mathf.Max(1f, baseExpForLevel2 * Mathf.Pow(xpGrowthRatePerLevel, level - 1));
    }

    public void AddExperience(float amount)
    {
        if (amount <= 0f) return;

        experienceCount += amount;
        bool leveledUp = false;

        while (experienceCount >= requiredExperienceForNextLevel)
        {
            experienceCount -= requiredExperienceForNextLevel;
            currentLevel++;
            requiredExperienceForNextLevel = ComputeRequiredXp(currentLevel);
            leveledUp = true;
            LevelUp?.Invoke();
        }

        if (leveledUp)
        {
            LevelChanged?.Invoke(currentLevel);
            LevelUpAugmentSelectionRequested?.Invoke();
        }

        ExperienceChanged?.Invoke(experienceCount, requiredExperienceForNextLevel);
    }

    public void Reset()
    {
        experienceCount = 0f;
        currentLevel = 1;
        requiredExperienceForNextLevel = ComputeRequiredXp(currentLevel);
        ExperienceChanged?.Invoke(experienceCount, requiredExperienceForNextLevel);
        LevelChanged?.Invoke(currentLevel);
    }

    public void NotifyExperienceChanged()
    {
        ExperienceChanged?.Invoke(experienceCount, requiredExperienceForNextLevel);
    }

    public void NotifyLevelChanged()
    {
        LevelChanged?.Invoke(currentLevel);
    }
}
