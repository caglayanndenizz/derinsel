using UnityEngine;

public class Lootable : MonoBehaviour, ICollectable
{
    [Header("Basic Settings")]
    public int value = 1;
    [Tooltip("isGold kapaliysa (XP orb) verilecek deneyim miktari.")]
    public int experienceValue = 12;
    public bool isGold = true;

    [Header("Magnet Settings")]
    public float moveSpeed = 6f;

    bool _isCollected;
    Transform _playerTransform;

    void Awake()
    {
        ResolvePlayerTransform();
    }

    void OnEnable()
    {
        _isCollected = false;
        ResolvePlayerTransform();
    }

    void ResolvePlayerTransform()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            _playerTransform = playerObj.transform;
    }

    void Update()
    {
        if (_isCollected || isGold) return;

        if (_playerTransform == null)
            ResolvePlayerTransform();

        if (_playerTransform == null) return;

        transform.position = Vector2.MoveTowards(transform.position, _playerTransform.position, moveSpeed * Time.deltaTime);
    }

    public void Collect(Player player)
    {
        if (_isCollected) return;
        _isCollected = true;

        if (player != null)
        {
            if (isGold)
            {
                player.PlayerCurrency?.AddGold(Mathf.Max(0, value));
            }
            else
            {
                int playerLevel = player.PlayerLevel != null ? player.PlayerLevel.CurrentLevel : 1;
                // Her 5 level'da bir %50 artis: level 1-5 = x1, 6-10 = x1.5, 11-15 = x2.25 ...
                float levelMultiplier = Mathf.Pow(1.5f, (playerLevel - 1) / 5);
                player.AddExperience(Mathf.Max(0, experienceValue * levelMultiplier));
            }
        }

        ReturnToPool();
    }

    public void ReturnToPool()
    {
        if (isGold && GoldLootPooler.Instance != null)
        {
            GoldLootPooler.Instance.ReturnGold(gameObject);
            return;
        }

        if (!isGold && ExperienceLootPooler.Instance != null)
        {
            ExperienceLootPooler.Instance.ReturnExperience(gameObject);
            return;
        }

        Destroy(gameObject);
    }
}
