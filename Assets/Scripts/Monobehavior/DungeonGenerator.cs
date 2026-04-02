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

    [Header("Zindan Ayarlari")]
    public int totalSteps = 1500; // Madencinin kaç adım atacağı
    public int mapWidth = 50;    // Sınırlandırma için genişlik
    public int mapHeight = 50;   // Sınırlandırma için yükseklik

    [Header("Spawn Ayarlari")]
    public GameObject player;       // Sahnede hazır duran oyuncu
    public GameObject enemyPrefab;  // Düşman prefab'ı
    public int enemyCount = 5;      // Kaç düşman doğsun?

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
        PlaceEntities();
    }
    void PlaceEntities()
    {
        // HashSet'i kolayca rastgele eleman seçebileceğimiz bir listeye çeviriyoruz
        List<Vector2Int> availableFloors = floorPositions.ToList();

        if (availableFloors.Count == 0) return;

        // --- 1. OYUNCUYU YERLEŞTİR ---
        // Madencinin başladığı ilk noktayı (0,0) oyuncu başlangıcı yapalım (en güvenli yer)
        player.transform.position = new Vector3(0.5f, 0.5f, 0); 

        // --- 2. DÜŞMANLARI YERLEŞTİR ---
        for (int i = 0; i < enemyCount; i++)
        {
            // Rastgele bir yer karosu seç
            int randomIndex = Random.Range(0, availableFloors.Count);
            Vector2Int spawnPos = availableFloors[randomIndex];

            // Oyuncunun dibinde doğmasınlar (Opsiyonel: 3 birim mesafe kontrolü)
            if (Vector2.Distance(Vector2.zero, spawnPos) < 3f)
            {
                i--; // Mesafe çok yakınsa bu turu pas geç ve tekrar dene
                continue;
            }

            // Dünyadaki gerçek koordinata çevir (Tile'ın ortasına gelmesi için 0.5 ekliyoruz)
            Vector3 worldPos = new Vector3(spawnPos.x + 0.5f, spawnPos.y + 0.5f, 0);
            
            Instantiate(enemyPrefab, worldPos, Quaternion.identity);
            
            // Aynı yere iki düşman gelmemesi için bu pozisyonu listeden çıkarabiliriz
            availableFloors.RemoveAt(randomIndex);
        }
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