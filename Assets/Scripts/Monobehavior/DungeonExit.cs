using UnityEngine;

public class DungeonExit : MonoBehaviour
{
    public enum ExitAction
    {
        NextFloor,
        ExitDungeon
    }

    [Header("Iki kapi yan yana")]
    [Tooltip("Oyuncu bu kapıya kardeş kapıdan belirgin şekilde daha yakın olmalı; arada yanlış tetiklenmeyi azaltır.")]
    public float closerThanSiblingEpsilon = 0.05f;

    private GameObject interactionUI;
    private DungeonGenerator dungeonGenerator;
    private ExitAction exitAction = ExitAction.NextFloor;
    private bool _isTriggered;
    private GameObject _siblingExit;
    private Collider2D _triggerCollider;

    public void Setup(GameObject ui, DungeonGenerator generator, ExitAction action, GameObject siblingExit)
    {
        interactionUI = ui;
        dungeonGenerator = generator != null ? generator : Object.FindAnyObjectByType<DungeonGenerator>();
        exitAction = action;
        _siblingExit = siblingExit;
        _isTriggered = false;

        if (_triggerCollider == null)
            _triggerCollider = GetComponent<Collider2D>();
    }

    private static Vector2 GetPlayerReferencePoint(Collider2D other)
    {
        Rigidbody2D rb = other.attachedRigidbody;
        if (rb != null)
            return rb.position;
        return other.transform.position;
    }

    private bool ShouldThisDoorHandleEnter(Vector2 playerPos)
    {
        if (_siblingExit == null || !_siblingExit.activeInHierarchy)
            return true;

        float dSelf = Vector2.Distance(playerPos, transform.position);
        float dSibling = Vector2.Distance(playerPos, _siblingExit.transform.position);

        if (dSibling < dSelf - closerThanSiblingEpsilon)
            return false;
        if (Mathf.Abs(dSelf - dSibling) <= closerThanSiblingEpsilon && GetInstanceID() > _siblingExit.GetInstanceID())
            return false;

        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || _isTriggered) return;

        Vector2 playerPos = GetPlayerReferencePoint(other);
        if (!ShouldThisDoorHandleEnter(playerPos))
            return;

        CommitExit();
    }

    private void CommitExit()
    {
        _isTriggered = true;

        if (_siblingExit != null)
            _siblingExit.SetActive(false);

        if (_triggerCollider == null)
            _triggerCollider = GetComponent<Collider2D>();
        if (_triggerCollider != null)
            _triggerCollider.enabled = false;

        Debug.Log($"Oyuncu {exitAction} kapisina girdi.");

        if (interactionUI != null) interactionUI.SetActive(false);

        if (dungeonGenerator == null)
        {
            Debug.LogWarning("DungeonExit: DungeonGenerator bulunamadi.");
            return;
        }

        if (exitAction == ExitAction.NextFloor)
            dungeonGenerator.StartNextFloorTransition();
        else
            dungeonGenerator.StartExitTransition();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_isTriggered) return;
        if (other.CompareTag("Player"))
        {
            Debug.Log("Oyuncu kapıdan uzaklaştı!");
            if (interactionUI != null) interactionUI.SetActive(false);
        }
    }
}
