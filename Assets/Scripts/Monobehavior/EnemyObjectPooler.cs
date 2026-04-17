using System.Collections.Generic;
using UnityEngine;

public class EnemyObjectPooler : MonoBehaviour
{
    public static EnemyObjectPooler Instance { get; private set; }

    [Header("Pool Settings")]
    public GameObject enemyPrefab;
    public int initialPoolSize = 50;
    public bool canExpandPool = true;
    public Transform poolParent;

    private readonly Queue<GameObject> _availableEnemies = new Queue<GameObject>();
    private readonly HashSet<GameObject> _trackedEnemies = new HashSet<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        WarmupPool();
    }

    private void WarmupPool()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("EnemyObjectPooler: enemyPrefab atanmamis.");
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject enemy = CreateNewEnemy();
            ReturnEnemy(enemy);
        }
    }

    private GameObject CreateNewEnemy()
    {
        Transform parent = poolParent != null ? poolParent : transform;
        GameObject enemy = Instantiate(enemyPrefab, parent);
        enemy.SetActive(false);
        _trackedEnemies.Add(enemy);
        return enemy;
    }

    public GameObject GetEnemy(Vector3 worldPosition, Quaternion rotation)
    {
        if (_availableEnemies.Count == 0)
        {
            if (!canExpandPool)
            {
                Debug.LogWarning("EnemyObjectPooler: Havuzdaki enemy tükendi.");
                return null;
            }

            GameObject expandedEnemy = CreateNewEnemy();
            _availableEnemies.Enqueue(expandedEnemy);
        }

        GameObject enemy = _availableEnemies.Dequeue();
        enemy.transform.SetPositionAndRotation(worldPosition, rotation);
        enemy.SetActive(true);
        return enemy;
    }

    public void ReturnEnemy(GameObject enemy)
    {
        if (enemy == null) return;

        if (!_trackedEnemies.Contains(enemy))
        {
            _trackedEnemies.Add(enemy);
        }

        enemy.SetActive(false);
        enemy.transform.SetParent(poolParent != null ? poolParent : transform);

        if (!_availableEnemies.Contains(enemy))
        {
            _availableEnemies.Enqueue(enemy);
        }
    }
}
