using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct WaveEntry
{
    public Enemy.EnemyType enemyType;
    [Min(1)] public int count;
}

/// <summary>
/// Defines the contents of a single wave: enemy types, counts, and spawn interval.
/// </summary>
[CreateAssetMenu(fileName = "WaveDefinition", menuName = "Scriptable Objects/Wave Definition")]
public class WaveDefinition : ScriptableObject
{
    [Tooltip("Enemy types and counts to spawn in this wave.")]
    public List<WaveEntry> entries = new();

    [Tooltip("Wait time between enemy spawns (seconds).")]
    public float spawnDelayBetweenEnemies = 0.3f;

    [Tooltip("EXP multiplier awarded when the wave is cleared (1 = normal).")]
    public float expMultiplier = 1f;

    [Tooltip("Gold multiplier awarded when the wave is cleared (1 = normal).")]
    public float goldMultiplier = 1f;
}
