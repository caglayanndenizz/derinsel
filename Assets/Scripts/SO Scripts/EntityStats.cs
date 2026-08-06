using UnityEngine;

[CreateAssetMenu(fileName = "EntityStats", menuName = "Scriptable Objects/EntityStats")]
public class EntityStats : ScriptableObject
{
    [Header("Base")]
    public float maxHealth;
    public float moveSpeed;

    [Header("Player Base Damage")]
    public float playerBaseDamageMin = 2f;
    public float playerBaseDamageMax = 5f;

    public float RollPlayerBaseDamage() => RollRange(playerBaseDamageMin, playerBaseDamageMax);

    private static float RollRange(float min, float max)
        => min <= max ? Random.Range(min, max) : Random.Range(max, min);
}
