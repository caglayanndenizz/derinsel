
public interface IDamageable
{
    // Every class implementing this interface MUST include this method.
    void TakeDamage(float amount, bool isHeavy);
}