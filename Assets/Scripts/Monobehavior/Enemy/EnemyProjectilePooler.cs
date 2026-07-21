using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prefab-keyed projectile pool: every distinct projectile prefab used by any
/// RangedAttackBehavior gets its own queue, mirroring EnemyObjectPooler's
/// pattern. Caller passes the prefab it wants on every GetProjectile call, so
/// one shared pooler can serve every enemy's projectile type correctly.
/// </summary>
public class EnemyProjectilePooler : MonoBehaviour
{
    public static EnemyProjectilePooler Instance { get; private set; }

    [Header("Pool Settings")]
    [Tooltip("Every distinct projectile prefab fired by any RangedAttackBehavior should be listed here so it gets pre-warmed. Unlisted prefabs still work, just without warmup.")]
    public List<GameObject> projectilePrefabs = new List<GameObject>();
    public int initialPoolSize = 100;
    public bool canExpandPool = true;
    public Transform poolParent;
    public EnemyObjectPooler enemyPoolerSource;

    // Key: source prefab. Every instance remembers its prefab so ReturnProjectile
    // puts it back into the right queue.
    private readonly Dictionary<GameObject, Queue<GameObject>> _available = new();
    private readonly Dictionary<GameObject, HashSet<GameObject>> _queued = new();
    private readonly Dictionary<GameObject, GameObject> _sourcePrefab = new();
    private readonly Dictionary<GameObject, EnemyProjectile> _componentCache = new();
    private bool _warmupCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SyncPoolSizeWithEnemyPooler();
    }

    private void SyncPoolSizeWithEnemyPooler()
    {
        if (enemyPoolerSource == null)
            enemyPoolerSource = EnemyObjectPooler.Instance;

        if (enemyPoolerSource != null)
            initialPoolSize = Mathf.Max(0, enemyPoolerSource.initialPoolSize);
    }

    private void OnEnable()
    {
        if (_warmupCompleted) return;
        WarmupPool();
        _warmupCompleted = true;
    }

    private void WarmupPool()
    {
        List<GameObject> prefabs = GetValidPrefabs();
        if (prefabs.Count == 0)
        {
            Debug.LogWarning("EnemyProjectilePooler: projectilePrefabs listesi bos.");
            return;
        }

        int perPrefab = Mathf.Max(1, initialPoolSize / prefabs.Count);
        foreach (GameObject prefab in prefabs)
        {
            for (int i = 0; i < perPrefab; i++)
            {
                GameObject projectile = CreateNewProjectile(prefab);
                ReturnProjectile(projectile);
            }
        }
    }

    private List<GameObject> GetValidPrefabs()
    {
        List<GameObject> prefabs = new List<GameObject>();
        if (projectilePrefabs == null) return prefabs;
        foreach (GameObject prefab in projectilePrefabs)
            if (prefab != null && !prefabs.Contains(prefab))
                prefabs.Add(prefab);
        return prefabs;
    }

    private Transform GetProjectileHierarchyParent()
    {
        return poolParent != null ? poolParent : null;
    }

    private GameObject CreateNewProjectile(GameObject prefab)
    {
        GameObject projectile = Instantiate(prefab);
        projectile.transform.SetParent(GetProjectileHierarchyParent(), false);
        projectile.SetActive(false);
        _componentCache[projectile] = projectile.GetComponent<EnemyProjectile>();
        _sourcePrefab[projectile] = prefab;
        return projectile;
    }

    private void EnsurePool(GameObject prefab,
        out Queue<GameObject> available, out HashSet<GameObject> queued)
    {
        if (!_available.TryGetValue(prefab, out available))
            _available[prefab] = available = new Queue<GameObject>();
        if (!_queued.TryGetValue(prefab, out queued))
            _queued[prefab] = queued = new HashSet<GameObject>();
    }

    /// <param name="prefab">Which projectile type to spawn — the caller's own projectilePrefab.</param>
    /// <param name="configureBeforeActivate">Aktif etmeden once (hasar/hiz) — boylece OnTriggerEnter2D Initialize'dan once 0 hasarla calismaz.</param>
    public GameObject GetProjectile(GameObject prefab, Vector3 worldPosition, Quaternion rotation, Action<EnemyProjectile> configureBeforeActivate = null)
    {
        if (prefab == null)
        {
            Debug.LogError("EnemyProjectilePooler: GetProjectile called with a null prefab.");
            return null;
        }

        EnsurePool(prefab, out Queue<GameObject> available, out HashSet<GameObject> queued);

        if (available.Count == 0)
        {
            if (!canExpandPool)
            {
                Debug.LogWarning($"EnemyProjectilePooler: pool for '{prefab.name}' is exhausted.");
                return null;
            }

            GameObject expanded = CreateNewProjectile(prefab);
            available.Enqueue(expanded);
            queued.Add(expanded);
        }

        GameObject projectile = available.Dequeue();
        queued.Remove(projectile);
        projectile.transform.SetParent(GetProjectileHierarchyParent(), false);
        projectile.transform.SetPositionAndRotation(worldPosition, rotation);

        if (configureBeforeActivate != null)
        {
            _componentCache.TryGetValue(projectile, out EnemyProjectile ep);
            configureBeforeActivate(ep);
        }

        projectile.SetActive(true);
        return projectile;
    }

    public void ReturnProjectile(GameObject projectile)
    {
        if (projectile == null) return;

        projectile.SetActive(false);
        projectile.transform.SetParent(GetProjectileHierarchyParent(), false);

        // Bu pooler'in yaratmadigi instance'lar (ör. pooler atanmamisken Instantiate fallback'i) sadece deaktive edilir.
        if (!_sourcePrefab.TryGetValue(projectile, out GameObject prefab)) return;

        EnsurePool(prefab, out Queue<GameObject> available, out HashSet<GameObject> queued);
        if (queued.Add(projectile))
            available.Enqueue(projectile);
    }
}
