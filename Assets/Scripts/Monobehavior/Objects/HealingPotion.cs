using UnityEngine;

public class HealingPotion : MonoBehaviour
{
    [SerializeField] private float healAmount = 20f;

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!col.CompareTag("Player")) return;
        Player player = col.GetComponentInParent<Player>();
        if (player == null) return;
        player.Heal(healAmount);
        Destroy(gameObject);
    }
}
