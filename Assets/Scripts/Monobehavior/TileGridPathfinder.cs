using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A* on a 4-neighbor grid. Walkable = floor tile present and no wall tile at the same cell.
/// </summary>
public static class TileGridPathfinder
{
    private static readonly Vector3Int[] s_Neighbors =
    {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0)
    };

    public static bool IsWalkable(Tilemap floor, Tilemap wall, Vector3Int cell)
    {
        if (floor == null || !floor.HasTile(cell)) return false;
        if (wall != null && wall.HasTile(cell)) return false;
        return true;
    }

    public static Vector3Int WorldToCell(Tilemap floor, Vector2 world) =>
        floor != null ? floor.WorldToCell(world) : Vector3Int.zero;

    public static Vector3Int? FindNearestWalkable(Tilemap floor, Tilemap wall, Vector3Int from, int searchRadius = 3)
    {
        if (IsWalkable(floor, wall, from)) return from;
        Vector3Int? best = null;
        int bestSq = int.MaxValue;
        for (int dx = -searchRadius; dx <= searchRadius; dx++)
        {
            for (int dy = -searchRadius; dy <= searchRadius; dy++)
            {
                var c = from + new Vector3Int(dx, dy, 0);
                if (!IsWalkable(floor, wall, c)) continue;
                int sq = dx * dx + dy * dy;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = c;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// Fills <paramref name="outWorldWaypoints"/> with cell center positions from start to goal (inclusive).
    /// </summary>
    public static bool TryFindPath(
        Tilemap floor,
        Tilemap wall,
        Vector3Int start,
        Vector3Int goal,
        int maxExpansions,
        List<Vector2> outWorldWaypoints)
    {
        outWorldWaypoints.Clear();
        if (floor == null) return false;
        if (!IsWalkable(floor, wall, start) || !IsWalkable(floor, wall, goal)) return false;

        float Heuristic(Vector3Int a) =>
            Mathf.Abs(a.x - goal.x) + Mathf.Abs(a.y - goal.y);

        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var gScore = new Dictionary<Vector3Int, float>();
        var open = new List<Vector3Int>();
        var closed = new HashSet<Vector3Int>();

        gScore[start] = 0f;
        open.Add(start);

        int expansions = 0;
        while (open.Count > 0 && expansions < maxExpansions)
        {
            int bestIdx = 0;
            float bestF = float.MaxValue;
            for (int i = 0; i < open.Count; i++)
            {
                var c = open[i];
                float f = gScore[c] + Heuristic(c);
                if (f < bestF)
                {
                    bestF = f;
                    bestIdx = i;
                }
            }

            Vector3Int current = open[bestIdx];
            open.RemoveAt(bestIdx);
            expansions++;

            if (current == goal)
            {
                ReconstructPath(cameFrom, start, goal, floor, outWorldWaypoints);
                return true;
            }

            closed.Add(current);

            for (int n = 0; n < s_Neighbors.Length; n++)
            {
                Vector3Int nb = current + s_Neighbors[n];
                if (closed.Contains(nb)) continue;
                if (!IsWalkable(floor, wall, nb)) continue;

                float tentativeG = gScore[current] + 1f;
                if (!gScore.TryGetValue(nb, out float oldG) || tentativeG < oldG)
                {
                    cameFrom[nb] = current;
                    gScore[nb] = tentativeG;
                    if (!open.Contains(nb))
                        open.Add(nb);
                }
            }
        }

        return false;
    }

    private static void ReconstructPath(
        Dictionary<Vector3Int, Vector3Int> cameFrom,
        Vector3Int start,
        Vector3Int goal,
        Tilemap floor,
        List<Vector2> outWorldWaypoints)
    {
        var cells = new List<Vector3Int>();
        Vector3Int cur = goal;
        while (true)
        {
            cells.Add(cur);
            if (cur == start) break;
            if (!cameFrom.TryGetValue(cur, out cur)) return;
        }

        cells.Reverse();
        foreach (var cell in cells)
            outWorldWaypoints.Add(floor.GetCellCenterWorld(cell));
    }
}
