using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;

public class DungeonGenerator : MonoBehaviour
{
    [Header("Tile Ayarlari")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;
    public TileBase floorTile;
    public TileBase wallTile;

    [Header("Ana Dunya Ayarlari")]
    public GameObject dungeonEntrance;

    [Header("UI Ayarlari")]
    public GameObject exitUI;

    [Header("Zindan Ayarlari")]
    public int minSteps = 1000; 
    public int maxSteps = 2000;

    [Header("Spawn Ayarlari")]
    public GameObject player;       
    public GameObject enemyPrefab;  
    public GameObject exitPrefab; 
    public int enemyCount = 10;

    [Header("Yeni Mekanik: Kirilabilir Duvar")]
    public TileBase destructableWallTile; // Kırılabilir duvar görseli
    [Range(0, 1)] public float destructableChance = 0.15f; // Çıkma şansı 

    private HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();
    private GameObject currentExitInstance; 

    public void GenerateDungeon()
    {
        // --- DEĞİŞİKLİK: Zindana girildiğinde giriş kapısını gizle ---
        if (dungeonEntrance != null)
            dungeonEntrance.SetActive(false);

        if (currentExitInstance != null) Destroy(currentExitInstance);
        
        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
        floorPositions.Clear();

        int randomTotalSteps = Random.Range(minSteps, maxSteps + 1);

        CreateStartingRoom();
        RandomWalk(randomTotalSteps);
        CleanUpWalls();

        VisualiseDungeon();
        PlaceEntities();
    }

    void CreateStartingRoom()
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                floorPositions.Add(new Vector2Int(x, y));
            }
        }
    }

    void RandomWalk(int steps)
    {
        Vector2Int currentPos = new Vector2Int(0, 0);
        for (int i = 0; i < steps; i++)
        {
            Vector2Int direction = GetRandomDirection();
            currentPos += direction;
            for (int xOffset = 0; xOffset < 2; xOffset++)
            {
                for (int yOffset = 0; yOffset < 2; yOffset++)
                {
                    floorPositions.Add(new Vector2Int(currentPos.x + xOffset, currentPos.y + yOffset));
                }
            }
        }
    }

    void CleanUpWalls()
    {
        HashSet<Vector2Int> tilesToConvert = new HashSet<Vector2Int>();
        foreach (var pos in floorPositions)
        {
            Vector2Int[] neighbors = { pos + Vector2Int.up, pos + Vector2Int.down, pos + Vector2Int.left, pos + Vector2Int.right };
            foreach (var n in neighbors)
            {
                if (!floorPositions.Contains(n) && CountFloorNeighbors(n) >= 2) tilesToConvert.Add(n);
            }
        }
        foreach (var pos in tilesToConvert) floorPositions.Add(pos);
    }

    int CountFloorNeighbors(Vector2Int pos)
    {
        int count = 0;
        if (floorPositions.Contains(pos + Vector2Int.up)) count++;
        if (floorPositions.Contains(pos + Vector2Int.down)) count++;
        if (floorPositions.Contains(pos + Vector2Int.left)) count++;
        if (floorPositions.Contains(pos + Vector2Int.right)) count++;
        return count;
    }
    public void BreakWallsInArea(Vector3 worldPosition, float radius)
    {
    // Çekicin AOE alanı içindeki tüm koordinatları tara
        for (float x = -radius; x <= radius; x += 0.5f)
        {
            for (float y = -radius; y <= radius; y += 0.5f)
            {
                Vector3 offsetPos = worldPosition + new Vector3(x, y, 0);
                Vector3Int cellPos = wallTilemap.WorldToCell(offsetPos);

                // Eğer vurduğumuz yerde Tile varsa (null değilse)
                if (wallTilemap.GetTile(cellPos) != null)
                {
                    // Rengini kontrol et
                    Color tileColor = wallTilemap.GetColor(cellPos);

                    // *** KRİTİK DEĞİŞİKLİK ***
                    // Eğer rengi normal gri (Varsayılan Color.white tint'siz doku rengi) değilse kırıyoruz.
                    // Not: UnityEngine.Color.white varsayılan tint'siz renk değeridir. Dokunun kendisi gri olsa bile
                    // tint rengi Color.white (1,1,1,1) olur. Biz Color.white'tan farklı bir şey arıyoruz.
                    // Buradaki '0.85f' değeri VisualiseDungeon'daki ile aynı olmalıdır.
                    if (tileColor.r < 1.0f) // Renk tint uygulanmışsa kırılabilir demektir.
                    {
                        wallTilemap.SetTile(cellPos, null); // Duvarı sil
                        floorTilemap.SetTile(cellPos, floorTile); // Yerine zemin koy
                        // İsteğe bağlı: Instantiate(particleEffect, offsetPos, Quaternion.identity);
                    }
                }
            }
        }
    }

    bool IsNearFloor(Vector2Int pos, int radius)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (floorPositions.Contains(new Vector2Int(pos.x + x, pos.y + y)))
                {
                    return true;
                }
            }
        }
        return false;
    }
    void VisualiseDungeon()
    {
        // 1. Zeminleri yerleştir
        foreach (var pos in floorPositions) 
        {
            floorTilemap.SetTile((Vector3Int)pos, floorTile);
        }

        // 2. Sınırları hesapla
        int minX = floorPositions.Min(p => p.x) - 10;
        int maxX = floorPositions.Max(p => p.x) + 10;
        int minY = floorPositions.Min(p => p.y) - 10;
        int maxY = floorPositions.Max(p => p.y) + 10;

        // 3. Duvarları yerleştir
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2Int currentTile = new Vector2Int(x, y);
                Vector3Int cellPos = new Vector3Int(x, y, 0);

                if (!floorPositions.Contains(currentTile))
                {
                    wallTilemap.SetTile(cellPos, wallTile);

                    // --- GÜNCELLEME: Mesafe kontrolünü 2 yaptık ---
                    // Eğer bu duvar 2 birimlik alanda bir zemine komşuysa ve şans tutarsa
                    if (IsNearFloor(currentTile, 2) && Random.value < destructableChance)
                    {
                        wallTilemap.SetTileFlags(cellPos, TileFlags.None);
                        // Rengi yine 2 ton açıyoruz (0.85f)
                        wallTilemap.SetColor(cellPos, new Color(0.85f, 0.85f, 0.85f, 1.0f)); 
                    }
                }
            }
        }
    }
    void PlaceEntities()
    {
        List<Vector2Int> availableFloors = floorPositions.ToList();
        player.transform.position = new Vector3(0.5f, 0.5f, 0); 

        Vector2Int exitPos = availableFloors.OrderByDescending(p => Vector2.Distance(Vector2.zero, p)).First();
        currentExitInstance = Instantiate(exitPrefab, new Vector3(exitPos.x + 0.5f, exitPos.y + 0.5f, 0), Quaternion.identity);

        DungeonExit exitScript = currentExitInstance.GetComponent<DungeonExit>();
        if(exitScript != null) 
        {
            exitScript.Setup(exitUI); 
        }

        int enemiesPlaced = 0;
        while (enemiesPlaced < enemyCount)
        {
            int randomIndex = Random.Range(0, availableFloors.Count);
            Vector2Int spawnPos = availableFloors[randomIndex];

            if (Vector2.Distance(Vector2.zero, spawnPos) < 6f || Vector2.Distance(exitPos, spawnPos) < 3f)
                continue;

            Instantiate(enemyPrefab, new Vector3(spawnPos.x + 0.5f, spawnPos.y + 0.5f, 0), Quaternion.identity);
            availableFloors.RemoveAt(randomIndex);
            enemiesPlaced++;
        }
    }

    Vector2Int GetRandomDirection()
    {
        int rand = Random.Range(0, 4);
        switch (rand) { case 0: return Vector2Int.up; case 1: return Vector2Int.down; case 2: return Vector2Int.left; case 3: return Vector2Int.right; default: return Vector2Int.zero; }
    }

    public void MoveToNextFloor()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject e in enemies) Destroy(e);

        GenerateDungeon();
        
        if (exitUI != null) exitUI.SetActive(false);
    }

    public void ExitDungeon()
    {
        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
        floorPositions.Clear();

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject e in enemies) Destroy(e);
        
        if (currentExitInstance != null) Destroy(currentExitInstance);

        if (exitUI != null) exitUI.SetActive(false);

        // --- DEĞİŞİKLİK: Zindandan çıkıldığında giriş kapısını tekrar aç ---
        if (dungeonEntrance != null)
        {
            dungeonEntrance.SetActive(true);
            // Oyuncuyu ana dünyadaki kapının önüne ışınla
            player.transform.position = dungeonEntrance.transform.position + new Vector3(0, -1.5f, 0); 
        }
    }
}