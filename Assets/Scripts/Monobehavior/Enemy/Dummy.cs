using UnityEngine;

/// <summary>
/// Stationary damage-testing target: reacts to hits like a real enemy would
/// (correct per-hit damage rolls, floating damage numbers) but never loses
/// health, never dies, never moves, and has no attack/AI behaviour of its own.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Dummy : MonoBehaviour, IDamageable
{
    public void TakeDamage(float amount, bool isHeavy)
    {
        DamageNumberPooler.SpawnDamageNumber(transform.position, amount, isHeavy);
    }
}
