using System;
using UnityEngine;

public abstract class BaseEntity : MonoBehaviour , IDamageable
{
    [Header("Data Reference")]
    public EntityStats stats; // Yarattığımız SO'yu buraya bağlayacağız

    protected float _currentHealth;

    public static Action<BaseEntity> OnAnyEntityDie { get; internal set; }

    protected virtual void Awake() 
    {
        // Oyun başladığında canı SO'daki veriden çekiyoruz
        if (stats != null)
            _currentHealth = stats.maxHealth;
    }

    public virtual void TakeDamage(float amount, bool isHeavy)
    {
        _currentHealth -= amount;
        Debug.Log(gameObject.name + " hasar aldi! Kalan can: " + _currentHealth);

        if (_currentHealth <= 0) Die();
    }

    protected virtual void Die() 
    {
        Debug.Log(gameObject.name + " yok edildi.");
        // Ölüm efektleri buraya gelecek
    }

    // Hareket her varlıkta farklı olacağı için gövdesini boş bırakıyoruz
    protected abstract void Move();

}