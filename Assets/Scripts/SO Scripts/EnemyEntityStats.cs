using UnityEngine;

public enum EnemyTier { Basic, MiniBoss, Boss }

[CreateAssetMenu(fileName = "EnemyEntityStats", menuName = "Scriptable Objects/EnemyEntityStats")]
public class EnemyEntityStats : ScriptableObject
{
    [Header("Identity")]
    public string enemyName;
    public EnemyTier tier = EnemyTier.Basic;

    [Header("Base")]
    public float maxHealth;
    [Tooltip("Seconds to traverse one world unit while patrolling (1 = one unit per second).")]
    public float walkSpeed = 1f;
    [Tooltip("Chase/attack approach speed (world units/sec).")]
    public float runSpeed = 5f;

    [Header("Combat")]
    public float enemyAP;
}
