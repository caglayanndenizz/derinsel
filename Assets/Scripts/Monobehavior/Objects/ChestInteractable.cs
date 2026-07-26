using System.Collections;
using UnityEngine;

public class ChestInteractable : MonoBehaviour
{
    [Tooltip("1 = Wooden (Common), 2 = Silver (Rare), 3 = Gold (Extraordinary)")]
    [SerializeField] private int rarity = 1;
    [SerializeField] private bool isUnlockChest;

    [Header("Animation")]
    [SerializeField] private string idleBoolName = "Idle";
    [SerializeField] private string openBoolName = "Open";

    private Animator _animator;
    private AugmentSelectionUI _augmentUI;
    private bool _opened;
    private bool _suppressRerollOnOpen;

    void Start()
    {
        if (_opened) return;

        _animator = GetComponent<Animator>();
        if (_animator != null)
            _animator.SetBool(idleBoolName, true);

        _augmentUI = Object.FindAnyObjectByType<AugmentSelectionUI>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_opened) return;
        if (other.GetComponent<Player>() == null && other.GetComponentInParent<Player>() == null) return;
        Open();
    }

    /// <summary>Opens immediately without waiting for the player to walk into the trigger — used when the chest is bought from a shop.</summary>
    /// <summary>Opens immediately without waiting for the player to walk into the trigger — used when the chest is bought from a shop. Shop-bought chests never offer a reroll.</summary>
    public void OpenNow()
    {
        if (_opened) return;
        _suppressRerollOnOpen = true;
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_augmentUI == null) _augmentUI = Object.FindAnyObjectByType<AugmentSelectionUI>();
        Open();
    }

    private void Open()
    {
        _opened = true;

        if (_animator != null)
        {
            _animator.SetBool(idleBoolName, false);
            _animator.SetBool(openBoolName, true);
            StartCoroutine(ShowUIAfterAnimation());
        }
        else
        {
            _augmentUI?.ShowChestPanel(rarity, !_suppressRerollOnOpen);
        }
    }

    private IEnumerator ShowUIAfterAnimation()
    {
        yield return null; // Animator'un Open state'e gecmesini bekle

        while (_animator != null)
        {
            AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(openBoolName) && info.normalizedTime >= 1f)
                break;
            yield return null;
        }

        if (isUnlockChest)
            _augmentUI?.TryShowUnlockChestPanel(!_suppressRerollOnOpen);
        else
            _augmentUI?.ShowChestPanel(rarity, !_suppressRerollOnOpen);
    }
}
