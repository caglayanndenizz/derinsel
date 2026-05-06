using System;
using UnityEngine;

public class PlayerLevel : MonoBehaviour
{
    [Header("Progression")]
    [SerializeField] private float experienceCount;
    [SerializeField] private float requiredExperienceForNextLevel = 100f;
    [SerializeField] private int currentLevel = 1;

    public event Action<float, float> ExperienceChanged;
    public event Action<int> LevelChanged;
    public event Action LevelUp;
    public event Action LevelUpAugmentSelectionRequested;

    public float ExperienceCount => experienceCount;
    public int CurrentLevel => Mathf.Max(1, currentLevel);
    public float RequiredExperienceForNextLevel => Mathf.Max(1f, requiredExperienceForNextLevel);

    private void Awake()
    {
        currentLevel = Mathf.Max(1, currentLevel);
        requiredExperienceForNextLevel = Mathf.Max(1f, requiredExperienceForNextLevel);
    }

    public void AddExperience(float amount)
    {
        if (amount <= 0f) return;

        experienceCount += amount;
        int levelUps = 0;
        float requiredExperience = Mathf.Max(1f, requiredExperienceForNextLevel);

        while (experienceCount >= requiredExperience)
        {
            experienceCount -= requiredExperience;
            levelUps++;
        }

        if (levelUps > 0)
        {
            currentLevel += levelUps;
            for (int i = 0; i < levelUps; i++)
                LevelUp?.Invoke();

            LevelChanged?.Invoke(Mathf.Max(1, currentLevel));
            LevelUpAugmentSelectionRequested?.Invoke();
        }

        ExperienceChanged?.Invoke(experienceCount, requiredExperience);
    }

    public void NotifyExperienceChanged()
    {
        ExperienceChanged?.Invoke(experienceCount, Mathf.Max(1f, requiredExperienceForNextLevel));
    }

    public void NotifyLevelChanged()
    {
        LevelChanged?.Invoke(Mathf.Max(1, currentLevel));
    }
}
