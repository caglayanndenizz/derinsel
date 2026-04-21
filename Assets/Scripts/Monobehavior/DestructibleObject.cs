using UnityEngine;

public class DestructibleObject : MonoBehaviour, IDamageable
{
    public float health = 10f;

    public void TakeDamage(float amount, bool isHeavy)
    {
        health -= amount;
        Debug.Log(gameObject.name + " darbe aldı!");

        if (health <= 0)
        {
            Debug.Log("nesne kirildi!");
            Destroy(gameObject);
        }
    }
}
