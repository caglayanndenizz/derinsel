using System;
using UnityEngine;

public class PlayerCurrency : MonoBehaviour
{
    [SerializeField] private float goldCount;

    public event Action<float> GoldChanged;

    public float GoldCount => Mathf.Max(0f, goldCount);

    public void AddGold(float amount)
    {
        if (amount <= 0f) return;
        goldCount += amount;
        NotifyGoldChanged();
    }

    /// <summary>Deducts gold only if the player can afford it. Returns false (no-op) if not.</summary>
    public bool TrySpendGold(float amount)
    {
        if (amount <= 0f) return true;
        if (goldCount < amount) return false;

        goldCount -= amount;
        NotifyGoldChanged();
        return true;
    }

    public void NotifyGoldChanged()
    {
        GoldChanged?.Invoke(Mathf.Max(0f, goldCount));
    }
}
