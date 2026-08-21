using System.Collections.Generic;
using UnityEngine;

public class KillCounter : MonoBehaviour
{
    public static KillCounter Instance { get; private set; }

    [Header("Unlock Chest")]
    [SerializeField] private GameObject unlockChestPrefab;
    [SerializeField] private int[] milestones = { 10, 25, 50, 75, 100 };

    [Header("Gold Milestone Chest (Spoils of War augment)")]
    [SerializeField] private GameObject silverChestPrefab;
    [SerializeField] private int goldChestInterval = 5;

    private int _totalKills;
    private int _nextMilestoneIndex;
    private int _totalLootGold;
    private readonly List<GameObject> _spawnedChests = new();
    private PlayerAugmentController _playerAugmentController;

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

    /// <summary>Called when the player picks up loot gold (enemy drops / breakables) — excludes gold gained from selling augments.</summary>
    public void RegisterLootGold(int amount, Vector3 position)
    {
        _totalLootGold += amount;

        if (goldChestInterval > 0 && _totalLootGold % goldChestInterval == 0 && HasGoldMilestoneChestAugment())
            SpawnSilverChest(position);
    }

    private bool HasGoldMilestoneChestAugment()
    {
        if (_playerAugmentController == null)
            _playerAugmentController = Object.FindAnyObjectByType<PlayerAugmentController>();
        return _playerAugmentController != null && _playerAugmentController.HasGoldMilestoneChest;
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
        _totalLootGold = 0;
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

    private void SpawnSilverChest(Vector3 position)
    {
        if (silverChestPrefab == null)
        {
            Debug.LogWarning("KillCounter: silverChestPrefab atanmamis.");
            return;
        }
        GameObject chest = Instantiate(silverChestPrefab, position, Quaternion.identity);
        _spawnedChests.Add(chest);
    }
}
