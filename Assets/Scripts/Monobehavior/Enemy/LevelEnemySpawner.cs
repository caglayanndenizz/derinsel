using System.Collections.Generic;
using UnityEngine;

public class LevelEnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public struct SpawnEntry
    {
        public Transform spawnPoint;
        public Enemy.EnemyType enemyType;
    }

    [SerializeField] private List<SpawnEntry> spawnEntries = new();

    private int _aliveCount;

    private void Start()
    {
        SpawnAll();
    }

    private void SpawnAll()
    {
        _aliveCount = 0;

        foreach (SpawnEntry entry in spawnEntries)
        {
            if (entry.spawnPoint == null) continue;

            GameObject go = EnemyObjectPooler.Instance?.GetEnemyOfType(
                entry.enemyType,
                entry.spawnPoint.position,
                Quaternion.identity);

            if (go == null) continue;

            Enemy enemy = go.GetComponent<Enemy>();
            if (enemy == null) continue;

            _aliveCount++;
            enemy.Died += OnEnemyDied;
        }

        if (_aliveCount == 0)
            LevelManager.Instance?.OnMiniBossKilled();
    }

    private void OnEnemyDied(Enemy enemy)
    {
        enemy.Died -= OnEnemyDied;
        _aliveCount = Mathf.Max(0, _aliveCount - 1);

        if (_aliveCount == 0)
            LevelManager.Instance?.OnMiniBossKilled();
    }
}
