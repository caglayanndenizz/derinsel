using UnityEngine;

/// <summary>Global hotkey (default L) that toggles the read-only augment list from any floor.</summary>
public class AugmentInventoryHotkey : MonoBehaviour
{
    [SerializeField] private AugmentInventoryUI inventoryUI;
    [SerializeField] private AugmentSelectionUI augmentSelectionUI;
    [SerializeField] private KeyCode toggleKey = KeyCode.L;

    private void Awake()
    {
        if (inventoryUI == null)
            inventoryUI = Object.FindAnyObjectByType<AugmentInventoryUI>();
        if (augmentSelectionUI == null)
            augmentSelectionUI = Object.FindAnyObjectByType<AugmentSelectionUI>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(toggleKey)) return;
        if (inventoryUI == null) return;

        if (inventoryUI.IsOpen)
        {
            inventoryUI.Hide();
            return;
        }

        if (augmentSelectionUI != null && augmentSelectionUI.IsPanelOpen) return;
        inventoryUI.Show(sellable: false);
    }
}
