using UnityEngine;


public class Player : BaseEntity
{

    public float goldCount = 0;
    public float experienceCount = 0;


    protected override void Awake()
    {
        base.Awake();
        
    }

    void Update()
    {
        Move();
    }

    protected override void Move()
    {
        float moveX = Input.GetAxis("Horizontal"); 
		float moveY = Input.GetAxis("Vertical");   

		Vector3 direction = new Vector3(moveX, moveY, 0).normalized;

		transform.Translate(direction * stats.moveSpeed * Time.deltaTime);


    }


    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            Enemy enemyScript = collision.gameObject.GetComponent<Enemy>();
        
            if (enemyScript != null)
            {
                TakeDamage(enemyScript.stats.attackPower);
            }
        }
    }

    protected override void Die()
    {
        Destroy(gameObject);
    }

}
