using UnityEngine;

public abstract class BaseEntity : MonoBehaviour , IDamageable
{
    [Header("Data Reference")]
    public EntityStats stats; // Yarattığımız SO'yu buraya bağlayacağız

    protected float _currentHealth;
    public float CurrentHealth => _currentHealth;
    public virtual float MaxHealth => stats != null ? stats.maxHealth : 0f;

    protected virtual void Awake()
    {
        if (stats != null)
            _currentHealth = stats.maxHealth;
    }

    public virtual void TakeDamage(float amount, bool isHeavy)
    {
        _currentHealth -= amount;
        if (_currentHealth <= 0) Die();
    }

    protected virtual void Die() { }

    protected abstract void Move();


}