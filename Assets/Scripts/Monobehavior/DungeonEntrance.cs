using UnityEngine;

public class DungeonEntrance : MonoBehaviour
{
    [Header("Referanslar")]
    public GameObject interactionUI;

    private bool _accepted = false;
    private bool isPlayerNearby = false;

    void Start()
    {
        if (interactionUI != null)
            interactionUI.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_accepted) return;
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
        if (!isPlayerNearby || _accepted) return;

        _accepted = true;
        if (interactionUI != null) interactionUI.SetActive(false);
        LevelManager.Instance?.LoadFirstLevel();
    }

    public void OnDeclineEnter()
    {
        if (interactionUI != null) interactionUI.SetActive(false);
    }
}