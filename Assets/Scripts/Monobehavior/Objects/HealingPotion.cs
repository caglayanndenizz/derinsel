using UnityEngine;

public class HealingPotion : MonoBehaviour, ICollectable
{
    [SerializeField] private float healAmount = 20f;

    public void Collect(Player player)
    {
        if (player == null) return;
        player.Heal(healAmount);
        Destroy(gameObject);
    }
}
