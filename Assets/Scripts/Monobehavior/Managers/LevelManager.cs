using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Level Prefabs (Level_01 → Level_05)")]
    [SerializeField] private List<GameObject> levelPrefabs = new();

    [Header("References")]
    [SerializeField] public GameObject player;
    [SerializeField] private CinemachineCamera vcam;
    [SerializeField] private TransitionFader transitionFader;
    [SerializeField] private EnemyObjectPooler enemyPooler;
    [SerializeField] private DungeonEntrance dungeonEntrance;
    [SerializeField] private AugmentSelectionUI augmentUI;
    [Tooltip("Lobby sahnesinim Global Light 2D'si. Level yuklendikten sonra kapanir.")]
    [SerializeField] private Light2D lobbyGlobalLight;

    [Header("Level End Chest")]
    [SerializeField] private GameObject goldenChestPrefab;
    [SerializeField] private GameObject silverChestPrefab;
    [Tooltip("Oyuncu augmenti sectikten bu kadar saniye sonra sonraki levela gecer.")]
    [SerializeField] private float chestToLevelDelay = 2f;

    private int _currentLevelIndex = -1;
    private GameObject _activeLevelInstance;
    private GameObject _levelEndChest;

    public int CurrentLevel => _currentLevelIndex + 1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // DungeonEntrance tarafindan cagirilir.
    public void LoadFirstLevel()
    {
        StartCoroutine(LoadLevelRoutine(0));
    }

    // Mini-boss olduruldugunde cagirilacak altyapi (EnemyType entegrasyonu sonraya).
    public void OnMiniBossKilled()
    {
        if (_currentLevelIndex >= levelPrefabs.Count - 1)
            return; // Level 5 son level — simdilik ilerleme yok

        StartCoroutine(LoadLevelRoutine(_currentLevelIndex + 1));
    }

    public void SpawnLevelEndChest(Vector3 position)
    {
        bool spawnGolden = Random.value < 0.5f;
        GameObject prefabToSpawn = spawnGolden ? goldenChestPrefab : silverChestPrefab;

        if (prefabToSpawn == null)
        {
            Debug.LogWarning("LevelManager: chest prefab atanmamis, direkt level gecisi yapiliyor.");
            OnMiniBossKilled();
            return;
        }

        _levelEndChest = Instantiate(prefabToSpawn, position, Quaternion.identity);

        if (augmentUI != null)
            augmentUI.OnChestAugmentSelected += OnChestAugmentSelected;
    }

    private void OnChestAugmentSelected()
    {
        if (augmentUI != null)
            augmentUI.OnChestAugmentSelected -= OnChestAugmentSelected;

        StartCoroutine(DelayedLevelTransition());
    }

    private IEnumerator DelayedLevelTransition()
    {
        yield return new WaitForSeconds(chestToLevelDelay);
        OnMiniBossKilled();
    }

    private IEnumerator LoadLevelRoutine(int levelIndex)
    {
        yield return transitionFader.FadeOutIn(() =>
        {
            UnloadCurrentLevel();
            SpawnLevel(levelIndex);
            if (vcam != null && player != null)
                vcam.ForceCameraPosition(player.transform.position, Quaternion.identity);
        });
    }

    private void SpawnLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= levelPrefabs.Count || levelPrefabs[levelIndex] == null)
        {
            Debug.LogError($"LevelManager: levelPrefabs[{levelIndex}] atanmamis veya gecersiz.");
            return;
        }

        _currentLevelIndex = levelIndex;
        if (lobbyGlobalLight != null) lobbyGlobalLight.enabled = false;

        if (dungeonEntrance != null) dungeonEntrance.gameObject.SetActive(false);

        _activeLevelInstance = Instantiate(levelPrefabs[levelIndex]);

        Transform birth = FindChildByName(_activeLevelInstance.transform, "Birth");
        Vector3 spawnPos;
        if (birth != null)
            spawnPos = new Vector3(birth.position.x + 0.5f, birth.position.y + 0.5f, 0f);
        else
        {
            Debug.LogWarning($"LevelManager: Level {levelIndex + 1} icinde 'Birth' objesi bulunamadi, (0.5, 0.5) kullaniliyor.");
            spawnPos = new Vector3(0.5f, 0.5f, 0f);
        }

        if (player != null)
            player.transform.position = spawnPos;
    }

    private void UnloadCurrentLevel()
    {
        CleanupEnemies();
        CleanupLoot();
        CleanupLevelEndChest();
        KillCounter.Instance?.CleanupChests();

        if (_activeLevelInstance != null)
        {
            Destroy(_activeLevelInstance);
            _activeLevelInstance = null;
        }

        if (lobbyGlobalLight != null) lobbyGlobalLight.enabled = true;
    }

    private void CleanupLevelEndChest()
    {
        if (augmentUI != null)
            augmentUI.OnChestAugmentSelected -= OnChestAugmentSelected;

        if (_levelEndChest != null)
        {
            Destroy(_levelEndChest);
            _levelEndChest = null;
        }
    }

    private void CleanupEnemies()
    {
        if (enemyPooler == null)
            enemyPooler = EnemyObjectPooler.Instance;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject e in enemies)
        {
            if (enemyPooler != null) enemyPooler.ReturnEnemy(e);
            else Destroy(e);
        }
    }

    private void CleanupLoot()
    {
        Lootable[] lootables = FindObjectsByType<Lootable>(FindObjectsSortMode.None);
        foreach (Lootable loot in lootables)
        {
            if (loot != null && loot.gameObject.activeInHierarchy)
                loot.ReturnToPool();
        }
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root.name == targetName) return root;
        foreach (Transform child in root)
        {
            Transform found = FindChildByName(child, targetName);
            if (found != null) return found;
        }
        return null;
    }
}
