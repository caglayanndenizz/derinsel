using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;
using Unity.Cinemachine;
using System.Collections;
using UnityEngine.Serialization;

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
    [FormerlySerializedAs("enemyPrefab")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();
    public GameObject exitPrefab; 
    private int enemyCount = 10;
    public EnemyObjectPooler enemyPooler;

    [Header("Transition Ayarlari")]
    public TransitionFader transitionFader;
    public CinemachineCamera vcam; // Sahnedeki sanal kamera

    [Header("Yeni Mekanik: Kirilabilir Duvar")]
    public TileBase destructableWallTile; // Kırılabilir duvar görseli
    [Range(0, 1)] public float destructableChance = 0.15f; // Çıkma şansı

    private HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();
    private readonly List<GameObject> currentExitInstances = new List<GameObject>();
    private bool _exitDoorsSpawnedForCurrentFloor;
    private float _nextRoomClearCheckTime;
    private const float RoomClearCheckInterval = 0.35f;
    private const float TargetDoorSpawnDistanceFromPlayer = 2f;
    private static readonly Color ExitDoorTint = Color.black;
    private int _currentDungeonFloor = 1;
    public int CurrentFloor => _currentDungeonFloor;

    private void Awake()
    {
        if (!ValidateConfiguration())
            enabled = false;
    }

    private void OnValidate()
    {
        if (enemyPrefabs == null)
            enemyPrefabs = new List<GameObject>();

        if (enemyPooler != null)
            SyncEnemyCountWithPoolSize();
    }

    private bool ValidateConfiguration()
    {
        bool hasError = false;

        if (floorTilemap == null) { Debug.LogError("DungeonGenerator: floorTilemap atanmamis."); hasError = true; }
        if (wallTilemap == null) { Debug.LogError("DungeonGenerator: wallTilemap atanmamis."); hasError = true; }
        if (floorTile == null) { Debug.LogError("DungeonGenerator: floorTile atanmamis."); hasError = true; }
        if (wallTile == null) { Debug.LogError("DungeonGenerator: wallTile atanmamis."); hasError = true; }
        if (player == null) { Debug.LogError("DungeonGenerator: player referansi atanmamis."); hasError = true; }
        if (enemyPrefabs == null || enemyPrefabs.Count == 0) { Debug.LogError("DungeonGenerator: enemyPrefabs listesi bos."); hasError = true; }
        if (exitPrefab == null) { Debug.LogError("DungeonGenerator: exitPrefab atanmamis."); hasError = true; }
        if (transitionFader == null) { Debug.LogError("DungeonGenerator: transitionFader atanmamis."); hasError = true; }
        if (vcam == null) { Debug.LogError("DungeonGenerator: vcam atanmamis."); hasError = true; }

        if (enemyPooler == null)
            enemyPooler = EnemyObjectPooler.Instance;

        if (enemyPooler != null)
        {
            SyncEnemyCountWithPoolSize();
        }
        else
        {
            Debug.LogWarning("DungeonGenerator: enemyPooler bulunamadi. Enemy spawn'lari enemyPrefabs listesinden Instantiate ile yapilacak.");
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
        yield return transitionFader.FadeOutIn(() =>
        {
            GenerateDungeon();
            vcam.ForceCameraPosition(player.transform.position, Quaternion.identity);
        });
    }
    public void StartNextFloorTransition()
    {
        if (exitUI != null) exitUI.SetActive(false);
        StartCoroutine(NextFloorTransitionRoutine());
    }

    private IEnumerator NextFloorTransitionRoutine()
    {
        yield return transitionFader.FadeOutIn(() =>
        {
            MoveToNextFloor();
            vcam.ForceCameraPosition(player.transform.position, Quaternion.identity);
        });
    }

    public void StartExitTransition()
    {
        if (exitUI != null) exitUI.SetActive(false);
        StartCoroutine(ExitTransitionRoutine());
    }

    private IEnumerator ExitTransitionRoutine()
    {
        yield return transitionFader.FadeOutIn(() =>
        {
            ExitDungeon();
            vcam.ForceCameraPosition(player.transform.position, Quaternion.identity);
        });
    }

    public void GenerateDungeon()
    {
        if (dungeonEntrance != null)
            dungeonEntrance.SetActive(false);

        ClearCurrentExits();

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
    public List<Vector3> BreakWallsInArea(Vector3 worldPosition, float radius)
    {
        List<Vector3> brokenWallWorldPositions = new List<Vector3>();
        if (wallTilemap == null || floorTilemap == null)
            return brokenWallWorldPositions;

        HashSet<Vector3Int> processedCells = new HashSet<Vector3Int>();

        for (float x = -radius; x <= radius; x += 0.5f)
        {
            for (float y = -radius; y <= radius; y += 0.5f)
            {
                Vector3 offsetPos = worldPosition + new Vector3(x, y, 0);
                Vector3Int cellPos = wallTilemap.WorldToCell(offsetPos);
                if (!processedCells.Add(cellPos))
                    continue;

                if (wallTilemap.GetTile(cellPos) != null)
                {
                    Color tileColor = wallTilemap.GetColor(cellPos);

                    if (tileColor.r < 1.0f)
                    {
                        wallTilemap.SetTile(cellPos, null);
                        floorTilemap.SetTile(cellPos, floorTile);
                        brokenWallWorldPositions.Add(wallTilemap.GetCellCenterWorld(cellPos));
                    }
                }
            }
        }

        return brokenWallWorldPositions;
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
        foreach (var pos in floorPositions)
            floorTilemap.SetTile((Vector3Int)pos, floorTile);

        int minX = floorPositions.Min(p => p.x) - 10;
        int maxX = floorPositions.Max(p => p.x) + 10;
        int minY = floorPositions.Min(p => p.y) - 10;
        int maxY = floorPositions.Max(p => p.y) + 10;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2Int currentTile = new Vector2Int(x, y);
                Vector3Int cellPos = new Vector3Int(x, y, 0);

                if (!floorPositions.Contains(currentTile))
                {
                    wallTilemap.SetTile(cellPos, wallTile);

                    if (IsNearFloor(currentTile, 2) && Random.value < destructableChance)
                    {
                        wallTilemap.SetTileFlags(cellPos, TileFlags.None);
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

        if (enemyPooler != null)
            SyncEnemyCountWithPoolSize();

        player.GetComponent<WallLootHandler>()?.ResetWallLootDropCounterForRoom();

        List<Vector2Int> availableFloors = floorPositions.ToList();
        player.transform.position = new Vector3(0.5f, 0.5f, 0);
        _exitDoorsSpawnedForCurrentFloor = false;

        int enemiesPlaced = 0;
        int attempts = 0;
        int maxAttempts = Mathf.Max(enemyCount * 20, availableFloors.Count * 3);
        while (enemiesPlaced < enemyCount && availableFloors.Count > 0 && attempts < maxAttempts)
        {
            attempts++;
            int randomIndex = Random.Range(0, availableFloors.Count);
            Vector2Int spawnPos = availableFloors[randomIndex];

            if (Vector2.Distance(Vector2.zero, spawnPos) < 6f)
            {
                availableFloors.RemoveAt(randomIndex);
                continue;
            }

            Vector3 spawnWorld = new Vector3(spawnPos.x + 0.5f, spawnPos.y + 0.5f, 0f);
            GameObject enemy = enemyPooler != null
                ? enemyPooler.GetEnemy(spawnWorld, Quaternion.identity)
                : SpawnEnemyFromPrefabList(spawnWorld, Quaternion.identity);
            if (enemy == null)
            {
                Debug.LogWarning("DungeonGenerator: Enemy spawn basarisiz (pool bos olabilir veya enemyPrefabs listesi gecersiz).");
                break;
            }

            availableFloors.RemoveAt(randomIndex);
            enemiesPlaced++;
        }

        if (enemiesPlaced < enemyCount)
        {
            Debug.LogWarning($"DungeonGenerator: Istek {enemyCount}, olusturulan {enemiesPlaced}. Uygun tile veya havuz kapasitesi yetersiz olabilir.");
        }

        TrySpawnExitDoorsIfRoomCleared();
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

    private GameObject SpawnEnemyFromPrefabList(Vector3 worldPosition, Quaternion rotation)
    {
        GameObject prefab = GetRandomEnemyPrefab();
        if (prefab == null) return null;
        return Instantiate(prefab, worldPosition, rotation);
    }

    private GameObject GetRandomEnemyPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
            return null;

        List<GameObject> validPrefabs = enemyPrefabs.Where(p => p != null).ToList();
        if (validPrefabs.Count == 0)
            return null;

        return validPrefabs[Random.Range(0, validPrefabs.Count)];
    }

    public void MoveToNextFloor()
    {
        ClearActiveLoot();
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
        ClearActiveLoot();
        player.GetComponent<Player>()?.ResetForDungeonExit();
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
        
        ClearCurrentExits();

        if (exitUI != null) exitUI.SetActive(false);

        if (dungeonEntrance != null)
        {
            dungeonEntrance.SetActive(true);
            player.transform.position = dungeonEntrance.transform.position + new Vector3(0, -1.5f, 0);
        }

        _currentDungeonFloor = 1;
    }

    private void Update()
    {
        if (Time.time < _nextRoomClearCheckTime) return;
        _nextRoomClearCheckTime = Time.time + RoomClearCheckInterval;
        TrySpawnExitDoorsIfRoomCleared();
    }

    private void TrySpawnExitDoorsIfRoomCleared()
    {
        if (_exitDoorsSpawnedForCurrentFloor || exitPrefab == null || floorPositions.Count < 2) return;

        GameObject[] aliveEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (aliveEnemies.Length > 0) return;

        SpawnDualExitDoors();
    }

    private void SpawnDualExitDoors()
    {
        Vector2Int firstDoorPos;
        Vector2Int secondDoorPos;
        GetSideBySideDoorPositions(out firstDoorPos, out secondDoorPos);

        bool isExitFloor = _currentDungeonFloor % 20 == 0;
        DungeonExit.ExitAction firstAction;
        DungeonExit.ExitAction secondAction;
        if (isExitFloor)
        {
            // Exit katında 1 NextFloor + 1 Exit (toplam yine 2 kapı)
            firstAction = DungeonExit.ExitAction.NextFloor;
            secondAction = DungeonExit.ExitAction.ExitDungeon;
        }
        else
        {
            // Normal katlarda 2 adet NextFloor kapısı
            firstAction = DungeonExit.ExitAction.NextFloor;
            secondAction = DungeonExit.ExitAction.NextFloor;
        }

        GameObject doorA = CreateExitDoorInstance(firstDoorPos, firstAction);
        GameObject doorB = CreateExitDoorInstance(secondDoorPos, secondAction);
        LinkExitDoorPair(doorA, doorB, firstAction, secondAction);

        _exitDoorsSpawnedForCurrentFloor = true;
    }

    private GameObject CreateExitDoorInstance(Vector2Int tilePos, DungeonExit.ExitAction action)
    {
        GameObject instance = Instantiate(exitPrefab, new Vector3(tilePos.x + 0.5f, tilePos.y + 0.5f, 0f), Quaternion.identity);
        currentExitInstances.Add(instance);

        if (action == DungeonExit.ExitAction.ExitDungeon)
        {
            SpriteRenderer spriteRenderer = instance.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.color = ExitDoorTint;
        }

        return instance;
    }

    private void LinkExitDoorPair(GameObject doorA, GameObject doorB, DungeonExit.ExitAction actionA, DungeonExit.ExitAction actionB)
    {
        if (doorA == null || doorB == null) return;

        DungeonExit exitA = doorA.GetComponent<DungeonExit>();
        DungeonExit exitB = doorB.GetComponent<DungeonExit>();
        if (exitA != null)
            exitA.Setup(exitUI, this, actionA, doorB);
        if (exitB != null)
            exitB.Setup(exitUI, this, actionB, doorA);
    }

    private void GetSideBySideDoorPositions(out Vector2Int firstDoorPos, out Vector2Int secondDoorPos)
    {
        Vector2Int[] neighborOffsets = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        float targetDistance = TargetDoorSpawnDistanceFromPlayer;
        Vector2 playerPos = player != null ? (Vector2)player.transform.position : Vector2.zero;
        Vector2 playerForward = GetPlayerForwardDirection();
        Vector2 targetPoint = playerPos + (playerForward * targetDistance);
        Vector2Int? bestFirst = null;
        Vector2Int? bestSecond = null;
        float bestScore = float.MaxValue;

        foreach (Vector2Int floor in floorPositions)
        {
            Vector2 floorWorld = new Vector2(floor.x + 0.5f, floor.y + 0.5f);

            foreach (Vector2Int offset in neighborOffsets)
            {
                Vector2Int candidate = floor + offset;
                if (!floorPositions.Contains(candidate)) continue;

                Vector2 candidateWorld = new Vector2(candidate.x + 0.5f, candidate.y + 0.5f);
                float floorTargetDistance = Vector2.Distance(floorWorld, targetPoint);
                float candidateTargetDistance = Vector2.Distance(candidateWorld, targetPoint);
                float averageTargetDistance = (floorTargetDistance + candidateTargetDistance) * 0.5f;
                float pairCenterToPlayer = Vector2.Distance((floorWorld + candidateWorld) * 0.5f, playerPos);
                float score = averageTargetDistance + Mathf.Abs(pairCenterToPlayer - targetDistance) * 0.35f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestFirst = floor;
                    bestSecond = candidate;
                }
            }
        }

        if (bestFirst.HasValue && bestSecond.HasValue)
        {
            firstDoorPos = bestFirst.Value;
            secondDoorPos = bestSecond.Value;
            return;
        }

        // Güvenli fallback (teorik olarak floorPositions.Count >= 2 iken buraya düşmemeli)
        using (var enumerator = floorPositions.GetEnumerator())
        {
            if (!enumerator.MoveNext())
            {
                firstDoorPos = Vector2Int.zero;
                secondDoorPos = Vector2Int.right;
                return;
            }

            firstDoorPos = enumerator.Current;
            if (enumerator.MoveNext())
            {
                secondDoorPos = enumerator.Current;
                return;
            }
        }

        secondDoorPos = firstDoorPos + Vector2Int.right;
    }

    private Vector2 GetPlayerForwardDirection()
    {
        if (player == null) return Vector2.right;

        // Rotasyonu baz al; sprite X flip kullanıyorsa yönü ters çevir.
        Vector2 forward = player.transform.right;
        if (player.transform.localScale.x < 0f)
            forward = -forward;

        if (forward.sqrMagnitude < 0.0001f)
            return Vector2.right;

        return forward.normalized;
    }

    private void SyncEnemyCountWithPoolSize()
    {
        if (enemyPooler == null) return;
        enemyCount = Mathf.Max(0, enemyPooler.initialPoolSize);
    }

    private void ClearActiveLoot()
    {
        Lootable[] lootables = FindObjectsByType<Lootable>(FindObjectsSortMode.None);
        foreach (Lootable loot in lootables)
        {
            if (loot != null && loot.gameObject.activeInHierarchy)
                loot.ReturnToPool();
        }
    }

    private void ClearCurrentExits()
    {
        for (int i = 0; i < currentExitInstances.Count; i++)
        {
            if (currentExitInstances[i] != null) Destroy(currentExitInstances[i]);
        }
        currentExitInstances.Clear();
        _exitDoorsSpawnedForCurrentFloor = false;
    }
}