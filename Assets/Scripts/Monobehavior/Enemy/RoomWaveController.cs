using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Her kat için wave sırasını yönetir.
/// DungeonGenerator.PlaceEntities() tarafından çağrılır; tüm dalgalar temizlenince
/// DungeonGenerator.OnAllWavesCleared() ile çıkış kapılarının açılmasını tetikler.
/// </summary>
public class RoomWaveController : MonoBehaviour
{
    public static RoomWaveController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private DungeonGenerator dungeonGenerator;
    [SerializeField] private EnemyObjectPooler enemyPooler;

    [Header("Floor Wave Configs")]
    [Tooltip("Her config bir kat aralığını kapsar. En yüksek fromFloor eşleşmesi kullanılır.")]
    [SerializeField] private List<FloorWaveConfig> floorConfigs = new();

    [Header("Timing")]
    [Tooltip("Dalgalar arası bekleme süresi (saniye).")]
    [SerializeField] private float delayBetweenWaves = 1.5f;

    [Header("Fallback")]
    [Tooltip("Eşleşen config yoksa spawn edilecek enemy sayısı (eski davranış).")]
    [SerializeField] private int fallbackEnemyCount = 8;

    // ── Read-only runtime state ──────────────────────────────────────────────
    public int  CurrentWaveIndex => _currentWaveIndex;
    public int  TotalWaves       => _wavesForFloor?.Count ?? 0;
    public bool WavesRunning     => _wavesRunning;

    private List<Vector2Int>    _spawnablePositions;
    private int                 _currentWaveIndex;
    private List<WaveDefinition> _wavesForFloor;
    private int                 _aliveEnemiesInWave;
    private bool                _wavesRunning;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (dungeonGenerator == null)
            dungeonGenerator = Object.FindAnyObjectByType<DungeonGenerator>();
        if (enemyPooler == null)
            enemyPooler = EnemyObjectPooler.Instance ?? Object.FindAnyObjectByType<EnemyObjectPooler>();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Called by DungeonGenerator at the start of each floor.</summary>
    public void StartFloor(List<Vector2Int> spawnablePositions, int floor)
    {
        StopAllCoroutines();
        _spawnablePositions  = new List<Vector2Int>(spawnablePositions);
        _currentWaveIndex    = 0;
        _aliveEnemiesInWave  = 0;
        _wavesRunning        = false;

        if (enemyPooler == null)
            enemyPooler = EnemyObjectPooler.Instance;

        _wavesForFloor = GetWavesForFloor(floor);

        if (_wavesForFloor == null || _wavesForFloor.Count == 0)
        {
            SpawnFallback();
            return;
        }

        _wavesRunning = true;
        StartCoroutine(SpawnWaveRoutine(_wavesForFloor[0]));
    }

    /// <summary>Call on floor transition so lingering coroutines are cancelled.</summary>
    public void ResetForNewFloor()
    {
        StopAllCoroutines();
        _wavesRunning       = false;
        _aliveEnemiesInWave = 0;
    }

    // ── Wave flow ────────────────────────────────────────────────────────────

    private IEnumerator SpawnWaveRoutine(WaveDefinition wave)
    {
        if (wave == null || wave.entries == null || wave.entries.Count == 0)
        {
            OnWaveCleared();
            yield break;
        }

        List<(Enemy.EnemyType type, Vector3 pos)> toSpawn = BuildSpawnList(wave);
        _aliveEnemiesInWave = toSpawn.Count;

        if (_aliveEnemiesInWave == 0)
        {
            OnWaveCleared();
            yield break;
        }

        foreach (var (type, pos) in toSpawn)
        {
            if (!_wavesRunning) yield break;

            GameObject go = enemyPooler != null
                ? enemyPooler.GetEnemyOfType(type, pos, Quaternion.identity)
                : null;

            if (go == null)
            {
                _aliveEnemiesInWave = Mathf.Max(0, _aliveEnemiesInWave - 1);
                if (_aliveEnemiesInWave == 0) { OnWaveCleared(); yield break; }
                continue;
            }

            Enemy e = go.GetComponent<Enemy>();
            if (e != null)
                e.Died += OnEnemyDied;
            else
            {
                _aliveEnemiesInWave = Mathf.Max(0, _aliveEnemiesInWave - 1);
                if (_aliveEnemiesInWave == 0) { OnWaveCleared(); yield break; }
            }

            yield return new WaitForSeconds(wave.spawnDelayBetweenEnemies);
        }
    }

