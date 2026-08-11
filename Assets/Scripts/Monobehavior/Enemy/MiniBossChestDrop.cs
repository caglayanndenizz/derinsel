using UnityEngine;

/// <summary>Attach only to miniboss enemy prefabs. Spawns a wooden reward chest at the enemy's death position.</summary>
[RequireComponent(typeof(Enemy))]
public class MiniBossChestDrop : MonoBehaviour
{
    [SerializeField] private GameObject woodenChestPrefab;

    private Enemy _enemy;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _enemy.Died += OnDied;
    }

    private void OnDestroy()
    {
        if (_enemy != null) _enemy.Died -= OnDied;
    }

    private void OnDied(Enemy enemy)
    {
        if (woodenChestPrefab == null)
        {
            Debug.LogWarning("MiniBossChestDrop: woodenChestPrefab atanmamis.", this);
            return;
        }
        Instantiate(woodenChestPrefab, enemy.ReferencePosition, Quaternion.identity);
    }
}
