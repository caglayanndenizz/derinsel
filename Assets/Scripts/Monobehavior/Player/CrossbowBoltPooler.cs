using System;
using System.Collections.Generic;
using UnityEngine;

public class CrossbowBoltPooler : MonoBehaviour
{
    public static CrossbowBoltPooler Instance { get; private set; }

    [Header("Pool Settings")]
    public GameObject boltPrefab;
    public int initialPoolSize = 40;
    public bool canExpandPool = true;
    public Transform poolParent;

    private readonly Queue<GameObject> _available = new();
    private readonly HashSet<GameObject> _queued = new();
    private readonly Dictionary<GameObject, PlayerBolt> _cache = new();
    private bool _warmupCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        if (_warmupCompleted || boltPrefab == null) return;
        WarmupPool();
        _warmupCompleted = true;
    }

    private void WarmupPool()
    {
        for (int i = 0; i < initialPoolSize; i++)
            ReturnBolt(CreateNew());
    }

    private GameObject CreateNew()
    {
        GameObject go = Instantiate(boltPrefab);
        go.transform.SetParent(poolParent, false);
        go.SetActive(false);
        _cache[go] = go.GetComponent<PlayerBolt>();
        return go;
    }

    public void GetBolt(Vector3 worldPosition, Quaternion rotation, Action<PlayerBolt> configureBeforeActivate = null)
    {
        if (boltPrefab == null) { Debug.LogWarning("CrossbowBoltPooler: Bolt Prefab atanmamis!"); return; }

        if (_available.Count == 0)
        {
            if (!canExpandPool) { Debug.LogWarning("CrossbowBoltPooler: havuz bitti."); return; }
            _available.Enqueue(CreateNew());
        }

        GameObject go = _available.Dequeue();
        _queued.Remove(go);
        go.transform.SetParent(poolParent, false);
        go.transform.SetPositionAndRotation(worldPosition, rotation);
        go.SetActive(true);

        _cache.TryGetValue(go, out PlayerBolt bolt);
        configureBeforeActivate?.Invoke(bolt);
    }

    public void ReturnBolt(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        go.transform.SetParent(poolParent, false);
        if (_queued.Add(go))
        {
            if (!_cache.ContainsKey(go))
                _cache[go] = go.GetComponent<PlayerBolt>();
            _available.Enqueue(go);
        }
    }
}
