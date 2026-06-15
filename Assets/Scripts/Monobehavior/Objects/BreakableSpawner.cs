using UnityEngine;

public class BreakableSpawner : MonoBehaviour
{
    [Header("Breakable Prefabs")]
    [SerializeField] private GameObject urnPrefab;
    [SerializeField] private GameObject vasePrefab;
    [SerializeField] private GameObject jugPrefab;

    [Header("Explosive")]
    [SerializeField] private GameObject explosivePrefab;
    [Tooltip("Chance to spawn an explosive object per call (0 = never, 1 = always).")]
    [Range(0f, 1f)]
    [SerializeField] private float explosiveChance = 0.15f;

    private GameObject[] _prefabs;

    void Awake()
    {
        _prefabs = new[] { urnPrefab, vasePrefab, jugPrefab };
    }

    public GameObject SpawnRandom(Vector3 worldPosition)
    {
        if (explosivePrefab != null && Random.value < explosiveChance)
            return Instantiate(explosivePrefab, worldPosition, Quaternion.identity);

        GameObject prefab = _prefabs[Random.Range(0, _prefabs.Length)];
        if (prefab == null) return null;
        return Instantiate(prefab, worldPosition, Quaternion.identity);
    }

    public GameObject SpawnUrn(Vector3 worldPosition)
    {
        if (urnPrefab == null) return null;
        return Instantiate(urnPrefab, worldPosition, Quaternion.identity);
    }

    public GameObject SpawnVase(Vector3 worldPosition)
    {
        if (vasePrefab == null) return null;
        return Instantiate(vasePrefab, worldPosition, Quaternion.identity);
    }

    public GameObject SpawnJug(Vector3 worldPosition)
    {
        if (jugPrefab == null) return null;
        return Instantiate(jugPrefab, worldPosition, Quaternion.identity);
    }
}
