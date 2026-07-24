using UnityEngine;

/// <summary>
/// Resting-floor NPC: touching it opens the augment inventory panel immediately, no confirm
/// step — mirrors ChestInteractable's auto-open pattern. Set sellable=true for the "sell" NPC,
/// false for a plain read-only "view" NPC — the only difference is which mode it opens in.
/// </summary>
public class AugmentInventoryNpc : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AugmentInventoryUI inventoryUI;

    [Header("Behavior")]
    [Tooltip("True = this NPC lets the player sell augments. False = read-only view NPC.")]
    [SerializeField] private bool sellable = true;

    private void Start()
    {
        if (inventoryUI == null)
            inventoryUI = Object.FindAnyObjectByType<AugmentInventoryUI>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (inventoryUI == null) return;
        inventoryUI.Show(sellable);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (inventoryUI == null) return;
        inventoryUI.Hide();
    }
}
