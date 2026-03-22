using UnityEngine;

public abstract class BaseEntity : MonoBehaviour 
{
    [Header("Data Reference")]
    public EntityStats stats; // Yarattığımız SO'yu buraya bağlayacağız

    protected float currentHealth;

    protected virtual void Awake() 
    {
        // Oyun başladığında canı SO'daki veriden çekiyoruz
        if (stats != null)
            currentHealth = stats.maxHealth;
    }

    public virtual void TakeDamage(float amount) 
    {
        currentHealth -= amount;
        Debug.Log(gameObject.name + " hasar aldi! Kalan can: " + currentHealth);

        if (currentHealth <= 0) Die();
    }

    protected virtual void Die() 
    {
        Debug.Log(gameObject.name + " yok edildi.");
        // Ölüm efektleri buraya gelecek
    }

    // Hareket her varlıkta farklı olacağı için gövdesini boş bırakıyoruz
    protected abstract void Move(); 
}