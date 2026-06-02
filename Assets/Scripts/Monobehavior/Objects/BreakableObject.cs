using UnityEngine;

public class BreakableObject : MonoBehaviour, IDamageable, IBreakable
{
    public enum DropType { None, Gold, Experience, Both }

    [Header("Loot")]
    [SerializeField] private DropType dropType = DropType.Both;
    [Tooltip("Dusecek altin sikke adedi.")]
    [SerializeField] private int goldCoins = 3;
    [Tooltip("Dusecek deneyim kure adedi.")]
    [SerializeField] private int expOrbs = 2;

    [Header("Animation")]
    [Tooltip("Animator'daki idle bool parametresinin adi.")]
    [SerializeField] private string idleBoolName = "Idle";
    [Tooltip("Animator'daki kirilma bool parametresinin adi VE state adı.")]
    [SerializeField] private string destroyBoolName = "Destroy";

    private Animator _animator;
    private bool _broken;

    void Start()
    {
        _animator = GetComponent<Animator>();
        if (_animator != null)
            _animator.SetBool(idleBoolName, true);
    }

    void Update()
    {
        if (!_broken || _animator == null) return;

        AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
        if (info.IsName(destroyBoolName) && info.normalizedTime >= 1f)
            Destroy(gameObject);
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
        }

        SpawnLoot();
    }

    private void SpawnLoot()
    {
        if (dropType == DropType.Gold || dropType == DropType.Both)
            for (int i = 0; i < goldCoins; i++)
                GoldLootPooler.Instance?.GetGold(transform.position, Quaternion.identity);

        if (dropType == DropType.Experience || dropType == DropType.Both)
            for (int i = 0; i < expOrbs; i++)
                ExperienceLootPooler.Instance?.GetExperience(transform.position, Quaternion.identity);
    }
}
