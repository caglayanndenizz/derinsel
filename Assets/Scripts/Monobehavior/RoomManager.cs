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
            Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        
            // --- BURASI YENİ: Küçük bir rastgele sapma ekliyoruz ---
            Vector3 randomOffset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0);
            Vector3 finalSpawnPos = sp.position + randomOffset;

            GameObject prefab = data.enemyPrefabs[Random.Range(0, data.enemyPrefabs.Length)];
            GameObject enemyObj = Instantiate(prefab, finalSpawnPos, Quaternion.identity);
        
            BaseEntity enemyScript = enemyObj.GetComponent<BaseEntity>();
                if (enemyScript != null)
                {
                    activeEnemies.Add(enemyScript);

                    if (enemyScript is Enemy enemy) 
                    {
                        if (data.type == EnemyType.Swarm)
                        {
                            enemy.currentState = Enemy.State.Chase; //
                            enemy.detectionRange = enemy.expandedDetectionRange;
                        }
                        else
                        {
                            enemy.currentState = Enemy.State.Patrol; //
                        }
                    }
                }
        
        }
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