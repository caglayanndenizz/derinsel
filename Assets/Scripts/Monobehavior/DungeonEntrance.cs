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
        if (isPlayerNearby)
        {
            interactionUI.SetActive(false);
            generator.GenerateDungeon();
            gameObject.SetActive(false);
        }
    }

    public void OnDeclineEnter()
    {
        interactionUI.SetActive(false);
    }
}