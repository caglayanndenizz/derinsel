using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct WaveEntry
{
    public Enemy.EnemyType enemyType;
    [Min(1)] public int count;
}

/// <summary>
/// Tek bir wave'in içeriğini tanımlar: hangi tipte kaç düşman, ne kadar aralıkla spawn edilir.
/// </summary>
[CreateAssetMenu(fileName = "WaveDefinition", menuName = "Scriptable Objects/Wave Definition")]
public class WaveDefinition : ScriptableObject
{
    [Tooltip("Bu wave'de spawn edilecek düşman tipleri ve sayıları.")]
    public List<WaveEntry> entries = new();

    [Tooltip("İki enemy spawn arasındaki bekleme süresi (saniye).")]
    public float spawnDelayBetweenEnemies = 0.3f;

    [Tooltip("Wave temizlenince verilen EXP çarpanı (1 = normal).")]
    public float expMultiplier = 1f;

    [Tooltip("Wave temizlenince verilen Gold çarpanı (1 = normal).")]
    public float goldMultiplier = 1f;
}
