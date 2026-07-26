using UnityEngine;

/// <summary>
/// Resting-floor NPC: touching it opens the augment shop panel, leaving closes it.
/// If the player is still inside the trigger when a bought chest's augment is picked,
/// the panel reopens automatically instead of requiring a fresh enter/exit.
/// </summary>
public class AugmentShopNpc : MonoBehaviour
{
    [SerializeField] private AugmentShopPanel shopPanel;

    private Player _playerInside;
    private AugmentSelectionUI _augmentSelectionUI;

    private void Awake()
    {
        _augmentSelectionUI = FindAnyObjectByType<AugmentSelectionUI>();
        if (_augmentSelectionUI != null)
            _augmentSelectionUI.OnChestAugmentSelected += HandleChestAugmentSelected;
    }

    private void OnDestroy()
    {
        if (_augmentSelectionUI != null)
            _augmentSelectionUI.OnChestAugmentSelected -= HandleChestAugmentSelected;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Player player = other.GetComponent<Player>() ?? other.GetComponentInParent<Player>();
        if (player == null || shopPanel == null) return;

        _playerInside = player;
        shopPanel.Show(player);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Player player = other.GetComponent<Player>() ?? other.GetComponentInParent<Player>();
        if (player == null || shopPanel == null) return;

        _playerInside = null;
        shopPanel.Hide();
    }

    private void HandleChestAugmentSelected()
    {
        if (_playerInside == null || shopPanel == null) return;
        shopPanel.Show(_playerInside);
    }
}
