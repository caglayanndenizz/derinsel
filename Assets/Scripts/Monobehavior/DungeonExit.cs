using UnityEngine;

public class DungeonExit : MonoBehaviour
{
    public enum ExitAction
    {
        NextFloor,
        ExitDungeon
    }

    private GameObject interactionUI;
    private DungeonGenerator dungeonGenerator;
    private ExitAction exitAction = ExitAction.NextFloor;
    private bool _isTriggered;

    // Generator kapıyı oluşturduğunda UI'ı bu fonksiyonla tanıtır
    public void Setup(GameObject ui)
    {
        interactionUI = ui;
        dungeonGenerator = Object.FindAnyObjectByType<DungeonGenerator>();
        _isTriggered = false;
    }

    public void Setup(GameObject ui, DungeonGenerator generator, ExitAction action)
    {
        interactionUI = ui;
        dungeonGenerator = generator != null ? generator : Object.FindAnyObjectByType<DungeonGenerator>();
        exitAction = action;
        _isTriggered = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || _isTriggered) return;

        _isTriggered = true;
        Debug.Log($"Oyuncu {exitAction} kapisina geldi!");

        if (interactionUI != null) interactionUI.SetActive(false);

        if (dungeonGenerator == null)
        {
            Debug.LogWarning("DungeonExit: DungeonGenerator bulunamadi.");
            return;
        }

        if (exitAction == ExitAction.NextFloor)
        {
            dungeonGenerator.StartNextFloorTransition();
        }
        else
        {
            dungeonGenerator.StartExitTransition();
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