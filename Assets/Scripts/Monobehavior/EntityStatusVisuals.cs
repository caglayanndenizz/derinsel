using UnityEngine;
using System.Collections;

/// <summary>
/// Hit flash and status effect coloring for the SpriteRenderer on this GameObject
/// or its children (art-pack enemies keep the sprite on a "Body" child).
/// Listens to EntityStatusEffects.StatusChanged; the owning entity only calls PlayHitFlash().
/// </summary>
public class EntityStatusVisuals : MonoBehaviour
{
    [Header("Visual Effects")]
    public Color flashColor = Color.red;

    private static readonly Color FreezeColor = new Color(0.3f, 0.6f, 1f);
    private static readonly Color FireColor   = new Color(1f, 0.45f, 0.1f);
    private static readonly Color PoisonColor = new Color(0.2f, 0.8f, 0.2f);
    private static readonly Color BleedColor  = new Color(0.65f, 0.05f, 0.1f);

    private SpriteRenderer      _spriteRenderer;
    private Color               _originalColor;
    private EntityStatusEffects _status;
    private Coroutine           _colorCycleCoroutine;

    private void Awake()
    {
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer != null)
            _originalColor = _spriteRenderer.color;
        _status = GetComponent<EntityStatusEffects>();
    }

    private void OnEnable()
    {
        if (_status == null)
            _status = GetComponent<EntityStatusEffects>();
        if (_status != null)
            _status.StatusChanged += RefreshStatusColor;

        _colorCycleCoroutine = null; // coroutines already stopped on disable
        if (_spriteRenderer != null)
            _spriteRenderer.color = _originalColor;
    }

    private void OnDisable()
    {
        if (_status != null)
            _status.StatusChanged -= RefreshStatusColor;
        _colorCycleCoroutine = null;
    }

    public void PlayHitFlash()
    {
        if (!isActiveAndEnabled) return;
        StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        if (_spriteRenderer != null) _spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(0.1f);
        RefreshStatusColor();
    }

    private Color GetCurrentStatusColor()
    {
        if (_status != null)
        {
            if (_status.IsOnFire)   return FireColor;
            if (_status.IsFrozen)   return FreezeColor;
            if (_status.IsPoisoned) return PoisonColor;
            if (_status.IsBleeding) return BleedColor;
        }
        return _originalColor;
    }

    private void RefreshStatusColor()
    {
        bool allThree = _status != null && _status.IsFrozen && _status.IsOnFire && _status.IsPoisoned;
        if (allThree)
        {
            if (_colorCycleCoroutine == null && isActiveAndEnabled)
                _colorCycleCoroutine = StartCoroutine(TripleStatusColorCycle());
        }
        else
        {
            if (_colorCycleCoroutine != null)
            {
                StopCoroutine(_colorCycleCoroutine);
                _colorCycleCoroutine = null;
            }
            if (_spriteRenderer != null)
                _spriteRenderer.color = GetCurrentStatusColor();
        }
    }

    private IEnumerator TripleStatusColorCycle()
    {
        Color[] colors = { FreezeColor, FireColor, PoisonColor };
        float stepDuration = 0.35f;
        int index = 0;

        while (_status != null && _status.IsFrozen && _status.IsOnFire && _status.IsPoisoned)
        {
            Color from = colors[index % 3];
            Color to   = colors[(index + 1) % 3];
            float elapsed = 0f;
            while (elapsed < stepDuration && _status.IsFrozen && _status.IsOnFire && _status.IsPoisoned)
            {
                elapsed += Time.deltaTime;
                if (_spriteRenderer != null)
                    _spriteRenderer.color = Color.Lerp(from, to, elapsed / stepDuration);
                yield return null;
            }
            index++;
        }

        _colorCycleCoroutine = null;
        RefreshStatusColor();
    }
}
