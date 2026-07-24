using UnityEngine;

/// <summary>
/// Tab 2 of the resting-floor NPC panel: spend gold to spawn a real chest prefab next to the
/// player (same ChestInteractable the player already knows — walking into it opens the offer,
/// nothing new to learn). If the ideal spot is occupied by a previously bought chest, searches
/// outward in a ring pattern so multiple chests never overlap.
/// </summary>
public class ChestPurchaseController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [Tooltip("Spawned chests are parented here so they get destroyed along with this level instance instead of lingering as orphaned scene-root objects. Must be a plain world-space Transform with scale (1,1,1) inside this level's hierarchy — do NOT use a UI Canvas transform, its scale would resize the chest.")]
    [SerializeField] private Transform spawnedChestParent;

    [Header("Chest Prefabs")]
    [SerializeField] private GameObject woodenChestPrefab;
    [SerializeField] private GameObject silverChestPrefab;
    [SerializeField] private GameObject goldChestPrefab;

    [Header("Prices (gold)")]
    [SerializeField] private float woodenPrice = 25f;
    [SerializeField] private float silverPrice = 60f;
    [SerializeField] private float goldPrice   = 120f;

    [Header("Spawn Placement")]
    [Tooltip("Direction (from the player) the chest tries to spawn in first.")]
    [SerializeField] private Vector2 spawnDirection = Vector2.right;
    [Tooltip("Distance from the player the chest tries to spawn at first (units).")]
    [SerializeField] private float spawnDistance = 0.5f;
    [Tooltip("Radius used to check whether a candidate spawn point is already occupied by another chest.")]
    [SerializeField] private float chestCheckRadius = 0.5f;
    [Tooltip("Layers that count as 'occupied' when searching for a free spawn spot (put spawned chests' layer here).")]
    [SerializeField] private LayerMask obstructionMask;
    [SerializeField] private int maxSearchRings = 4;

    private void Awake()
    {
        if (player == null)
            player = Object.FindAnyObjectByType<Player>();
    }

    /// <summary>UnityEvent-friendly wrapper for UI Button OnClick — Unity's OnClick() dropdown only lists void methods. Wire buttons to this, not TryBuyChest.</summary>
    public void BuyChestButtonClicked(int rarity)
    {
        TryBuyChest(rarity);
    }

    /// <summary>1 = Wooden, 2 = Silver, 3 = Gold. Returns false if unaffordable or nothing could be spawned.</summary>
    public bool TryBuyChest(int rarity)
    {
        if (player == null || player.PlayerCurrency == null) return false;

        GameObject prefab = GetPrefab(rarity);
        if (prefab == null) return false;

        float price = GetPrice(rarity);
        if (!player.PlayerCurrency.TrySpendGold(price)) return false;

        Vector3 idealSpot = player.transform.position + (Vector3)spawnDirection.normalized * spawnDistance;
        Vector3 spawnPos  = FindFreeSpawnPosition(idealSpot);
        Instantiate(prefab, spawnPos, Quaternion.identity, spawnedChestParent);
        return true;
    }

    private Vector3 FindFreeSpawnPosition(Vector3 origin)
    {
        if (Physics2D.OverlapCircle(origin, chestCheckRadius, obstructionMask) == null)
            return origin;

        for (int ring = 1; ring <= maxSearchRings; ring++)
        {
            float ringDistance = chestCheckRadius * 2f * ring;
            const int pointsOnRing = 8;
            for (int i = 0; i < pointsOnRing; i++)
            {
                float angle = (360f / pointsOnRing) * i * Mathf.Deg2Rad;
                Vector3 candidate = origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * ringDistance;
                if (Physics2D.OverlapCircle(candidate, chestCheckRadius, obstructionMask) == null)
                    return candidate;
            }
        }

        return origin; // fallback — every ring searched was occupied, spawn anyway rather than silently fail
    }

    private GameObject GetPrefab(int rarity) => rarity switch
    {
        3 => goldChestPrefab,
        2 => silverChestPrefab,
        _ => woodenChestPrefab,
    };

    private float GetPrice(int rarity) => rarity switch
    {
        3 => Mathf.Max(0f, goldPrice),
        2 => Mathf.Max(0f, silverPrice),
        _ => Mathf.Max(0f, woodenPrice),
    };
}
