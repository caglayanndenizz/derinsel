using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerArrowPooler : MonoBehaviour
{
    public static PlayerArrowPooler Instance { get; private set; }

    [Header("Arrow Pool")]
    public GameObject arrowPrefab;
    public int arrowPoolSize = 80;

    [Header("Bolt Pool")]
    public GameObject boltPrefab;
    public int boltPoolSize = 40;

    [Header("Settings")]
    public bool canExpandPool = true;
    [Tooltip("Havuz bosaldiktan sonra kac adet gecici obje olusturulsun.")]
    public int overflowBatchSize = 20;
    [Tooltip("Gecici objeler kac saniye kullanilmazsa yok edilsin.")]
    public float overflowTTL = 10f;
    public Transform poolParent;

    // ── Arrow ──────────────────────────────────────────────────────────────
    readonly Queue<GameObject>              _arrowAvailable    = new();
    readonly HashSet<GameObject>            _arrowQueued       = new();
    readonly Dictionary<GameObject, PlayerArrow> _arrowCache   = new();
    readonly HashSet<GameObject>            _arrowOverflow     = new();
    readonly HashSet<GameObject>            _arrowPendingDestroy = new();

    // ── Bolt ───────────────────────────────────────────────────────────────
    readonly Queue<GameObject>              _boltAvailable     = new();
    readonly HashSet<GameObject>            _boltQueued        = new();
    readonly Dictionary<GameObject, PlayerBolt> _boltCache     = new();
    readonly HashSet<GameObject>            _boltOverflow      = new();
    readonly HashSet<GameObject>            _boltPendingDestroy = new();

    bool _warmupDone;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        if (_warmupDone) return;
        if (arrowPrefab != null) WarmupArrows();
        if (boltPrefab  != null) WarmupBolts();
        _warmupDone = true;
    }

    void WarmupArrows()
    {
        for (int i = 0; i < arrowPoolSize; i++) ReturnArrow(CreateArrow());
    }

    void WarmupBolts()
    {
        for (int i = 0; i < boltPoolSize; i++) ReturnBolt(CreateBolt());
    }

    // ── Factory ────────────────────────────────────────────────────────────

    GameObject CreateArrow()
    {
        var go = Instantiate(arrowPrefab);
        go.transform.SetParent(poolParent, false);
        go.SetActive(false);
        _arrowCache[go] = go.GetComponent<PlayerArrow>();
        return go;
    }

    GameObject CreateBolt()
    {
        var go = Instantiate(boltPrefab);
        go.transform.SetParent(poolParent, false);
        go.SetActive(false);
        _boltCache[go] = go.GetComponent<PlayerBolt>();
        return go;
    }

    // ── Arrow Public API ───────────────────────────────────────────────────

    public void GetArrow(Vector3 worldPosition, Quaternion rotation, Action<PlayerArrow> configure = null)
    {
        if (arrowPrefab == null) { Debug.LogWarning("PlayerArrowPooler: arrowPrefab atanmamış!"); return; }

        if (_arrowAvailable.Count == 0)
        {
            if (!canExpandPool) { Debug.LogWarning("PlayerArrowPooler: arrow havuzu dolu, genişletme kapalı."); return; }
            ExpandArrowPool();
        }

        var go = _arrowAvailable.Dequeue();
        _arrowQueued.Remove(go);
        go.transform.SetParent(poolParent, false);
        go.transform.SetPositionAndRotation(worldPosition, rotation);
        go.SetActive(true);

        _arrowCache.TryGetValue(go, out PlayerArrow arrow);
        configure?.Invoke(arrow);
    }

    public void ReturnArrow(GameObject go)
    {
        if (go == null) return;

        if (_arrowPendingDestroy.Remove(go))
        {
            _arrowOverflow.Remove(go);
            _arrowCache.Remove(go);
            go.SetActive(false);
            Destroy(go);
            return;
        }

        go.SetActive(false);
        go.transform.SetParent(poolParent, false);
        if (_arrowQueued.Add(go))
        {
            if (!_arrowCache.ContainsKey(go))
                _arrowCache[go] = go.GetComponent<PlayerArrow>();
            _arrowAvailable.Enqueue(go);
        }
    }

    // ── Bolt Public API ────────────────────────────────────────────────────

    public void GetBolt(Vector3 worldPosition, Quaternion rotation, Action<PlayerBolt> configure = null)
    {
        if (boltPrefab == null) { Debug.LogWarning("PlayerArrowPooler: boltPrefab atanmamış!"); return; }

        if (_boltAvailable.Count == 0)
        {
            if (!canExpandPool) { Debug.LogWarning("PlayerArrowPooler: bolt havuzu dolu, genişletme kapalı."); return; }
            ExpandBoltPool();
        }

        var go = _boltAvailable.Dequeue();
        _boltQueued.Remove(go);
        go.transform.SetParent(poolParent, false);
        go.transform.SetPositionAndRotation(worldPosition, rotation);
        go.SetActive(true);

        _boltCache.TryGetValue(go, out PlayerBolt bolt);
        configure?.Invoke(bolt);
    }

    public void ReturnBolt(GameObject go)
    {
        if (go == null) return;

        if (_boltPendingDestroy.Remove(go))
        {
            _boltOverflow.Remove(go);
            _boltCache.Remove(go);
            go.SetActive(false);
            Destroy(go);
            return;
        }

        go.SetActive(false);
        go.transform.SetParent(poolParent, false);
        if (_boltQueued.Add(go))
        {
            if (!_boltCache.ContainsKey(go))
                _boltCache[go] = go.GetComponent<PlayerBolt>();
            _boltAvailable.Enqueue(go);
        }
    }

    // ── Overflow Expansion ─────────────────────────────────────────────────

    void ExpandArrowPool()
    {
        var batch = new HashSet<GameObject>();
        for (int i = 0; i < overflowBatchSize; i++)
        {
            var go = CreateArrow();
            ReturnArrow(go);
            _arrowOverflow.Add(go);
            batch.Add(go);
        }
        StartCoroutine(CleanupOverflow(batch, _arrowQueued, _arrowAvailable, _arrowOverflow, _arrowPendingDestroy, _arrowCache));
    }

    void ExpandBoltPool()
    {
        var batch = new HashSet<GameObject>();
        for (int i = 0; i < overflowBatchSize; i++)
        {
            var go = CreateBolt();
            ReturnBolt(go);
            _boltOverflow.Add(go);
            batch.Add(go);
        }
        StartCoroutine(CleanupOverflow(batch, _boltQueued, _boltAvailable, _boltOverflow, _boltPendingDestroy, _boltCache));
    }

    IEnumerator CleanupOverflow(
        HashSet<GameObject> batch,
        HashSet<GameObject> queued,
        Queue<GameObject>   available,
        HashSet<GameObject> overflow,
        HashSet<GameObject> pendingDestroy,
        IDictionary         cache)
    {
        yield return new WaitForSeconds(overflowTTL);

        // Drain queue once: re-enqueue non-expired items, destroy expired ones that are still waiting
        int count = available.Count;
        for (int i = 0; i < count; i++)
        {
            var go = available.Dequeue();
            queued.Remove(go);

            if (batch.Contains(go))
            {
                // Still sitting in pool unused — destroy immediately
                overflow.Remove(go);
                cache.Remove(go);
                if (go != null) Destroy(go);
            }
            else
            {
                queued.Add(go);
                available.Enqueue(go);
            }
        }

        // Overflow objects currently in use — destroy them when returned
        foreach (var go in batch)
        {
            if (go == null) continue;
            if (!queued.Contains(go))   // not in pool = currently in use
                pendingDestroy.Add(go);
        }
    }
}
