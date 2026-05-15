using System.Collections.Generic;
using UnityEngine;

public class WallLootHandler : MonoBehaviour
{
    [Header("Wall Loot")]
    [SerializeField] private float wallLootDropGateChance = 0.05f;
    [SerializeField] private float wallLootGoldChance = 0.30f;
    [SerializeField] private float wallLootExpChance = 0.50f;
    [SerializeField] private float wallLootHealingPotionChance = 0.20f;
    [SerializeField] private int wallLootExpValue = 5;
    [SerializeField] private int maxGoldPerRoom = 10;
    [SerializeField] private int maxExpOrbsPerRoom = 10;
    [SerializeField] private int maxHealingPotionsPerRoom = 2;
    [SerializeField] public GameObject healingPotionPrefab;

    PlayerAugmentController _playerAugmentController;
    GoldLootPooler _goldLootPooler;
    ExperienceLootPooler _experienceLootPooler;

    int _wallGoldSpawnedThisRoom;
    int _wallExpOrbsSpawnedThisRoom;
    int _wallHealingPotionsSpawnedThisRoom;

    void Awake()
    {
        _playerAugmentController = GetComponent<PlayerAugmentController>();
        _goldLootPooler = GoldLootPooler.Instance ?? Object.FindAnyObjectByType<GoldLootPooler>();
        _experienceLootPooler = ExperienceLootPooler.Instance ?? Object.FindAnyObjectByType<ExperienceLootPooler>();
    }

    public void TrySpawnWallLootForBrokenWalls(List<Vector3> brokenWalls)
    {
        if (brokenWalls == null || brokenWalls.Count == 0) return;
        if (_playerAugmentController == null || !_playerAugmentController.HasWallLootsUnlock) return;

        float luckMult = _playerAugmentController.LuckMultiplier;
        foreach (Vector3 wallPosition in brokenWalls)
        {
            if (Random.value > Mathf.Clamp01(wallLootDropGateChance * luckMult))
                continue;
            TrySpawnSingleWallLoot(wallPosition);
        }
    }

    void TrySpawnSingleWallLoot(Vector3 spawnPosition)
    {
        float potionChance = _wallHealingPotionsSpawnedThisRoom < maxHealingPotionsPerRoom
            ? Mathf.Clamp01(wallLootHealingPotionChance) : 0f;
        float goldChance = _wallGoldSpawnedThisRoom < maxGoldPerRoom
            ? Mathf.Clamp01(wallLootGoldChance) : 0f;
        float expChance = _wallExpOrbsSpawnedThisRoom < maxExpOrbsPerRoom
            ? Mathf.Clamp01(wallLootExpChance) : 0f;
        float total = potionChance + goldChance + expChance;
        if (total <= 0.0001f) return;

        float roll = Random.value * total;

        if (roll < potionChance)
        {
            if (healingPotionPrefab == null) return;
            Instantiate(healingPotionPrefab, spawnPosition, Quaternion.identity);
            _wallHealingPotionsSpawnedThisRoom++;
            return;
        }

        roll -= potionChance;
        if (roll < goldChance)
        {
            if (_goldLootPooler != null && _goldLootPooler.GetGold(spawnPosition, Quaternion.identity) != null)
                _wallGoldSpawnedThisRoom++;
            return;
        }

        if (_experienceLootPooler == null) return;
        GameObject expObj = _experienceLootPooler.GetExperience(spawnPosition, Quaternion.identity);
        if (expObj == null) return;
        Lootable lootable = expObj.GetComponent<Lootable>();
        if (lootable != null)
            lootable.experienceValue = Mathf.Max(0, wallLootExpValue);
        _wallExpOrbsSpawnedThisRoom++;
    }

    public void ResetWallLootDropCounterForRoom()
    {
        _wallGoldSpawnedThisRoom = 0;
        _wallExpOrbsSpawnedThisRoom = 0;
        _wallHealingPotionsSpawnedThisRoom = 0;
    }
}
