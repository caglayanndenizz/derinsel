using UnityEngine;

public class DestructibleObject : MonoBehaviour, IDamageable
{
    public float health = 10f;

    public void TakeDamage(float amount, bool isHeavy)
    {
        health -= amount;

        if (health <= 0)
            Destroy(gameObject);
    }
}
