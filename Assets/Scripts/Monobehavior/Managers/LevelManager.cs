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
    [Tooltip("Hiyerarsideki World/Lobby objesi. Level yuklendikten sonra kapanir, level unload edilince tekrar acilir.")]
    [SerializeField] private GameObject lobbyRoot;

    [Header("Level End Chest")]
    [SerializeField] private GameObject goldenChestPrefab;
    [SerializeField] private GameObject silverChestPrefab;
    [Tooltip("Oyuncu augmenti sectikten bu kadar saniye sonra sonraki levela gecer.")]
    [SerializeField] private float chestToLevelDelay = 2f;

    [Header("Healing")]
    [Tooltip("levelPrefabs listesindeki Resting prefabi ile ayni referans olmali — bu levela girildiginde can barini full doldurur.")]
    [SerializeField] private GameObject restingLevelPrefab;
    [Tooltip("Bir level gecildiginde (sonraki levela ilerlerken) can barina eklenen yuzde (0.1 = %10).")]
    [SerializeField] private float levelClearHealFraction = 0.1f;

    [Header("Sound")]
    private AudioSource _audioSource;
    public AudioClip gameMusic;

    private int _currentLevelIndex = -1;
    private GameObject _activeLevelInstance;
    private GameObject _levelEndChest;

    public int CurrentLevel => _currentLevelIndex + 1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = true;
        if (gameMusic != null)
        {
            _audioSource.clip = gameMusic;
            _audioSource.Play();
        }
    }

    // DungeonEntrance tarafindan cagirilir.
    public void LoadFirstLevel()
    {
        StartCoroutine(LoadLevelRoutine(0));
    }

    // Level temizlendiginde (veya DungeonSpirit ile manuel) cagirilir; siradaki level'a ilerler.
    public void AdvanceToNextLevel()
    {
        if (_currentLevelIndex >= levelPrefabs.Count - 1)
            return; // Level 5 son level — simdilik ilerleme yok

        Player playerComponent = player != null ? player.GetComponent<Player>() : null;
        if (playerComponent != null)
            playerComponent.Heal(playerComponent.MaxHealth * levelClearHealFraction);

        StartCoroutine(LoadLevelRoutine(_currentLevelIndex + 1));
    }

    public void SpawnLevelEndChest(Vector3 position)
    {
        bool spawnGolden = Random.value < 0.5f;
        GameObject prefabToSpawn = spawnGolden ? goldenChestPrefab : silverChestPrefab;

        if (prefabToSpawn == null)
        {
            Debug.LogWarning("LevelManager: chest prefab atanmamis, direkt level gecisi yapiliyor.");
            AdvanceToNextLevel();
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
        AdvanceToNextLevel();
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
        if (lobbyRoot != null) lobbyRoot.SetActive(false);

        if (dungeonEntrance != null) dungeonEntrance.gameObject.SetActive(false);

        _activeLevelInstance = Instantiate(levelPrefabs[levelIndex]);

        var births = new List<Transform>();
        FindChildrenByNamePrefix(_activeLevelInstance.transform, "Birth", births);

        Vector3 spawnPos;
        if (births.Count > 0)
        {
            Transform birth = births[Random.Range(0, births.Count)];
            spawnPos = new Vector3(birth.position.x + 0.5f, birth.position.y + 0.5f, 0f);
        }
        else
        {
            Debug.LogWarning($"LevelManager: Level {levelIndex + 1} icinde 'Birth' objesi bulunamadi, (0.5, 0.5) kullaniliyor.");
            spawnPos = new Vector3(0.5f, 0.5f, 0f);
        }

        if (player != null)
            player.transform.position = spawnPos;

        if (levelPrefabs[levelIndex] == restingLevelPrefab)
        {
            Player playerComponent = player != null ? player.GetComponent<Player>() : null;
            playerComponent?.Heal(playerComponent.MaxHealth);
        }
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
        if (lobbyRoot != null) lobbyRoot.SetActive(true);
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

    /// <summary>Collects every descendant whose name is exactly namePrefix or starts with it (covers Unity's "Birth (1)", "Birth (2)" auto-dedup naming).</summary>
    private static void FindChildrenByNamePrefix(Transform root, string namePrefix, List<Transform> results)
    {
        if (root.name == namePrefix || root.name.StartsWith(namePrefix))
            results.Add(root);
        foreach (Transform child in root)
            FindChildrenByNamePrefix(child, namePrefix, results);
    }
}
