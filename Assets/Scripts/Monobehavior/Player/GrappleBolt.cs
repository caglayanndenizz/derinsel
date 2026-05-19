using UnityEngine;

public class GrappleBolt : MonoBehaviour
{
    [SerializeField] float speed       = 22f;
    [SerializeField] float maxLifetime = 3f;

    [Header("Rope Visual")]
    [SerializeField] Material ropeMaterial;
    [SerializeField] float    ropeWidth = 0.05f;
    [SerializeField] Color    ropeColor = Color.white;

    Vector2        _direction;
    float          _spawnTime;
    bool           _initialized;
    bool           _anchored;
    Transform      _ownerTransform;
    IPlayerContext _playerContext;
    LineRenderer   _lineRenderer;
    LayerMask      _enemyMask;
    Vector2        _previousFramePosition;

    public void Initialize(Vector2 targetWorldPos, Transform ownerTransform, IPlayerContext context, LayerMask enemyMask)
    {
        _ownerTransform = ownerTransform;
        _playerContext  = context;
        _enemyMask      = enemyMask;
        _anchored       = false;

        Vector2 origin = transform.position;
        Vector2 to     = targetWorldPos - origin;
        if (to.sqrMagnitude < 0.0001f) { Destroy(gameObject); return; }

        _direction = to.normalized;
        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        _spawnTime             = Time.time;
        _previousFramePosition = transform.position;
        _initialized           = true;

        SetupLineRenderer();
    }

    void SetupLineRenderer()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer == null)
            _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.positionCount = 2;
        _lineRenderer.startWidth    = ropeWidth;
        _lineRenderer.endWidth      = ropeWidth;
        _lineRenderer.startColor    = ropeColor;
        _lineRenderer.endColor      = ropeColor;
        _lineRenderer.useWorldSpace = true;
        if (ropeMaterial != null) _lineRenderer.material = ropeMaterial;
    }

    void Update()
    {
        if (!_initialized) return;

        if (_anchored)
        {
            UpdateRope();
            return;
        }

        if (Time.time - _spawnTime >= maxLifetime) { Destroy(gameObject); return; }

        Vector2 prev = _previousFramePosition;
        Vector2 next = (Vector2)transform.position + _direction * speed * Time.deltaTime;

        RaycastHit2D[] hits = Physics2D.LinecastAll(prev, next);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit2D h in hits)
        {
            if (h.collider == null) continue;
            if (IsOwnCollider(h.collider)) continue;
            if (h.collider.GetComponentInParent<Player>() != null) continue;
            if (h.collider.GetComponentInParent<GrappleBolt>() != null) continue;
            if (h.collider.isTrigger) continue;

            bool isEnemy = ((1 << h.collider.gameObject.layer) & _enemyMask) != 0;
            if (isEnemy) { Destroy(gameObject); return; }

            Anchor(h.point);
            return;
        }

        _previousFramePosition = (Vector2)transform.position;
        transform.position     = new Vector3(next.x, next.y, transform.position.z);
        UpdateRope();
    }

    void Anchor(Vector2 anchorPoint)
    {
        _anchored = true;
        transform.position = new Vector3(anchorPoint.x, anchorPoint.y, transform.position.z);

        float ropeLength = Vector2.Distance(anchorPoint, _ownerTransform.position);
        _playerContext.EnterGrappleSwing(anchorPoint, ropeLength, this);
        UpdateRope();
    }

    void UpdateRope()
    {
        if (_lineRenderer == null || _ownerTransform == null) return;
        _lineRenderer.SetPosition(0, transform.position);
        _lineRenderer.SetPosition(1, _ownerTransform.position);
    }

    bool IsOwnCollider(Collider2D c) =>
        c != null && (c.transform == transform || c.transform.IsChildOf(transform));

    public void Detach()
    {
        Destroy(gameObject);
    }
}
