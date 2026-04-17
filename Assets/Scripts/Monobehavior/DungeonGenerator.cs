using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;
using Unity.Cinemachine;
using System.Collections;



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
    public int minSteps = 100; 
    public int maxSteps = 250;

    [Header("Spawn Ayarlari")]
    public GameObject player;       
    public GameObject enemyPrefab;  
    public GameObject exitPrefab; 
    public int enemyCount = 10;
    public EnemyObjectPooler enemyPooler;

    [Header("Transition Ayarlari")]
    public Animator fadeAnimator; // FadeImage üzerindeki Animator
    public CinemachineCamera vcam; // Sahnedeki sanal kamera
    public float transitionDuration;
    
    
    [Header("Yeni Mekanik: Kirilabilir Duvar")]
    public TileBase destructableWallTile; // Kırılabilir duvar görseli
    [Range(0, 1)] public float destructableChance = 0.15f; // Çıkma şansı 



    private HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();
    private GameObject currentExitInstance;
    private int _currentDungeonFloor = 1;

    private void Awake()
    {
        if (!ValidateConfiguration())
            enabled = false;
    }

    private bool ValidateConfiguration()
    {
        bool hasError = false;

        if (floorTilemap == null) { Debug.LogError("DungeonGenerator: floorTilemap atanmamis."); hasError = true; }
        if (wallTilemap == null) { Debug.LogError("DungeonGenerator: wallTilemap atanmamis."); hasError = true; }
        if (floorTile == null) { Debug.LogError("DungeonGenerator: floorTile atanmamis."); hasError = true; }
        if (wallTile == null) { Debug.LogError("DungeonGenerator: wallTile atanmamis."); hasError = true; }
        if (player == null) { Debug.LogError("DungeonGenerator: player referansi atanmamis."); hasError = true; }
        if (exitPrefab == null) { Debug.LogError("DungeonGenerator: exitPrefab atanmamis."); hasError = true; }
        if (fadeAnimator == null) { Debug.LogError("DungeonGenerator: fadeAnimator atanmamis."); hasError = true; }
        if (vcam == null) { Debug.LogError("DungeonGenerator: vcam atanmamis."); hasError = true; }

        if (enemyPooler == null)
            enemyPooler = EnemyObjectPooler.Instance;

        if (enemyPooler == null)
        {
            Debug.LogError("DungeonGenerator: enemyPooler atanmamis ve sahnede EnemyObjectPooler.Instance bulunamadi.");
            hasError = true;
        }

        return !hasError;
    }


    public void StartDungeonTransition()
    { 
        if (dungeonEntrance != null) dungeonEntrance.SetActive(false);
        _currentDungeonFloor = 1;
        StartCoroutine(DungeonTransitionRoutine());

    }

    private IEnumerator DungeonTransitionRoutine()
    {
        // 1. Ekranı Karart
        fadeAnimator.SetTrigger("StartFade");

        // 2. Animasyonun tamamlanmasını bekle (0.5 saniye demiştik)
        yield return new WaitForSeconds(transitionDuration);

        // 3. Zindanı kur ve oyuncuyu ışınla
        GenerateDungeon();

        // 4. KRİTİK NOKTA: Kamerayı şak diye oyuncuya sabitle (Kayma bitti!)
        vcam.ForceCameraPosition(player.transform.position, Quaternion.identity);

        // 5. Ekranı Geri Aç
        fadeAnimator.SetTrigger("EndFade");
    }
    public void StartNextFloorTransition()
    {
        // BUTONA BASILDIĞI AN: Çıkış panelini hemen gizle
        if (exitUI != null) exitUI.SetActive(false); 

        StartCoroutine(NextFloorTransitionRoutine());
    }

    private IEnumerator NextFloorTransitionRoutine()
    {
        // 1. Ekranı karartmaya başla
        fadeAnimator.SetTrigger("StartFade");

        // 2. Animasyonun kararması için belirlediğin süre kadar bekle
        // Eğer değişken tanımlamadıysan direkt 0.5f veya 1.0f yazabilirsin
        yield return new WaitForSeconds(transitionDuration); 

        // 3. Zindanı bir üst kat için yeniden oluştur (Eski fonksiyonun)
        MoveToNextFloor();

        // 4. Kamerayı yeni doğuş noktasına (0,0) şak diye ışınla
        // Oyuncu MoveToNextFloor içinde zaten ışınlandığı için kamera onu burada yakalar
        vcam.ForceCameraPosition(player.transform.position, Quaternion.identity);

        // 5. Ekranı geri aç
        fadeAnimator.SetTrigger("EndFade");
    }
        
    // Zindandan çıkarken de aynısını kullanabilirsin
    public void StartExitTransition()
    {
        if (exitUI != null) exitUI.SetActive(false);
        StartCoroutine(ExitTransitionRoutine());
    }

    private IEnumerator ExitTransitionRoutine()
    {
        fadeAnimator.SetTrigger("StartFade");
        yield return new WaitForSeconds(transitionDuration);

        ExitDungeon(); // Senin mevcut çıkış fonksiyonun

        vcam.ForceCameraPosition(player.transform.position, Quaternion.identity);

        fadeAnimator.SetTrigger("EndFade");
    }



    public void GenerateDungeon()
    {

        if (dungeonEntrance != null)
            dungeonEntrance.SetActive(false);

        if (currentExitInstance != null) Destroy(currentExitInstance);

        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
        floorPositions.Clear();

        if (_currentDungeonFloor < 1) _currentDungeonFloor = 1;

        float floorMultiplier = GetFloorSizeMultiplier(_currentDungeonFloor);
        int scaledMinSteps = Mathf.FloorToInt(minSteps * floorMultiplier);
        int scaledMaxSteps = Mathf.FloorToInt(maxSteps * floorMultiplier);
        if (scaledMaxSteps < scaledMinSteps) scaledMaxSteps = scaledMinSteps;

        int randomTotalSteps = Random.Range(scaledMinSteps, scaledMaxSteps + 1);

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

    void RandomWalk(int targetFloorCount)
    {
        Vector2Int currentPos = new Vector2Int(0, 0);
        int maxAttempts = Mathf.Max(targetFloorCount * 30, 500);
        int attempts = 0;

        while (floorPositions.Count < targetFloorCount && attempts < maxAttempts)
        {
            attempts++;
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

        if (floorPositions.Count < targetFloorCount)
        {
            Debug.LogWarning($"DungeonGenerator: Hedef floor sayisina ulasilamadi. Hedef={targetFloorCount}, Uretilen={floorPositions.Count}, Deneme={attempts}/{maxAttempts}");
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
    
        for (float x = -radius; x <= radius; x += 0.5f)
        {
            for (float y = -radius; y <= radius; y += 0.5f)
            {
                Vector3 offsetPos = worldPosition + new Vector3(x, y, 0);
                Vector3Int cellPos = wallTilemap.WorldToCell(offsetPos);

                
                if (wallTilemap.GetTile(cellPos) != null)
                {
                    
                    Color tileColor = wallTilemap.GetColor(cellPos);

                    
                    if (tileColor.r < 1.0f)
                    {
                        wallTilemap.SetTile(cellPos, null); 
                        floorTilemap.SetTile(cellPos, floorTile); 
                        
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
        if (enemyPooler == null)
            enemyPooler = EnemyObjectPooler.Instance;

        if (enemyPooler == null)
        {
            Debug.LogError("DungeonGenerator: PlaceEntities cagrildi ama enemyPooler null.");
            return;
        }

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
        int attempts = 0;
        int maxAttempts = Mathf.Max(enemyCount * 20, availableFloors.Count * 3);
        while (enemiesPlaced < enemyCount && availableFloors.Count > 0 && attempts < maxAttempts)
        {
            attempts++;
            int randomIndex = Random.Range(0, availableFloors.Count);
            Vector2Int spawnPos = availableFloors[randomIndex];

            if (Vector2.Distance(Vector2.zero, spawnPos) < 6f || Vector2.Distance(exitPos, spawnPos) < 3f)
            {
                availableFloors.RemoveAt(randomIndex);
                continue;
            }

            GameObject enemy = enemyPooler.GetEnemy(new Vector3(spawnPos.x + 0.5f, spawnPos.y + 0.5f, 0), Quaternion.identity);
            if (enemy == null)
            {
                Debug.LogWarning("DungeonGenerator: Enemy pool bos ve genisleme kapali oldugu icin spawn atlandi.");
                break;
            }

            availableFloors.RemoveAt(randomIndex);
            enemiesPlaced++;
        }

        if (enemiesPlaced < enemyCount)
        {
            Debug.LogWarning($"DungeonGenerator: Istek {enemyCount}, olusturulan {enemiesPlaced}. Uygun tile veya havuz kapasitesi yetersiz olabilir.");
        }
    }

    Vector2Int GetRandomDirection()
    {
        int rand = Random.Range(0, 4);
        switch (rand) { case 0: return Vector2Int.up; case 1: return Vector2Int.down; case 2: return Vector2Int.left; case 3: return Vector2Int.right; default: return Vector2Int.zero; }
    }

    private float GetFloorSizeMultiplier(int floorNumber)
    {
        // Her 5 katta bir +0.25x artar: 1-5 => 1.00x, 6-10 => 1.25x, 11-15 => 1.50x ...
        int tier = Mathf.FloorToInt((Mathf.Max(1, floorNumber) - 1) / 5f);
        return 1f + (tier * 0.2f);
    }

    public void MoveToNextFloor()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemyPooler == null)
            enemyPooler = EnemyObjectPooler.Instance;

        foreach (GameObject e in enemies)
        {
            if (enemyPooler != null) enemyPooler.ReturnEnemy(e);
            else Destroy(e);
        }

        _currentDungeonFloor++;
        GenerateDungeon();
        
        if (exitUI != null) exitUI.SetActive(false);
    }

    public void ExitDungeon()
    {
        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
        floorPositions.Clear();

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemyPooler == null)
            enemyPooler = EnemyObjectPooler.Instance;

        foreach (GameObject e in enemies)
        {
            if (enemyPooler != null) enemyPooler.ReturnEnemy(e);
            else Destroy(e);
        }
        
        if (currentExitInstance != null) Destroy(currentExitInstance);

        if (exitUI != null) exitUI.SetActive(false);

        // --- DEĞİŞİKLİK: Zindandan çıkıldığında giriş kapısını tekrar aç ---
        if (dungeonEntrance != null)
        {
            dungeonEntrance.SetActive(true);
            // Oyuncuyu ana dünyadaki kapının önüne ışınla
            player.transform.position = dungeonEntrance.transform.position + new Vector3(0, -1.5f, 0); 
        }

        _currentDungeonFloor = 1;
    }
}