using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public partial class Player
{
    [Header("Dash Settings")]
    [Tooltip("Dash mesafesi (birim). Duvara çarparsa öncesinde durur.")]
    public float dashDistance = 3f;
    public float dashCooldown = 10f;
    [SerializeField] private float dashAlphaFlashDuration = 0.1f;
    [Tooltip("Boşsa root/child'dan otomatik aranır.")]
    [SerializeField] private SpriteRenderer dashFlashTarget;

    [Header("Dash Cooldown UI")]
    public Slider dashCooldownBar;
    public GameObject dashCooldownCanvas;
    [SerializeField] private bool showDashCooldownBarWhenReady = true;

    private float _nextDashTime = 0f;

    // ─── Dash ────────────────────────────────────────────────────────────────

    private void HandleDash()
    {
        if (!Input.GetKeyDown(KeyCode.Space)) return;
        if (Time.time < _nextDashTime) return;

        Vector2 dashDir = GetDashDirection();
        if (dashDir.sqrMagnitude < 0.0001f) return;

        Vector2 from = _rb.position;
        float closestBlockingDist = dashDistance;

        RaycastHit2D[] hits = Physics2D.RaycastAll(from, dashDir, closestBlockingDist);
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D h = hits[i];
            if (h.collider == null || h.collider.isTrigger) continue;
            if (h.collider.GetComponentInParent<Player>() != null) continue;
            if (h.collider.GetComponentInParent<Enemy>() != null) continue;
            if (h.distance < closestBlockingDist)
                closestBlockingDist = h.distance;
        }

        float actualDistance = Mathf.Max(0f, closestBlockingDist - 0.3f);
        _rb.position = from + dashDir * actualDistance;
        _nextDashTime = Time.time + Mathf.Max(0f, dashCooldown);
        StartCoroutine(DashAlphaFlash());
    }

    private Vector2 GetDashDirection()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        Vector2 dir = new Vector2(moveX, moveY).normalized;
        if (dir.sqrMagnitude > 0.0001f) return dir;
        return transform.localScale.x >= 0f ? Vector2.right : Vector2.left;
    }

    private IEnumerator DashAlphaFlash()
    {
        if (dashFlashTarget == null) yield break;
        Color original = dashFlashTarget.color;
        Color invisible = original;
        invisible.a = 0f;
        dashFlashTarget.color = invisible;
        yield return new WaitForSeconds(Mathf.Max(0.01f, dashAlphaFlashDuration));
        dashFlashTarget.color = original;
    }

    // ─── UI ──────────────────────────────────────────────────────────────────

    private void InitializeDashCooldownUI()
    {
        if (dashCooldownBar == null) return;
        dashCooldownBar.minValue = 0f;
        dashCooldownBar.maxValue = 1f;
        dashCooldownBar.value = 1f;
        if (dashCooldownCanvas != null)
            dashCooldownCanvas.SetActive(showDashCooldownBarWhenReady);
    }

    private void UpdateDashCooldownUI()
    {
        if (dashCooldownBar == null) return;
        float cooldown = Mathf.Max(0f, dashCooldown);
        if (cooldown <= 0f)
        {
            dashCooldownBar.value = 1f;
            if (dashCooldownCanvas != null) dashCooldownCanvas.SetActive(showDashCooldownBarWhenReady);
            return;
        }
        float remaining = Mathf.Max(0f, _nextDashTime - Time.time);
        dashCooldownBar.value = Mathf.Clamp01(1f - (remaining / cooldown));
        if (dashCooldownCanvas != null)
        {
            bool isReady = remaining <= 0f;
            dashCooldownCanvas.SetActive(!isReady || showDashCooldownBarWhenReady);
        }
    }
}
