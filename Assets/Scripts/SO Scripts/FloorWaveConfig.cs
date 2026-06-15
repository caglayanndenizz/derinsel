using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the wave sequence for a specific floor range.
/// RoomWaveController uses the closest matching config to the current floor.
/// </summary>
[CreateAssetMenu(fileName = "FloorWaveConfig", menuName = "Scriptable Objects/Floor Wave Config")]
public class FloorWaveConfig : ScriptableObject
{
    [Tooltip("First floor this config applies to (inclusive).")]
    public int fromFloor = 1;

    [Tooltip("Last floor this config applies to (inclusive). -1 = no upper limit.")]
    public int toFloor = -1;

    [Tooltip("Waves played in order within this floor range.")]
    public List<WaveDefinition> waves = new();
}
