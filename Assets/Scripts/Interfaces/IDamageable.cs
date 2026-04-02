
public interface IDamageable
{
    // Bu arayüzü kullanan her şey bu fonksiyonu içermek ZORUNDA.
    void TakeDamage(float amount, bool isHeavy);
}