using UnityEngine;

public class DungeonEntrance : MonoBehaviour
{
    [Header("Referanslar")]
    public DungeonGenerator generator; 
    public GameObject interactionUI;  

    private bool isPlayerNearby = false;

    void Start()
    {
        if (interactionUI != null)
            interactionUI.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
            interactionUI.SetActive(true); 
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            interactionUI.SetActive(false); 
        }
    }

    // "EVET" butonuna basıldığında çalışacak fonksiyon
    public void OnAcceptEnter()
    {
        if (isPlayerNearby)
        {
            // 1. UI Panelini kapat
            interactionUI.SetActive(false);

            // 2. Zindanı oluştur
            generator.GenerateDungeon(); 

            Player player = Object.FindAnyObjectByType<Player>();
            if (player != null)
                player.ApplyDungeonEntrySpeedBoost();

            // 3. Giriş Sprite'ını (bu objeyi) devre dışı bırak
            // Artık zindanın içindesin, kapı görünmez olacak.
            gameObject.SetActive(false); 
        }
    }

    public void OnDeclineEnter()
    {
        interactionUI.SetActive(false);
    }
}