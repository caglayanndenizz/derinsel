using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Belirli bir kat aralığı için wave sırasını tanımlar.
/// RoomWaveController, mevcut kata en yakın eşleşen config'i kullanır.
/// </summary>
[CreateAssetMenu(fileName = "FloorWaveConfig", menuName = "Scriptable Objects/Floor Wave Config")]
public class FloorWaveConfig : ScriptableObject
{
    [Tooltip("Bu config'in geçerli olduğu ilk kat (dahil).")]
    public int fromFloor = 1;

    [Tooltip("Bu config'in geçerli olduğu son kat (dahil). -1 = sınır yok.")]
    public int toFloor = -1;

    [Tooltip("Bu kat aralığında sırayla oynatılacak dalgalar.")]
    public List<WaveDefinition> waves = new();
}
