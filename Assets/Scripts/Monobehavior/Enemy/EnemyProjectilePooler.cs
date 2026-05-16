using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyProjectilePooler : MonoBehaviour
{
    public static EnemyProjectilePooler Instance { get; private set; }

    [Header("Pool Settings")]
    public GameObject projectilePrefab;
    public int initialPoolSize = 100;
    public bool canExpandPool = true;
    public Transform poolParent;
    public EnemyObjectPooler enemyPoolerSource;

    private readonly Queue<GameObject> _availableProjectiles = new Queue<GameObject>();
    private readonly HashSet<GameObject> _queuedProjectiles = new HashSet<GameObject>();
    private readonly Dictionary<GameObject, EnemyProjectile> _componentCache = new Dictionary<GameObject, EnemyProjectile>();
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
        if (_warmupCompleted || projectilePrefab == null)
            return;
        WarmupPool();
        _warmupCompleted = true;
    }

    private void WarmupPool()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("EnemyProjectilePooler: projectilePrefab atanmamis.");
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject projectile = CreateNewProjectile();
            ReturnProjectile(projectile);
        }
    }

    private Transform GetProjectileHierarchyParent()
    {
        return poolParent != null ? poolParent : null;
    }

    private GameObject CreateNewProjectile()
    {
        GameObject projectile = Instantiate(projectilePrefab);
        projectile.transform.SetParent(GetProjectileHierarchyParent(), false);
        projectile.SetActive(false);
        _componentCache[projectile] = projectile.GetComponent<EnemyProjectile>();
        return projectile;
    }

    /// <param name="configureBeforeActivate">Aktif etmeden once (hasar/hiz) — boylece OnTriggerEnter2D Initialize'dan once 0 hasarla calismaz.</param>
    public GameObject GetProjectile(Vector3 worldPosition, Quaternion rotation, Action<EnemyProjectile> configureBeforeActivate = null)
    {
        if (_availableProjectiles.Count == 0)
        {
            if (!canExpandPool)
            {
                Debug.LogWarning("EnemyProjectilePooler: Havuzdaki projectile tükendi.");
                return null;
            }

            GameObject expandedProjectile = CreateNewProjectile();
            _availableProjectiles.Enqueue(expandedProjectile);
        }

        GameObject projectile = _availableProjectiles.Dequeue();
        _queuedProjectiles.Remove(projectile);
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

        if (_queuedProjectiles.Add(projectile))
        {
            if (!_componentCache.ContainsKey(projectile))
                _componentCache[projectile] = projectile.GetComponent<EnemyProjectile>();
            _availableProjectiles.Enqueue(projectile);
        }
    }
}
