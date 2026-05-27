using UnityEngine;

[CreateAssetMenu(fileName = "EntityStats", menuName = "Scriptable Objects/EntityStats")]
public class EntityStats : ScriptableObject
{
    [Header("Base")]
    public float maxHealth;
    public float moveSpeed;

    [Header("Bow")]
    public float bowLightAp;
    public float bowHeavyAp;

    [Header("Crossbow")]
    public float crossbowAp;

    [Header("Hammer")]
    public float hammerLightDamage;
    public float heavyAttackDamage;
}
