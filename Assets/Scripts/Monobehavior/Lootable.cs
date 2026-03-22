using UnityEngine;

public class Lootable : MonoBehaviour
{
    public float lifetime = 0.4f;
    public int value = 1;
    public bool isGold = true;
    private bool isCollected = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;

        if (other.CompareTag("Player"))
        {
           
            isCollected = true;

            Player player = other.GetComponent<Player>();
            if (player != null)
            {
                if (isGold) player.goldCount += value;
                else player.experienceCount += value;
                
                Debug.Log(gameObject.name + " toplandi");
            }

            
            Destroy(gameObject, lifetime);
            //lifetime suresince bir animasyon eklenebilir belki. Vampire survivorsdaki gibi player a gelmeden once hareketlenmesi gibi bir sey
        }
    }
}