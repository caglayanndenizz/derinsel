using UnityEngine;

public class Lootable : MonoBehaviour, ICollectable
{
    [Header("Basic Settings")]
    public int value = 1;

    bool _isCollected;

    void OnEnable()
    {
        _isCollected = false;
    }

    public void Collect(Player player)
    {
        if (_isCollected) return;
        _isCollected = true;

        if (player != null)
        {
            int gainedGold = Mathf.Max(0, value);
            player.PlayerCurrency?.AddGold(gainedGold);
            KillCounter.Instance?.RegisterLootGold(gainedGold, player.transform.position);
        }

        ReturnToPool();
    }

    public void ReturnToPool()
    {
        if (GoldLootPooler.Instance != null)
        {
            GoldLootPooler.Instance.ReturnGold(gameObject);
            return;
        }

        Destroy(gameObject);
    }
}
