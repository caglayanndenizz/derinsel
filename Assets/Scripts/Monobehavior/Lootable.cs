using UnityEngine;

public class Lootable : MonoBehaviour
{
    [Header("Basic Settings")]
    public float lifetime = 0.4f;
    public int value = 1;
    public bool isGold = true;
    
    [Header("Magnet Settings")]
    public float detectionRange = 10f; // Oyuncuya çekilme mesafesi (x kadar uzaklık)
    public float moveSpeed = 6f;     // Oyuncuya uçma hızı
    
    private bool isCollected = false;
    private Transform playerTransform;

    private void Awake()
    {
        ResolvePlayerTransform();
    }

    private void OnEnable()
    {
        isCollected = false;
        ResolvePlayerTransform();
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(ReturnToPool));
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
       /* if (isCollected)
        {
            // Toplandıktan sonra hafifçe yukarı yükselme efekti (eski kodun)
            transform.Translate(Vector2.up * Time.deltaTime * 1f);
            return;
        }
        */
        // --- MIKNATIS MEKANİĞİ ---
        if (playerTransform != null)
        {
            float distance = Vector2.Distance(transform.position, playerTransform.position);

            // Eğer oyuncu x (detectionRange) kadar yakınsa
            if (distance <= detectionRange)
            {
                // Obje oyuncuya doğru hızla hareket eder
                transform.position = Vector2.MoveTowards(transform.position, playerTransform.position, moveSpeed * Time.deltaTime);
                
                // Hızın gitgide artmasını istersen şunu kullanabilirsin:
                // moveSpeed += Time.deltaTime * 5f;
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;

        if (other.CompareTag("Player"))
        {
            Collect(other.gameObject);
        }
    }

    // Toplama mantığını ayrı bir fonksiyona aldık ki kod daha temiz olsun
    private void Collect(GameObject playerObj)
    {
        isCollected = true;
        Player player = playerObj.GetComponent<Player>();
        
        if (player != null)
        {
            if (isGold) player.goldCount += value;
            else player.experienceCount += value;
        }

        // Kısa gecikmeden sonra havuza iade edilir
        Invoke(nameof(ReturnToPool), lifetime);
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