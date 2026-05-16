using UnityEngine;

public class Lootable : MonoBehaviour, ICollectable
{
    [Header("Basic Settings")]
    public int value = 1;
    [Tooltip("isGold kapaliysa (XP orb) verilecek deneyim miktari.")]
    public int experienceValue = 10;
    public bool isGold = true;

    [Header("Magnet Settings")]
    public float moveSpeed = 6f;
    public float goldHomingDelay = 5f;

    bool _isCollected;
    Transform _playerTransform;
    bool _goldHomingUnlocked;

    void Awake()
    {
        ResolvePlayerTransform();
    }

    void OnEnable()
    {
        _isCollected = false;
        _goldHomingUnlocked = !isGold;
        ResolvePlayerTransform();
        CancelInvoke(nameof(EnableGoldHoming));
    }

    void OnDisable()
    {
        CancelInvoke(nameof(EnableGoldHoming));
    }

    void EnableGoldHoming()
    {
        _goldHomingUnlocked = true;
    }

    void ResolvePlayerTransform()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            _playerTransform = playerObj.transform;
    }

    void Update()
    {
        if (_isCollected) return;

        if (_playerTransform == null)
            ResolvePlayerTransform();

        if (_playerTransform == null) return;

        if (isGold && !_goldHomingUnlocked) return;
        transform.position = Vector2.MoveTowards(transform.position, _playerTransform.position, moveSpeed * Time.deltaTime);
    }

    public void Collect(Player player)
    {
        if (_isCollected) return;
        _isCollected = true;

        if (player != null)
        {
            if (isGold)
                player.PlayerCurrency?.AddGold(Mathf.Max(0, value));
            else
                player.AddExperience(Mathf.Max(0, experienceValue));
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
