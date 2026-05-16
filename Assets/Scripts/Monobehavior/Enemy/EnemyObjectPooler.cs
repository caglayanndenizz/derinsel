using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class EnemyObjectPooler : MonoBehaviour
{
    public static EnemyObjectPooler Instance { get; private set; }

    [Header("Pool Settings")]
    [FormerlySerializedAs("enemyPrefab")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();
    public int initialPoolSize = 50;
    public bool canExpandPool = true;
    public Transform poolParent;

    [Header("Baglantili pooler'lar")]
    [Tooltip("Bos birakilirsa Gold / Experience / Projectile pooler sahne icinde (kapali dahil) aranir.")]
    [SerializeField] private GoldLootPooler linkedGoldLootPooler;
    [SerializeField] private ExperienceLootPooler linkedExperienceLootPooler;
    [SerializeField] private EnemyProjectilePooler linkedProjectilePooler;

    private readonly Queue<GameObject> _availableEnemies = new Queue<GameObject>();
    private readonly HashSet<GameObject> _queuedEnemies = new HashSet<GameObject>();
    private List<GameObject> _validPrefabCache;
    private int _leasedActiveEnemyCount;

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

    private void OnValidate()
    {
        if (enemyPrefabs == null)
            enemyPrefabs = new List<GameObject>();
        _validPrefabCache = null;
    }

    private void Start()
    {
        ResolveLinkedPoolersIfNeeded();
        SetLinkedPoolersActive(_leasedActiveEnemyCount > 0);
    }

    private void ResolveLinkedPoolersIfNeeded()
    {
        if (linkedGoldLootPooler == null)
            linkedGoldLootPooler = FindFirstObjectByType<GoldLootPooler>(FindObjectsInactive.Include);
        if (linkedExperienceLootPooler == null)
            linkedExperienceLootPooler = FindFirstObjectByType<ExperienceLootPooler>(FindObjectsInactive.Include);
        if (linkedProjectilePooler == null)
            linkedProjectilePooler = FindFirstObjectByType<EnemyProjectilePooler>(FindObjectsInactive.Include);
    }

    private void SetLinkedPoolersActive(bool active)
    {
        if (linkedGoldLootPooler != null)
            linkedGoldLootPooler.gameObject.SetActive(active);
        if (linkedExperienceLootPooler != null)
            linkedExperienceLootPooler.gameObject.SetActive(active);
        if (linkedProjectilePooler != null)
            linkedProjectilePooler.gameObject.SetActive(active);
    }

    private void WarmupPool()
    {
        if (!HasValidPrefabs())
        {
            Debug.LogWarning("EnemyObjectPooler: enemyPrefabs listesinde gecerli prefab yok.");
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
        GameObject prefab = GetRandomEnemyPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("EnemyObjectPooler: Enemy olusturulamadi, gecerli prefab bulunamadi.");
            return null;
        }

        Transform parent = poolParent != null ? poolParent : transform;
        GameObject enemy = Instantiate(prefab, parent);
        enemy.SetActive(false);
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
            if (expandedEnemy == null) return null;
            _availableEnemies.Enqueue(expandedEnemy);
        }

        GameObject enemy = _availableEnemies.Dequeue();
        _queuedEnemies.Remove(enemy);
        enemy.transform.SetPositionAndRotation(worldPosition, rotation);

        if (_leasedActiveEnemyCount == 0)
            SetLinkedPoolersActive(true);
        _leasedActiveEnemyCount++;

        enemy.SetActive(true);
        return enemy;
    }

    public void ReturnEnemy(GameObject enemy)
    {
        if (enemy == null) return;

        bool wasActiveInWorld = enemy.activeSelf;

        enemy.SetActive(false);
        enemy.transform.SetParent(poolParent != null ? poolParent : transform);

        if (_queuedEnemies.Add(enemy))
        {
            _availableEnemies.Enqueue(enemy);
            if (wasActiveInWorld)
                _leasedActiveEnemyCount = Mathf.Max(0, _leasedActiveEnemyCount - 1);
        }

        if (_leasedActiveEnemyCount == 0)
            SetLinkedPoolersActive(false);
    }

    private bool HasValidPrefabs()
    {
        RebuildCacheIfNeeded();
        return _validPrefabCache.Count > 0;
    }

    private GameObject GetRandomEnemyPrefab()
    {
        RebuildCacheIfNeeded();
        if (_validPrefabCache.Count == 0) return null;
        return _validPrefabCache[Random.Range(0, _validPrefabCache.Count)];
    }

    private void RebuildCacheIfNeeded()
    {
        if (_validPrefabCache != null) return;
        _validPrefabCache = new List<GameObject>();
        if (enemyPrefabs == null) return;
        for (int i = 0; i < enemyPrefabs.Count; i++)
            if (enemyPrefabs[i] != null)
                _validPrefabCache.Add(enemyPrefabs[i]);
    }
}
