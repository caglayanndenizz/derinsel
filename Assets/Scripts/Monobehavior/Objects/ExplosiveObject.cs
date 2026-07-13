using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class ExplosiveObject : MonoBehaviour, IDamageable
{
    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 4f;

    [Header("Screen Shake")]
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [Tooltip("Force magnitude passed to GenerateImpulse. Normal heavy = 1, recommended 3-5 for explosions.")]
    [SerializeField] private float impulseForce = 4f;

    [Header("Hit Stop")]
    [Tooltip("Time scale during explosion hit stop. Normal heavy = 0.08. Lower = harder feel.")]
    [SerializeField] private float hitStopTimeScale = 0.02f;
    [Tooltip("Freeze duration during explosion hit stop (seconds). Normal heavy = 0.045.")]
    [SerializeField] private float hitStopDuration = 0.12f;

    [Header("Animation")]
    [SerializeField] private string idleBoolName = "Idle";
    [SerializeField] private string explodeBoolName = "Destroy";

    private Animator _animator;
    private bool _exploded;

    void Start()
    {
        _animator = GetComponent<Animator>();
        if (_animator != null)
            _animator.SetBool(idleBoolName, true);
    }

    public void TakeDamage(float amount, bool isHeavy)
    {
        Explode();
    }

    private void Explode()
    {
        if (_exploded) return;
        _exploded = true;

        if (_animator != null)
        {
            _animator.SetBool(idleBoolName, false);
            _animator.SetBool(explodeBoolName, true);
            StartCoroutine(WaitForDestroyAnimation());
        }
        else
        {
            Destroy(gameObject);
        }

        TriggerExplosionFeedback();
        ApplyExplosionDamage();
    }

    private IEnumerator WaitForDestroyAnimation()
    {
        while (true)
        {
            yield return null;
            AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(explodeBoolName) && info.normalizedTime >= 1f)
            {
                Destroy(gameObject);
                yield break;
            }
        }
    }

    private void TriggerExplosionFeedback()
    {
        if (impulseSource != null)
            impulseSource.GenerateImpulse(impulseForce);

        if (HitStopManager.Instance != null)
            HitStopManager.Instance.TriggerHitStop(hitStopTimeScale, hitStopDuration);
    }

    private void ApplyExplosionDamage()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        HashSet<int> processed = new HashSet<int>();

        foreach (Collider2D col in hits)
        {
            Player player = col.GetComponentInParent<Player>();
            if (player != null)
            {
                if (processed.Add(player.gameObject.GetInstanceID()))
                    player.TakeDamage(player.MaxHealth * 0.5f, true);
                continue;
            }

            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                if (!enemy.IsDead && processed.Add(enemy.gameObject.GetInstanceID()))
                    enemy.TakeDamage(enemy.MaxHealth, true);
                continue;
            }

            IBreakable breakable = col.GetComponentInParent<IBreakable>();
            if (breakable != null)
            {
                MonoBehaviour mb = breakable as MonoBehaviour;
                if (mb != null && processed.Add(mb.gameObject.GetInstanceID()))
                    breakable.Break();
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, explosionRadius);
        Gizmos.color = new Color(1f, 0.4f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
