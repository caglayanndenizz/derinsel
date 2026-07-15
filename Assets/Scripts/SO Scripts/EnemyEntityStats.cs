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
    public float moveSpeed;

    [Header("Combat")]
    public float enemyAP;
}
