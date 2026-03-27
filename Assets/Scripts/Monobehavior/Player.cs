using UnityEngine;


public class Player : BaseEntity
{

    public float goldCount = 0;
    public float experienceCount = 0;

    public Animator animator;

    protected override void Awake()
    {
        base.Awake();
        animator = GetComponent<Animator>();
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

        UpdateAnimations(moveX, moveY);

    }

    private void UpdateAnimations(float x, float y)
    {
        animator.SetBool("Moveleft", x < 0);
        animator.SetBool("MoveRight", x > 0);
        animator.SetBool("MoveUp", y > 0);
        animator.SetBool("MoveDown", x >= 0 && y < 0);
        animator.SetBool("MoveDown1", x <= 0 && y < 0);


        //buraya movedown adinda bir parametre ekleyip y<0 yaz.
        //sprite hala otomatik saga donuyor sola giderken de. Onu da yapmayi unutma. 
        //Yon tusunu birakmama ragmen hala yurume animasyonu gerceklestiriyor 
        
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
