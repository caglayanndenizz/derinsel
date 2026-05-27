using UnityEngine;

[CreateAssetMenu(fileName = "EntityStats", menuName = "Scriptable Objects/EntityStats")]
public class EntityStats : ScriptableObject
{
    public string entityName;
    public float maxHealth;
    public float moveSpeed;
    public float enemyAP;
    public float lightAttackDamage;
    public float hammerLightDamage;
    public float heavyAttackDamage;
}
