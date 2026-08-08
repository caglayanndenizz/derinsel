using System.Collections.Generic;
using UnityEngine;

public class LevelEnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public struct SpawnEntry
    {
        public Transform spawnPoint;
        public GameObject enemyPrefab;
    }

    [SerializeField] private List<SpawnEntry> spawnEntries = new();

    private int _aliveCount;
    private Vector3 _lastDeathPosition;

    private void Start()
    {
        SpawnAll();
    }

    private void SpawnAll()
    {
        _aliveCount = 0;

        foreach (SpawnEntry entry in spawnEntries)
        {
            if (entry.spawnPoint == null || entry.enemyPrefab == null) continue;

            GameObject go = EnemyObjectPooler.Instance?.GetEnemy(
                entry.enemyPrefab,
                entry.spawnPoint.position,
                Quaternion.identity);

            if (go == null) continue;

            Enemy enemy = go.GetComponent<Enemy>();
            if (enemy == null) continue;

            _aliveCount++;
            enemy.Died += OnEnemyDied;
        }

        if (_aliveCount == 0)
            LevelManager.Instance?.AdvanceToNextLevel();
    }

    private void OnEnemyDied(Enemy enemy)
    {
        enemy.Died -= OnEnemyDied;
        _lastDeathPosition = enemy.transform.position;
        KillCounter.Instance?.RegisterKill(_lastDeathPosition);
        _aliveCount = Mathf.Max(0, _aliveCount - 1);

        if (_aliveCount == 0)
            LevelManager.Instance?.SpawnLevelEndChest(_lastDeathPosition);
    }
}
