using UnityEngine;

public class DungeonEntrance : MonoBehaviour
{
    [Header("Referanslar")]
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
            if (interactionUI != null) interactionUI.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            if (interactionUI != null) interactionUI.SetActive(false);
        }
    }

    public void OnAcceptEnter()
    {
        if (!isPlayerNearby) return;

        if (interactionUI != null) interactionUI.SetActive(false);
        gameObject.SetActive(false);
        LevelManager.Instance?.LoadFirstLevel();
    }

    public void OnDeclineEnter()
    {
        if (interactionUI != null) interactionUI.SetActive(false);
    }
}