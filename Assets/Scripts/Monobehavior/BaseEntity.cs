using UnityEngine;

public abstract class BaseEntity : MonoBehaviour , IDamageable
{
    protected float _currentHealth;
    public float CurrentHealth => _currentHealth;
    public virtual float MaxHealth => 0f;

    protected virtual void Awake()
    {
        _currentHealth = MaxHealth;
    }

    public virtual void TakeDamage(float amount, bool isHeavy)
    {
        _currentHealth -= amount;
        if (_currentHealth <= 0) Die();
    }

    protected virtual void Die() { }

    protected abstract void Move();
}
