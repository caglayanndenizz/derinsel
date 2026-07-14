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

    [System.Serializable]
    public struct EnemyTypePrefabEntry
    {
        public Enemy.EnemyType enemyType;
        public GameObject prefab;
    }

    [Header("Wave System — Prefabs by Type")]
    [Tooltip("Prefab per enemy type for the wave system. Falls back to the random pool if empty.")]
    [SerializeField] private List<EnemyTypePrefabEntry> typedPrefabs = new List<EnemyTypePrefabEntry>();

    [Header("Linked Poolers")]
    [Tooltip("If empty, Gold / Experience / Projectile poolers are searched in the scene (including inactive). They are activated once at startup and never toggled off — loot dropped in the world lives under them.")]
    [SerializeField] private GoldLootPooler linkedGoldLootPooler;
    [SerializeField] private ExperienceLootPooler linkedExperienceLootPooler;
    [SerializeField] private EnemyProjectilePooler linkedProjectilePooler;

    private readonly Queue<GameObject> _availableEnemies = new Queue<GameObject>();
    private readonly HashSet<GameObject> _queuedEnemies = new HashSet<GameObject>();

    // Typed pool: separate queue per EnemyType — GetEnemyOfType no longer calls Instantiate
    private readonly Dictionary<Enemy.EnemyType, Queue<GameObject>>   _typedAvailable = new();
    private readonly Dictionary<Enemy.EnemyType, HashSet<GameObject>> _typedQueued    = new();

    // Which typed pool each instance was created for. Instances not in this map
    // belong to the generic pool — ReturnEnemy must not guess by enemyType,
    // otherwise generic-pool enemies migrate into typed pools and the generic
    // pool drains permanently.
    private readonly Dictionary<GameObject, Enemy.EnemyType> _typedPoolMembership = new();

    private List<GameObject> _validPrefabCache;

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
        SetLinkedPoolersActive(true);
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

    // Loot poolers must stay active for the whole scene: dropped gold/xp is
    // parented under them, so deactivating a pooler hides live loot in the world.
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
        // Warm the typed pools the wave system actually spawns from;
        // the generic random pool is only warmed when no typed prefab exists.
        List<Enemy.EnemyType> typesToWarm = GetDistinctTypedTypes();
        if (typesToWarm.Count > 0)
        {
            int perType = Mathf.Max(1, initialPoolSize / typesToWarm.Count);
            foreach (Enemy.EnemyType type in typesToWarm)
            {
                for (int i = 0; i < perType; i++)
                {
                    GameObject enemy = CreateNewTypedEnemy(type);
                    if (enemy == null) break;
                    ReturnEnemy(enemy);
                }
            }
            return;
        }

        if (!HasValidPrefabs())
        {
            Debug.LogWarning("EnemyObjectPooler: no valid prefab found in enemyPrefabs list.");
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
            Debug.LogWarning("EnemyObjectPooler: could not create enemy — no valid prefab found.");
            return null;
        }

        Transform parent = poolParent != null ? poolParent : transform;
        GameObject enemy = Instantiate(prefab, parent);
        enemy.SetActive(false);
        return enemy;
    }

    private GameObject CreateNewTypedEnemy(Enemy.EnemyType type)
    {
        GameObject prefab = GetTypedPrefab(type);
        if (prefab == null) return null;

        Transform parent = poolParent != null ? poolParent : transform;
        GameObject enemy = Instantiate(prefab, parent);
        enemy.SetActive(false);
        _typedPoolMembership[enemy] = type;
        return enemy;
    }

    private void EnsureTypedPool(Enemy.EnemyType type,
        out Queue<GameObject> available, out HashSet<GameObject> queued)
    {
        if (!_typedAvailable.TryGetValue(type, out available))
            _typedAvailable[type] = available = new Queue<GameObject>();
        if (!_typedQueued.TryGetValue(type, out queued))
            _typedQueued[type] = queued = new HashSet<GameObject>();
    }

    public GameObject GetEnemy(Vector3 worldPosition, Quaternion rotation)
    {
        if (_availableEnemies.Count == 0)
        {
            if (!canExpandPool)
            {
                Debug.LogWarning("EnemyObjectPooler: enemy pool is exhausted.");
                return null;
            }

            GameObject expandedEnemy = CreateNewEnemy();
            if (expandedEnemy == null) return null;
            _availableEnemies.Enqueue(expandedEnemy);
        }

        GameObject enemy = _availableEnemies.Dequeue();
        _queuedEnemies.Remove(enemy);
        enemy.transform.SetPositionAndRotation(worldPosition, rotation);
        enemy.SetActive(true);
        return enemy;
    }

    /// <summary>
    /// Type-specific spawn for the wave system. Uses a per-type pool —
    /// Instantiate is NOT called on every spawn. If the type has no prefab in
    /// typedPrefabs, logs an error and falls back to a random generic enemy.
    /// </summary>
    public GameObject GetEnemyOfType(Enemy.EnemyType type, Vector3 worldPosition, Quaternion rotation)
    {
        GameObject prefab = GetTypedPrefab(type);

        if (prefab == null)
        {
            Debug.LogError($"EnemyObjectPooler: no prefab assigned for {type} in typedPrefabs — spawning a RANDOM enemy from the generic pool instead. Assign the prefab in the inspector.");
            return GetEnemy(worldPosition, rotation);
        }

        EnsureTypedPool(type, out Queue<GameObject> available, out HashSet<GameObject> queued);

        // Expand if pool is empty
        if (available.Count == 0)
        {
            if (!canExpandPool)
            {
                Debug.LogWarning($"EnemyObjectPooler: {type} typed pool is exhausted, expansion is disabled.");
                return null;
            }
            GameObject newEnemy = CreateNewTypedEnemy(type);
            if (newEnemy == null) return null;
            available.Enqueue(newEnemy);
            queued.Add(newEnemy);
        }

        GameObject enemy = available.Dequeue();
        queued.Remove(enemy);
        enemy.transform.SetPositionAndRotation(worldPosition, rotation);
        enemy.SetActive(true);
        return enemy;
    }

    private GameObject GetTypedPrefab(Enemy.EnemyType type)
    {
        if (typedPrefabs == null) return null;
        foreach (EnemyTypePrefabEntry entry in typedPrefabs)
            if (entry.prefab != null && entry.enemyType == type)
                return entry.prefab;
        return null;
    }

    private List<Enemy.EnemyType> GetDistinctTypedTypes()
    {
        List<Enemy.EnemyType> types = new List<Enemy.EnemyType>();
        if (typedPrefabs == null) return types;
        foreach (EnemyTypePrefabEntry entry in typedPrefabs)
            if (entry.prefab != null && !types.Contains(entry.enemyType))
                types.Add(entry.enemyType);
        return types;
    }

    public void ReturnEnemy(GameObject enemy)
    {
        if (enemy == null) return;

        enemy.SetActive(false);
        enemy.transform.SetParent(poolParent != null ? poolParent : transform);

        // Instances created by a typed pool go back to that pool;
        // everything else belongs to the generic pool.
        if (_typedPoolMembership.TryGetValue(enemy, out Enemy.EnemyType type))
        {
            EnsureTypedPool(type, out Queue<GameObject> available, out HashSet<GameObject> queued);
            if (queued.Add(enemy))
                available.Enqueue(enemy);
            return;
        }

        if (_queuedEnemies.Add(enemy))
            _availableEnemies.Enqueue(enemy);
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
