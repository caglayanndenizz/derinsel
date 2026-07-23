using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pools floating damage-number popups. Self-bootstraps on first use (mirrors
/// PlayerImpactFeedback.EnsureHitStopManager) so no manual scene setup is required —
/// call <see cref="SpawnDamageNumber"/> from anywhere and a manager instance is created if missing.
/// </summary>
public class DamageNumberPooler : MonoBehaviour
{
    public static DamageNumberPooler Instance { get; private set; }

    [Header("Pool Settings")]
    [Tooltip("Optional custom prefab (needs a DamageNumber component; TextMeshPro is added automatically if missing). Auto-created at runtime if left empty.")]
    [SerializeField] private DamageNumber damageNumberPrefab;
    [SerializeField] private int initialPoolSize = 20;
    [SerializeField] private Transform poolParent;

    [Header("Render Sorting")]
    [Tooltip("Must draw above enemy sprites. Matches an existing Sorting Layer name in the project; falls back to Default if not found.")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 100;

    private readonly Queue<DamageNumber> _pool = new Queue<DamageNumber>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        for (int i = 0; i < initialPoolSize; i++)
            ReturnToPool(CreateNew());
    }

    /// <summary>Spawns a floating damage number at the given world position, creating the pooler if it doesn't exist yet.</summary>
    public static void SpawnDamageNumber(Vector3 worldPosition, float damage, bool isHeavy)
        => EnsureInstance()?.Spawn(worldPosition, damage, isHeavy);

    private static DamageNumberPooler EnsureInstance()
    {
        if (Instance == null)
        {
            GameObject managerGO = new GameObject("DamageNumberPooler");
            managerGO.AddComponent<DamageNumberPooler>(); // Awake() assigns Instance
        }
        return Instance;
    }

    private void Spawn(Vector3 worldPosition, float damage, bool isHeavy)
    {
        DamageNumber instance = _pool.Count > 0 ? _pool.Dequeue() : CreateNew();
        instance.transform.SetParent(poolParent != null ? poolParent : transform);
        instance.Play(worldPosition, damage, isHeavy, ReturnToPool);
    }

    private DamageNumber CreateNew()
    {
        DamageNumber instance;
        if (damageNumberPrefab != null)
        {
            instance = Instantiate(damageNumberPrefab, poolParent != null ? poolParent : transform);
        }
        else
        {
            GameObject go = new GameObject("DamageNumber");
            go.transform.SetParent(poolParent != null ? poolParent : transform);
            instance = go.AddComponent<DamageNumber>(); // RequireComponent adds TextMeshPro
        }

        MeshRenderer meshRenderer = instance.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrder;
        }

        instance.gameObject.SetActive(false);
        return instance;
    }

    private void ReturnToPool(DamageNumber instance)
    {
        instance.gameObject.SetActive(false);
        _pool.Enqueue(instance);
    }
}
