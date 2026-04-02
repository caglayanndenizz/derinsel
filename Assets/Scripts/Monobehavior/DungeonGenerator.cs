using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DungeonGenerator : MonoBehaviour
{
    [Header("Tile Ayarlari")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;
    public TileBase floorTile;
    public TileBase wallTile;

    [Header("Zindan Ayarlari")]
    public int totalSteps = 1500; // Madencinin kaç adım atacağı
    public int mapWidth = 50;    // Sınırlandırma için genişlik
    public int mapHeight = 50;   // Sınırlandırma için yükseklik

    private HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();

    void Start()
    {
        GenerateDungeon();
    }

    void GenerateDungeon()
    {
        // 1. Temizlik yap
        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
        floorPositions.Clear();

        // 2. Rastgele Yürüyüşü Başlat
        RandomWalk();

        // 3. Tile'ları Yerleştir
        VisualiseDungeon();
    }

    void RandomWalk()
    {
        Vector2Int currentPos = new Vector2Int(0, 0);
        floorPositions.Add(currentPos);

        for (int i = 0; i < totalSteps; i++)
        {
            // Rastgele bir yön seç (Yukarı, Aşağı, Sol, Sağ)
            Vector2Int direction = GetRandomDirection();
            currentPos += direction;

            // Pozisyonu kaydet (HashSet olduğu için aynı yere tekrar basarsa eklemez)
            floorPositions.Add(currentPos);
        }
    }

    Vector2Int GetRandomDirection()
    {
        int rand = Random.Range(0, 4);
        switch (rand)
        {
            case 0: return Vector2Int.up;
            case 1: return Vector2Int.down;
            case 2: return Vector2Int.left;
            case 3: return Vector2Int.right;
            default: return Vector2Int.zero;
        }
    }

    void VisualiseDungeon()
    {
        // Önce yerleri çiz
        foreach (var pos in floorPositions)
        {
            floorTilemap.SetTile((Vector3Int)pos, floorTile);
        }

        // Yerlerin etrafını duvarla doldur (Basit bir çevreleme mantığı)
        for (int x = -mapWidth; x < mapWidth; x++)
        {
            for (int y = -mapHeight; y < mapHeight; y++)
            {
                Vector2Int checkPos = new Vector2Int(x, y);
                if (!floorPositions.Contains(checkPos))
                {
                    wallTilemap.SetTile((Vector3Int)checkPos, wallTile);
                }
            }
        }
    }
}