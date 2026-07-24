using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Prefab-keyed enemy pool: every distinct prefab gets its own queue, so any
/// new enemy variant is just a new prefab in the list — no enum registration.
/// </summary>
public class EnemyObjectPooler : MonoBehaviour
{
    public static EnemyObjectPooler Instance { get; private set; }

    [Header("Pool Settings")]
    [Tooltip("Prefabs warmed at startup; initialPoolSize is split between them.")]
    [FormerlySerializedAs("enemyPrefab")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();
    public int initialPoolSize = 50;
    public bool canExpandPool = true;
    public Transform poolParent;

    [Header("Linked Poolers")]
    [Tooltip("If empty, Gold / Projectile poolers are searched in the scene (including inactive). They are activated once at startup and never toggled off — loot dropped in the world lives under them.")]
    [SerializeField] private GoldLootPooler linkedGoldLootPooler;
    [SerializeField] private EnemyProjectilePooler linkedProjectilePooler;

    // Key: source prefab. Every instance remembers its prefab so ReturnEnemy
    // puts it back into the right queue.
    private readonly Dictionary<GameObject, Queue<GameObject>>   _available = new();
    private readonly Dictionary<GameObject, HashSet<GameObject>> _queued    = new();
    private readonly Dictionary<GameObject, GameObject> _sourcePrefab = new();

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

    private void Start()
    {
        ResolveLinkedPoolersIfNeeded();
        SetLinkedPoolersActive(true);
    }

    private void ResolveLinkedPoolersIfNeeded()
    {
        if (linkedGoldLootPooler == null)
            linkedGoldLootPooler = FindFirstObjectByType<GoldLootPooler>(FindObjectsInactive.Include);
        if (linkedProjectilePooler == null)
            linkedProjectilePooler = FindFirstObjectByType<EnemyProjectilePooler>(FindObjectsInactive.Include);
    }

    // Loot poolers must stay active for the whole scene: dropped gold is
    // parented under them, so deactivating a pooler hides live loot in the world.
    private void SetLinkedPoolersActive(bool active)
    {
        if (linkedGoldLootPooler != null)
            linkedGoldLootPooler.gameObject.SetActive(active);
        if (linkedProjectilePooler != null)
            linkedProjectilePooler.gameObject.SetActive(active);
    }

    private void WarmupPool()
    {
        List<GameObject> prefabs = GetValidPrefabs();
        if (prefabs.Count == 0)
        {
            Debug.LogWarning("EnemyObjectPooler: no valid prefab found in enemyPrefabs list.");
            return;
        }

        int perPrefab = Mathf.Max(1, initialPoolSize / prefabs.Count);
        foreach (GameObject prefab in prefabs)
        {
            for (int i = 0; i < perPrefab; i++)
            {
                GameObject enemy = CreateNewEnemy(prefab);
                if (enemy == null) break;
                ReturnEnemy(enemy);
            }
        }
    }

    private GameObject CreateNewEnemy(GameObject prefab)
    {
        if (prefab == null) return null;

        Transform parent = poolParent != null ? poolParent : transform;
        GameObject enemy = Instantiate(prefab, parent);
        enemy.SetActive(false);
        _sourcePrefab[enemy] = prefab;
        return enemy;
    }

    private void EnsurePool(GameObject prefab,
        out Queue<GameObject> available, out HashSet<GameObject> queued)
    {
        if (!_available.TryGetValue(prefab, out available))
            _available[prefab] = available = new Queue<GameObject>();
        if (!_queued.TryGetValue(prefab, out queued))
            _queued[prefab] = queued = new HashSet<GameObject>();
    }

    /// <summary>
    /// Spawns a pooled instance of the given enemy prefab —
    /// Instantiate is NOT called on every spawn.
    /// </summary>
    public GameObject GetEnemy(GameObject prefab, Vector3 worldPosition, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("EnemyObjectPooler: GetEnemy called with a null prefab.");
            return null;
        }

        EnsurePool(prefab, out Queue<GameObject> available, out HashSet<GameObject> queued);

        if (available.Count == 0)
        {
            if (!canExpandPool)
            {
                Debug.LogWarning($"EnemyObjectPooler: pool for '{prefab.name}' is exhausted, expansion is disabled.");
                return null;
            }
            GameObject newEnemy = CreateNewEnemy(prefab);
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

    public void ReturnEnemy(GameObject enemy)
    {
        if (enemy == null) return;

        enemy.SetActive(false);
        enemy.transform.SetParent(poolParent != null ? poolParent : transform);

        // Instances the pool didn't create (scene-placed enemies) are only
        // deactivated — they have no source prefab to be reused as.
        if (!_sourcePrefab.TryGetValue(enemy, out GameObject prefab)) return;

        EnsurePool(prefab, out Queue<GameObject> available, out HashSet<GameObject> queued);
        if (queued.Add(enemy))
            available.Enqueue(enemy);
    }

    private List<GameObject> GetValidPrefabs()
    {
        List<GameObject> prefabs = new List<GameObject>();
        if (enemyPrefabs == null) return prefabs;
        foreach (GameObject prefab in enemyPrefabs)
            if (prefab != null && !prefabs.Contains(prefab))
                prefabs.Add(prefab);
        return prefabs;
    }
}
