using UnityEngine;

public class DestructibleObject : MonoBehaviour, IDamageable
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
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
