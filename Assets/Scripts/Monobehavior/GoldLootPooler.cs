using System.Collections.Generic;
using UnityEngine;

public class GoldLootPooler : MonoBehaviour
{
    public static GoldLootPooler Instance { get; private set; }

    [Header("Pool Settings")]
    public GameObject goldPrefab;
    public int initialPoolSize = 50;
    public bool canExpandPool = true;
    public Transform poolParent;
    public EnemyObjectPooler enemyPoolerSource;

    private readonly Queue<GameObject> _availableGold = new Queue<GameObject>();
    private readonly HashSet<GameObject> _trackedGold = new HashSet<GameObject>();
    private readonly HashSet<GameObject> _queuedGold = new HashSet<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SyncPoolSizeWithEnemyPooler();
        WarmupPool();
    }

    private void OnValidate()
    {
        SyncPoolSizeWithEnemyPooler();
    }

    private void SyncPoolSizeWithEnemyPooler()
    {
        if (enemyPoolerSource == null)
            enemyPoolerSource = EnemyObjectPooler.Instance;

        if (enemyPoolerSource != null)
            initialPoolSize = Mathf.Max(0, enemyPoolerSource.initialPoolSize);
    }

    private void WarmupPool()
    {
        if (goldPrefab == null)
        {
            Debug.LogWarning("GoldLootPooler: goldPrefab atanmamis.");
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject gold = CreateNewGold();
            ReturnGold(gold);
        }
    }

    private GameObject CreateNewGold()
    {
        Transform parent = poolParent != null ? poolParent : transform;
        GameObject gold = Instantiate(goldPrefab, parent);
        gold.SetActive(false);
        _trackedGold.Add(gold);
        return gold;
    }

    public GameObject GetGold(Vector3 worldPosition, Quaternion rotation)
    {
        if (_availableGold.Count == 0)
        {
            if (!canExpandPool)
            {
                Debug.LogWarning("GoldLootPooler: Havuzdaki gold tukendi.");
                return null;
            }

            GameObject expandedGold = CreateNewGold();
            _availableGold.Enqueue(expandedGold);
        }

        GameObject gold = _availableGold.Dequeue();
        _queuedGold.Remove(gold);
        gold.transform.SetPositionAndRotation(worldPosition, rotation);
        Lootable lootable = gold.GetComponent<Lootable>();
        if (lootable != null) lootable.isGold = true;
        gold.SetActive(true);
        return gold;
    }

    public void ReturnGold(GameObject gold)
    {
        if (gold == null) return;

        if (!_trackedGold.Contains(gold))
            _trackedGold.Add(gold);

        gold.SetActive(false);
        gold.transform.SetParent(poolParent != null ? poolParent : transform);

        if (_queuedGold.Add(gold))
            _availableGold.Enqueue(gold);
    }
}
