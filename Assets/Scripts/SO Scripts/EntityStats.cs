using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "EntityStats", menuName = "Scriptable Objects/EntityStats")]
public class EntityStats : ScriptableObject
{
    [Header("Base")]
    public float maxHealth;
    public float moveSpeed;

    [Header("Bow — Light")]
    [FormerlySerializedAs("bowLightAp")] public float bowLightApMin = 10f;
    public float bowLightApMax = 10f;

    [Header("Bow — Heavy (charged)")]
    [FormerlySerializedAs("bowHeavyAp")] public float bowHeavyApMin = 40f;
    public float bowHeavyApMax = 40f;

    [Header("Hammer — Light")]
    [FormerlySerializedAs("hammerLightDamage")] public float hammerLightDamageMin = 10f;
    public float hammerLightDamageMax = 10f;

    [Header("Hammer — Heavy (fallback when the target has no BaseEntity)")]
    [FormerlySerializedAs("heavyAttackDamage")] public float heavyAttackDamageMin = 40f;
    public float heavyAttackDamageMax = 40f;

    public float RollBowLightAp()        => RollRange(bowLightApMin, bowLightApMax);
    public float RollBowHeavyAp()        => RollRange(bowHeavyApMin, bowHeavyApMax);
    /// <summary>Crossbow is the bow's mutated form — its damage always matches the bow's pre-mutation (light) range.</summary>
    public float RollCrossbowAp()        => RollBowLightAp();
    public float RollHammerLightDamage() => RollRange(hammerLightDamageMin, hammerLightDamageMax);
    public float RollHeavyAttackDamage() => RollRange(heavyAttackDamageMin, heavyAttackDamageMax);

    private static float RollRange(float min, float max)
        => min <= max ? Random.Range(min, max) : Random.Range(max, min);
}
