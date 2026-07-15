using UnityEngine;

/// <summary>
/// Spatial queries for an entity: navigation blocking probes, obstacle avoidance,
/// line of sight and last-safe-position tracking. Config values stay serialized on
/// the owner and are passed in via Configure. Reusable by future bosses.
/// </summary>
public class EntitySensor : MonoBehaviour
{
    private LayerMask _blockingEnvironmentMask = Physics2D.DefaultRaycastLayers;
    private float _sensorLength = 1.5f;

    private Rigidbody2D _rb;
    private Vector2 _lastSafeWorldPosition;

    public Vector2 LastSafeWorldPosition => _lastSafeWorldPosition;

    private static readonly LosComparer _losComparer = new LosComparer();
    private class LosComparer : System.Collections.Generic.IComparer<RaycastHit2D>
    {
        public int Compare(RaycastHit2D a, RaycastHit2D b) => a.distance.CompareTo(b.distance);
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    public void Configure(LayerMask blockingEnvironmentMask, float sensorLength)
    {
        _blockingEnvironmentMask = blockingEnvironmentMask;
        _sensorLength = sensorLength;
    }

    public void SetLastSafePosition(Vector2 pos)
    {
        _lastSafeWorldPosition = pos;
    }

    /// <summary>Updates the last safe position if the current one is not blocked.</summary>
    public void TrackLastSafePosition(Vector2 currentPos)
    {
        if (!IsNavigationBlockedAt(currentPos))
            _lastSafeWorldPosition = currentPos;
    }

    public bool IsNavigationBlockedAt(Vector2 worldPos)
    {
        const float probeRadius = 0.12f;
        Collider2D hit = Physics2D.OverlapCircle(worldPos, probeRadius, _blockingEnvironmentMask);
        if (hit == null) return false;
        if (hit.isTrigger) return false;
        if (_rb != null && hit.attachedRigidbody == _rb) return false;
        if (hit.gameObject == gameObject || hit.transform.IsChildOf(transform)) return false;
        if (hit.CompareTag("Player")) return false;
        if (hit.GetComponentInParent<Enemy>() != null) return false;
        return true;
    }

    public Vector2 GetAvoidanceDirection(Vector2 currentDir)
    {
        Vector2 origin = _rb != null ? _rb.position : (Vector2)transform.position;
        if (IsNavigationBlockedAt(origin + currentDir * _sensorLength))
        {
            Vector2 leftDir = RotateVector(currentDir, 45f);
            Vector2 rightDir = RotateVector(currentDir, -45f);

            if (!IsNavigationBlockedAt(origin + leftDir * _sensorLength)) return leftDir;
            if (!IsNavigationBlockedAt(origin + rightDir * _sensorLength)) return rightDir;
            return RotateVector(currentDir, 90f);
        }
        return currentDir;
    }

    private static Vector2 RotateVector(Vector2 v, float deg)
    {
        float s = Mathf.Sin(deg * Mathf.Deg2Rad);
        float c = Mathf.Cos(deg * Mathf.Deg2Rad);
        return new Vector2((c * v.x) - (s * v.y), (s * v.x) + (c * v.y));
    }

    /// <summary>
    /// True if nothing on the blocking mask stands between start and the target.
    /// Colliders belonging to this entity or to other enemies are ignored;
    /// a collider tagged targetTag counts as reaching the target.
    /// </summary>
    public bool HasLineOfSight(Vector2 start, Vector2 targetWorld, float maxDistance, string targetTag = "Player")
    {
        Vector2 toTarget = targetWorld - start;
        float fullDist = toTarget.magnitude;
        if (fullDist < 0.0001f) return true;

        Vector2 dir = toTarget / fullDist;
        if (fullDist > maxDistance + 0.001f) return false;

        float checkDist = Mathf.Min(fullDist, maxDistance);
        Vector2 end = start + dir * checkDist;

        for (float i = 0.25f; i < checkDist; i += 0.5f)
        {
            if (IsNavigationBlockedAt(start + dir * i)) return false;
        }

        RaycastHit2D[] hits = Physics2D.LinecastAll(start, end, _blockingEnvironmentMask);
        System.Array.Sort(hits, 0, hits.Length, _losComparer);
        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.isTrigger) continue;
            if (_rb != null && hit.collider.attachedRigidbody == _rb) continue;
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.GetComponentInParent<Enemy>() != null) continue;
            if (hit.collider.CompareTag(targetTag)) return true;
            return false;
        }

        return true;
    }
}
