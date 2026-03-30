using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class Enemy : BaseEntity
{
    public enum State
    {
        Patrol,
        Chase
    }

     [Header("State Settings")]
    public State currentState = State.Patrol;
    public float detectionRange = 3f; 
    public float expandedDetectionRange = 12f; 
    public float attackRange = 1f;

    [Header("Patrol Settings")]
    public float patrolDistance = 2f;
    private Vector3 startPosition;
    private int patrolDirection = 1;

    [Header("Movement Speed")]
    private float patrolSpeed;
    private float originalDetectionRange;

    [Header("Loot")]

    public GameObject goldPrefab;
    public GameObject experiencePrefab;



    protected GameObject player;

     protected override void Awake()
    {
        base.Awake();
        player = GameObject.FindGameObjectWithTag("Player");
        startPosition = transform.position;
        patrolSpeed = stats.moveSpeed;
        originalDetectionRange = detectionRange;
    }
    void Update()
    {
        CheckState();
        Move();
    }

      private void CheckState()
    {
        // If the player is dead or destroyed, revert to patrol
        if (player == null)
        {
            // Try to find the player again in case it spawned after the enemy
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                currentState = State.Patrol;
                startPosition = transform.position; // Reset patrol center
                detectionRange = originalDetectionRange; // Reset detection range to its original value
                return;
            }
        }

        // Use Vector2 to prevent Z-axis differences from breaking distance logic in 2D
        float distanceToPlayer = Vector2.Distance(transform.position, player.transform.position);

        if (currentState == State.Patrol)
        {
            if (distanceToPlayer <= detectionRange) // Using the current detectionRange (initially originalDetectionRange)
            {
                currentState = State.Chase;
                detectionRange = expandedDetectionRange; // Expand detection range
            }
        }
        else if (currentState == State.Chase)
        {
            // If player leaves the expanded detection range, revert to Patrol
            if (distanceToPlayer > detectionRange)
            {
                currentState = State.Patrol;
                startPosition = transform.position; // Reset patrol center
                detectionRange = originalDetectionRange; // Reset detection range
            }
            // If player is still within the expanded detection range, remain in Chase state.
            // The detectionRange variable already holds the expanded value.
        }
    }

    

    protected override void Move() 
    {
         if (player == null) return;

        float currentSpeed = (currentState == State.Patrol) ? patrolSpeed : stats.moveSpeed * 1.75f;

        if (currentState == State.Patrol)
        {
            
            transform.Translate(Vector3.right * patrolDirection * currentSpeed * Time.deltaTime);

            
            if (Vector2.Distance(startPosition, transform.position) >= patrolDistance)
            {
                patrolDirection *= -1;
            }
        }
        else if (currentState == State.Chase)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.transform.position);

            if (distanceToPlayer > attackRange)
            {
                // Move towards the player
                Vector3 direction = (player.transform.position - transform.position).normalized;
                transform.Translate(direction * currentSpeed * Time.deltaTime, Space.World);
            }
            else
            {
                // oyuncuya yetisildi, attaga baslama vakti. 
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Player playerScript = collision.gameObject.GetComponent<Player>();
        
            if (playerScript != null)
            {
                TakeDamage(playerScript.stats.attackPower);
            }
        }

        //buradaki collision mantigi attack function gelince silinecek.
    }

    protected override void Die() 
    {
    Debug.Log(gameObject.name + " ganimet birakarak öldü.");

    Instantiate(goldPrefab, transform.position, Quaternion.identity);
    Instantiate(experiencePrefab, transform.position + new Vector3(0.5f, 0, 0), Quaternion.identity);

    
    Destroy(gameObject);
    }

    private void OnDrawGizmos()
    {
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