    private void OnEnemyDied(Enemy e)
    {
        e.Died -= OnEnemyDied;
        _aliveEnemiesInWave = Mathf.Max(0, _aliveEnemiesInWave - 1);
        if (_aliveEnemiesInWave == 0)
            OnWaveCleared();
    }

    private void OnWaveCleared()
    {
        _currentWaveIndex++;

        if (_wavesForFloor != null && _currentWaveIndex < _wavesForFloor.Count)
        {
            StartCoroutine(NextWaveAfterDelay(_wavesForFloor[_currentWaveIndex]));
            return;
        }

        _wavesRunning = false;
        dungeonGenerator?.OnAllWavesCleared();
    }

    private IEnumerator NextWaveAfterDelay(WaveDefinition wave)
    {
        yield return new WaitForSeconds(delayBetweenWaves);
        StartCoroutine(SpawnWaveRoutine(wave));
    }

    // ── Spawn helpers ────────────────────────────────────────────────────────

    private void SpawnFallback()
    {
        if (enemyPooler != null)
        {
            List<Vector2Int> shuffled = new List<Vector2Int>(_spawnablePositions);
            Shuffle(shuffled);

            int spawned = 0;
            foreach (Vector2Int tile in shuffled)
            {
                if (spawned >= fallbackEnemyCount) break;
                Vector3 world = TileToWorld(tile);
                if (Vector2.Distance(Vector2.zero, world) < 6f) continue;
                if (enemyPooler.GetEnemy(world, Quaternion.identity) != null)
                    spawned++;
            }
        }

        // Fallback'te wave tracking yok; DungeonGenerator'ın Update() tag-check'ini
        // devralabilmesi için wave sistemini devre dışı bırak.
        dungeonGenerator?.OnAllWavesCleared();
    }

    private List<(Enemy.EnemyType type, Vector3 pos)> BuildSpawnList(WaveDefinition wave)
    {
        var validTiles = new List<Vector2Int>();
        foreach (Vector2Int tile in _spawnablePositions)
        {
            if (Vector2.Distance(Vector2.zero, TileToWorld(tile)) >= 6f)
                validTiles.Add(tile);
        }
        Shuffle(validTiles);

        var result = new List<(Enemy.EnemyType, Vector3)>();
        int posIdx = 0;

        foreach (WaveEntry entry in wave.entries)
        {
            for (int i = 0; i < entry.count; i++)
            {
                Vector3 spawnPos;
                if (validTiles.Count > 0)
                {
                    int idx = posIdx < validTiles.Count ? posIdx : Random.Range(0, validTiles.Count);
                    spawnPos = TileToWorld(validTiles[idx]);
                    posIdx++;
                }
                else
                {
                    spawnPos = new Vector3(Random.Range(-15f, 15f), Random.Range(-15f, 15f), 0f);
                }
                result.Add((entry.enemyType, spawnPos));
            }
        }

        Shuffle(result);
        return result;
    }

    // ── Public query ─────────────────────────────────────────────────────────

    /// <summary>
    /// Verilen kattaki tüm wave'lerdeki toplam enemy sayısını döndürür.
    /// Wave config yoksa fallbackEnemyCount kullanılır.
    /// </summary>
    public int GetTotalEnemyCountForFloor(int floor)
    {
        List<WaveDefinition> waves = GetWavesForFloor(floor);
        if (waves == null || waves.Count == 0) return fallbackEnemyCount;

        int total = 0;
        foreach (WaveDefinition wave in waves)
        {
            if (wave == null) continue;
            foreach (WaveEntry entry in wave.entries)
                total += Mathf.Max(0, entry.count);
        }
        return Mathf.Max(1, total);
    }

    // ── Config lookup ────────────────────────────────────────────────────────

    private List<WaveDefinition> GetWavesForFloor(int floor)
    {
        if (floorConfigs == null || floorConfigs.Count == 0) return null;

        FloorWaveConfig best = null;
        foreach (FloorWaveConfig cfg in floorConfigs)
        {
            if (cfg == null) continue;
            if (floor < cfg.fromFloor) continue;
            if (cfg.toFloor >= 0 && floor > cfg.toFloor) continue;
            if (best == null || cfg.fromFloor > best.fromFloor)
                best = cfg;
        }
        return best?.waves;
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static Vector3 TileToWorld(Vector2Int tile) =>
        new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f);

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
