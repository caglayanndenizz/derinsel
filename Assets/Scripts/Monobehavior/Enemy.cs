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
    public float detectionRange = 3f; // This will be the initial detection range
    public float expandedDetectionRange = 12f; // The range it expands to
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
            currentState = State.Patrol;
            startPosition = transform.position; // Reset patrol center
            detectionRange = originalDetectionRange; // Reset detection range to its original value
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

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
            // Move horizontally back and forth
            transform.Translate(Vector3.right * patrolDirection * currentSpeed * Time.deltaTime);

            // Reverse direction if we moved too far from the start positions
            if (Vector3.Distance(startPosition, transform.position) >= patrolDistance)
            {
                patrolDirection *= -1;
            }
        }
        else if (currentState == State.Chase)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

            if (distanceToPlayer > attackRange)
            {
                // Move towards the player
                Vector3 direction = (player.transform.position - transform.position).normalized;
                transform.Translate(direction * currentSpeed * Time.deltaTime);
            }
            else
            {
                // Reached the player, start attacking
                Debug.Log(gameObject.name + " is attacking the player!");
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
{
    // Eğer bana çarpan şeyin etiketi "Player" ise
    if (collision.gameObject.CompareTag("Player"))
    {
        // Kendimi yok et
        Destroy(gameObject);
        if(player != null)
            {
                Instantiate(goldPrefab, transform.position, Quaternion.identity);
                Instantiate(experiencePrefab, transform.position + new Vector3(0.5f ,0,0), Quaternion.identity);

            }
    }
}

    private void OnDrawGizmos()
    {
        // Draw the current detection range in yellow (will be 3f or 15f)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Draw the attack range in red
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
