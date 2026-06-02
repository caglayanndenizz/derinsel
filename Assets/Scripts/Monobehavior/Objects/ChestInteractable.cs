using UnityEngine;

public class ChestInteractable : MonoBehaviour
{
    [Tooltip("1 = Wooden (Common), 2 = Silver (Rare), 3 = Gold (Extraordinary)")]
    [SerializeField] private int rarity = 1;

    private Animator _animator;
    private bool _opened;

    void Start()
    {
        _animator = GetComponent<Animator>();
        if (_animator != null)
            _animator.SetBool("Idle", true);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_opened) return;
        if (other.GetComponent<Player>() == null && other.GetComponentInParent<Player>() == null) return;
        Open();
    }

    private void Open()
    {
        _opened = true;

        if (_animator != null)
        {
            _animator.SetBool("Idle", false);
            _animator.SetBool("Open", true);
        }

        AugmentSelectionUI ui = Object.FindAnyObjectByType<AugmentSelectionUI>();
        ui?.ShowChestPanel(rarity);
    }
}
