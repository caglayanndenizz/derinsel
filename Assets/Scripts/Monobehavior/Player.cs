using UnityEngine;


public class Player : BaseEntity
{

    public float goldCount = 0;
    public float experienceCount = 0;

    void Update()
    {
        Move();
    }

    protected override void Move()
    {
        float moveX = Input.GetAxis("Horizontal"); 
		    float moveY = Input.GetAxis("Vertical");   

		    // 2D'de yukarı/aşağı hareketi Y eksenidir, Z değil!
		    Vector3 direction = new Vector3(moveX, moveY, 0);

		    transform.Translate(direction * stats.moveSpeed * Time.deltaTime);

    }
}
