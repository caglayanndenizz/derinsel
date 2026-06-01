using System.Collections.Generic;
using UnityEngine;

public class ExperienceLootPooler : MonoBehaviour
{
    public static ExperienceLootPooler Instance { get; private set; }

    [Header("Pool Settings")]
    public GameObject experiencePrefab;
    public int initialPoolSize = 50;
    public bool canExpandPool = true;
    public Transform poolParent;
    public EnemyObjectPooler enemyPoolerSource;

    private readonly Queue<GameObject> _availableExperience = new Queue<GameObject>();
    private readonly HashSet<GameObject> _queuedExperience = new HashSet<GameObject>();
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

    private void OnEnable()
    {
        if (_warmupCompleted || experiencePrefab == null)
            return;
        WarmupPool();
        _warmupCompleted = true;
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
        if (experiencePrefab == null)
        {
            Debug.LogWarning("ExperienceLootPooler: experiencePrefab atanmamis.");
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject exp = CreateNewExperience();
            ReturnExperience(exp);
        }
    }

    private GameObject CreateNewExperience()
    {
        Transform parent = poolParent != null ? poolParent : transform;
        GameObject exp = Instantiate(experiencePrefab, parent);
        Lootable lootable = exp.GetComponent<Lootable>();
        if (lootable != null) lootable.isGold = false;
        exp.SetActive(false);
        return exp;
    }

    public GameObject GetExperience(Vector3 worldPosition, Quaternion rotation)
    {
        if (_availableExperience.Count == 0)
        {
            if (!canExpandPool)
            {
                Debug.LogWarning("ExperienceLootPooler: Havuzdaki experience tukendi.");
                return null;
            }

            GameObject expandedExp = CreateNewExperience();
            _availableExperience.Enqueue(expandedExp);
        }

        GameObject exp = _availableExperience.Dequeue();
        _queuedExperience.Remove(exp);
        exp.transform.SetPositionAndRotation(worldPosition, rotation);
        exp.SetActive(true);
        return exp;
    }

    public void ReturnExperience(GameObject exp)
    {
        if (exp == null) return;

        exp.SetActive(false);
        exp.transform.SetParent(poolParent != null ? poolParent : transform);

        if (_queuedExperience.Add(exp))
            _availableExperience.Enqueue(exp);
    }
}
