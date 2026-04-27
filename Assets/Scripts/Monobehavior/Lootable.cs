using UnityEngine;

public class Lootable : MonoBehaviour
{
    [Header("Basic Settings")]
    public int value = 1;
    public bool isGold = true;
    
    [Header("Magnet Settings")]
    public float moveSpeed = 6f;
    public float goldHomingDelay = 5f;
    
    private bool isCollected = false;
    private Transform playerTransform;
    private bool goldHomingUnlocked;

    private void Awake()
    {
        ResolvePlayerTransform();
    }

    private void OnEnable()
    {
        isCollected = false;
        goldHomingUnlocked = !isGold;
        ResolvePlayerTransform();
        CancelInvoke(nameof(EnableGoldHoming));

        if (isGold)
            Invoke(nameof(EnableGoldHoming), Mathf.Max(0f, goldHomingDelay));
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(EnableGoldHoming));
    }

    private void EnableGoldHoming()
    {
        goldHomingUnlocked = true;
    }

    private void ResolvePlayerTransform()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    void Update()
    {
        if (isCollected) return;

        if (playerTransform == null)
        {
            ResolvePlayerTransform();
        }

        if (playerTransform == null) return;

        if (isGold && !goldHomingUnlocked) return;
        transform.position = Vector2.MoveTowards(transform.position, playerTransform.position, moveSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;

        if (other.CompareTag("Player"))
        {
            Collect(other.gameObject);
        }
    }

    private void Collect(GameObject playerObj)
    {
        isCollected = true;
        Player player = playerObj.GetComponent<Player>();
        
        if (player != null)
        {
            if (isGold) player.AddGold(value);
            else player.AddExperience(value);
        }

        ReturnToPool();
    }

    private void ReturnToPool()
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

        // Pooler bulunamazsa güvenli fallback
        Destroy(gameObject);
    }
}