using UnityEngine;

public enum EnemyType
{
    Swarm,
    Tanky,
    Mixed
}

[CreateAssetMenu(fileName = "RoomData", menuName = "Scriptable Objects/RoomData")]


public class RoomData : ScriptableObject
{
    public string roomName;
    public EnemyType type;
    public int minEnemyCount;
    public int maxEnemyCount;
    public GameObject[] enemyPrefabs;

}
