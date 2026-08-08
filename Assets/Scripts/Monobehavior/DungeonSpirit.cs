using UnityEngine;

public class DungeonSpirit : MonoBehaviour
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
            Time.timeScale = 0f;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            if (interactionUI != null) interactionUI.SetActive(false);
            Time.timeScale = 1f;
        }
    }

    public void OnAcceptEnter()
    {
        if (!isPlayerNearby || _accepted) return;

        _accepted = true;
        if (interactionUI != null) interactionUI.SetActive(false);

        // Fader coroutine runs on scaled Time.deltaTime, so time must be
        // resumed before it starts or the fade-to-black never progresses.
        Time.timeScale = 1f;
        LevelManager.Instance?.AdvanceToNextLevel();
    }

    public void OnDeclineEnter()
    {
        if (interactionUI != null) interactionUI.SetActive(false);
        Time.timeScale = 1f;
    }
}
