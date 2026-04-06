using UnityEngine;

public class DungeonExit : MonoBehaviour
{
    private GameObject interactionUI;

    // Generator kapıyı oluşturduğunda UI'ı bu fonksiyonla tanıtır
    public void Setup(GameObject ui)
    {
        interactionUI = ui;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Oyuncu kapıya geldi!"); 
            // isPlayerNearby değişkenini sildik, direkt UI'ı açıyoruz
            if (interactionUI != null) interactionUI.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Oyuncu kapıdan uzaklaştı!");
            // isPlayerNearby değişkenini sildik, direkt UI'ı kapatıyoruz
            if (interactionUI != null) interactionUI.SetActive(false);
        }
    }
}