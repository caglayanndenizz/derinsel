using UnityEngine;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    [Header("Room Configuration")]
    public RoomData data; // Hangi oda şablonunu kullanıyoruz?
    public Transform[] spawnPoints; // Düşmanların doğabileceği noktalar

    [Header("State Tracking")]
    private List<BaseEntity> activeEnemies = new List<BaseEntity>();
    private bool isCleared = false;

    void Start()
    {
        // Oyun başladığında veya odaya girildiğinde düşmanları doğur
        SpawnEnemies();
    }

    void OnEnable()
    {
        // Herhangi bir varlık öldüğünde haberdar olmak için abone oluyoruz
        BaseEntity.OnAnyEntityDie += HandleEnemyDeath;
    }

    void OnDisable()
    {
        // Aboneliği temizliyoruz
        BaseEntity.OnAnyEntityDie -= HandleEnemyDeath;
    }

    private void SpawnEnemies()
    {
        // RoomData'daki min/max aralığında kaç düşman doğacağını seç
        int count = Random.Range(data.minEnemyCount, data.maxEnemyCount + 1);

        for (int i = 0; i < count; i++)
        {
            // Rastgele bir doğuş noktası seç
            Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
            
            // Havuzdaki rastgele bir düşman prefabını seç
            GameObject prefab = data.enemyPrefabs[Random.Range(0, data.enemyPrefabs.Length)];

            // Düşmanı yarat
            GameObject enemyObj = Instantiate(prefab, sp.position, Quaternion.identity);
            
            // Takip listesine ekle
            BaseEntity enemyScript = enemyObj.GetComponent<BaseEntity>();
            if (enemyScript != null)
            {
                activeEnemies.Add(enemyScript);
            }
        }
        
        Debug.Log(data.roomName + " odası hazır! Düşman sayısı: " + activeEnemies.Count);
    }

    private void HandleEnemyDeath(BaseEntity victim)
    {
        // Eğer ölen şey bizim listemizdeki bir düşmansa
        if (activeEnemies.Contains(victim))
        {
            activeEnemies.Remove(victim);
            Debug.Log("Düşman elendi! Kalan: " + activeEnemies.Count);

            // Tüm düşmanlar bitti mi?
            if (activeEnemies.Count <= 0 && !isCleared)
            {
                RoomCleared();
            }
        }
    }

    private void RoomCleared()
    {
        isCleared = true;
        Debug.Log("Oda Temizlendi! Kapılar açılıyor...");
        // Burada kapıları açacak veya ödül düşürecek bir Event fırlatabiliriz.
    }
}