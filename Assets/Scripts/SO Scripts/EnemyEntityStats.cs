using UnityEngine;

[CreateAssetMenu(fileName = "EnemyEntityStats", menuName = "Scriptable Objects/EnemyEntityStats")]
public class EnemyEntityStats : ScriptableObject
{
    [Header("Identity")]
    public string enemyName;

    [Header("Base")]
    public float maxHealth;
    public float moveSpeed;

    [Header("Combat")]
    public float enemyAP;
}
