using System.Collections;
using UnityEngine;

public class BreakableObject : MonoBehaviour, IDamageable, IBreakable
{
    public enum DropType { None, Gold, Healing }

    [Header("Loot")]
    [SerializeField] private DropType dropType = DropType.Gold;
    [Tooltip("Dusecek altin sikke adedi.")]
    [SerializeField] private int goldCoins = 3;
    [Tooltip("DropType Healing secildiginde spawn edilecek prefab.")]
    [SerializeField] private GameObject healingPrefab;

    [Header("Animation")]
    [Tooltip("Animator'daki idle bool parametresinin adi.")]
    [SerializeField] private string idleBoolName = "Idle";
    [Tooltip("Name of the break bool parameter in the Animator AND the state name.")]
    [SerializeField] private string destroyBoolName = "Destroy";

    private Animator _animator;
    private bool _broken;

    void Start()
    {
        _animator = GetComponent<Animator>();
        if (_animator != null)
            _animator.SetBool(idleBoolName, true);
    }

    public void TakeDamage(float amount, bool isHeavy)
    {
        Break();
    }

    public void Break()
    {
        if (_broken) return;
        _broken = true;

        if (_animator != null)
        {
            _animator.SetBool(idleBoolName, false);
            _animator.SetBool(destroyBoolName, true);
            StartCoroutine(WaitForDestroyAnimation());
        }
        else
        {
            Destroy(gameObject);
        }

        SpawnLoot();
    }

    private IEnumerator WaitForDestroyAnimation()
    {
        while (true)
        {
            yield return null;
            AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(destroyBoolName) && info.normalizedTime >= 1f)
            {
                Destroy(gameObject);
                yield break;
            }
        }
    }

    private void SpawnLoot()
    {
        if (dropType == DropType.Gold)
            for (int i = 0; i < goldCoins; i++)
                GoldLootPooler.Instance?.GetGold(transform.position, Quaternion.identity);
        else if (dropType == DropType.Healing && healingPrefab != null)
            Instantiate(healingPrefab, transform.position, Quaternion.identity);
    }
}
