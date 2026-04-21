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
    private readonly HashSet<GameObject> _trackedProjectiles = new HashSet<GameObject>();
    private readonly HashSet<GameObject> _queuedProjectiles = new HashSet<GameObject>();
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

    private GameObject CreateNewProjectile()
    {
        Transform parent = poolParent != null ? poolParent : transform;
        GameObject projectile = Instantiate(projectilePrefab, parent);
        projectile.SetActive(false);
        _trackedProjectiles.Add(projectile);
        return projectile;
    }

    public GameObject GetProjectile(Vector3 worldPosition, Quaternion rotation)
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
        projectile.transform.SetPositionAndRotation(worldPosition, rotation);
        projectile.SetActive(true);
        return projectile;
    }

    public void ReturnProjectile(GameObject projectile)
    {
        if (projectile == null) return;

        if (!_trackedProjectiles.Contains(projectile))
            _trackedProjectiles.Add(projectile);

        projectile.SetActive(false);
        projectile.transform.SetParent(poolParent != null ? poolParent : transform);

        if (_queuedProjectiles.Add(projectile))
            _availableProjectiles.Enqueue(projectile);
    }
}
