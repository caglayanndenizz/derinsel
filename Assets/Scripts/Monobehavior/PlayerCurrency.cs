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

    public void NotifyGoldChanged()
    {
        GoldChanged?.Invoke(Mathf.Max(0f, goldCount));
    }
}
