using System.Collections.Generic;
using UnityEngine;

public class KillCounter : MonoBehaviour
{
    public static KillCounter Instance { get; private set; }

    [Header("Unlock Chest")]
    [SerializeField] private GameObject unlockChestPrefab;
    [SerializeField] private int[] milestones = { 10, 25, 50, 75, 100 };

    private int _totalKills;
    private int _nextMilestoneIndex;
    private readonly List<GameObject> _spawnedChests = new();

    public int TotalKills => _totalKills;
    public event System.Action<int> KillCountChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void RegisterKill(Vector3 position)
    {
        _totalKills++;
        KillCountChanged?.Invoke(_totalKills);

        if (_nextMilestoneIndex < milestones.Length && _totalKills >= milestones[_nextMilestoneIndex])
        {
            _nextMilestoneIndex++;
            SpawnUnlockChest(position);
        }
    }

    public void CleanupChests()
    {
        foreach (GameObject chest in _spawnedChests)
            if (chest != null) Destroy(chest);
        _spawnedChests.Clear();
    }

    public void ResetForNewRun()
    {
        CleanupChests();
        _totalKills = 0;
        _nextMilestoneIndex = 0;
    }

    private void SpawnUnlockChest(Vector3 position)
    {
        if (unlockChestPrefab == null)
        {
            Debug.LogWarning("KillCounter: unlockChestPrefab atanmamis.");
            return;
        }
        GameObject chest = Instantiate(unlockChestPrefab, position, Quaternion.identity);
        _spawnedChests.Add(chest);
    }
}
