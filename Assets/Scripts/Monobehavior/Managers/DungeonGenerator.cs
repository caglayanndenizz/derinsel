using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DungeonGenerator : MonoBehaviour
{
    public static DungeonGenerator Instance { get; private set; }

    // Tilemap refs kept for Enemy.cs navigation checks (null-safe).
    public Tilemap wallTilemap;
    public Tilemap floorTilemap;
    public TileBase floorTile;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Transition entry points ───────────────────────────────────────────────

    public void StartDungeonTransition()
    {
        LevelManager.Instance?.LoadFirstLevel();
    }

    public void StartNextFloorTransition()
    {
        LevelManager.Instance?.OnMiniBossKilled();
    }

    // ── BreakWallsInArea (devre dışı — sonraya) ──────────────────────────────

    public List<Vector3> BreakWallsInArea(Vector3 worldPosition, float radius)
    {
        return new List<Vector3>();

        // TODO: yeniden etkinleştirilecek
        // var broken = new List<Vector3>();
        // if (wallTilemap == null || floorTilemap == null) return broken;
        // var processed = new HashSet<Vector3Int>();
        // for (float x = -radius; x <= radius; x += 0.5f)
        // {
        //     for (float y = -radius; y <= radius; y += 0.5f)
        //     {
        //         Vector3Int cell = wallTilemap.WorldToCell(worldPosition + new Vector3(x, y, 0f));
        //         if (!processed.Add(cell)) continue;
        //         if (wallTilemap.GetTile(cell) == null) continue;
        //         if (wallTilemap.GetColor(cell).r >= 1f) continue;
        //         wallTilemap.SetTile(cell, null);
        //         floorTilemap.SetTile(cell, floorTile);
        //         broken.Add(wallTilemap.GetCellCenterWorld(cell));
        //     }
        // }
        // return broken;
    }
}
