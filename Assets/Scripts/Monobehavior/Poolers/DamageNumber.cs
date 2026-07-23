using UnityEngine;
using TMPro;

/// <summary>
/// Single floating combat-text instance: pops in above the hit point, floats upward while
/// fading out, then returns itself to <see cref="DamageNumberPooler"/>.
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class DamageNumber : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float lifetime = 0.8f;
    [SerializeField] private float floatDistance = 1f;
    [SerializeField] private float popScaleDuration = 0.1f;
    [SerializeField] private float popScale = 1.3f;
    [Tooltip("Fraction of lifetime (0-1) at which the fade-out begins.")]
    [SerializeField][Range(0f, 1f)] private float fadeStartFraction = 0.4f;

    [Header("Colors / Size")]
    [SerializeField] private Color lightHitColor = Color.white;
    [SerializeField] private Color heavyHitColor = new Color(1f, 0.35f, 0.1f);
    [SerializeField] private float lightFontSize = 3.5f;
    [SerializeField] private float heavyFontSize = 5f;

    private TextMeshPro _text;
    private float _elapsed;
    private Vector3 _startPos;
    private Color _baseColor;
    private System.Action<DamageNumber> _onComplete;

    private void Awake()
    {
        _text = GetComponent<TextMeshPro>();
        _text.alignment = TextAlignmentOptions.Center;
    }

    public void Play(Vector3 worldPosition, float damage, bool isHeavy, System.Action<DamageNumber> onComplete)
    {
        _onComplete = onComplete;
        _elapsed = 0f;
        _startPos = worldPosition + new Vector3(Random.Range(-0.25f, 0.25f), 0.4f, 0f);
        transform.position = _startPos;
        transform.localScale = Vector3.one * 0.4f;

        _baseColor = isHeavy ? heavyHitColor : lightHitColor;
        _text.text = Mathf.RoundToInt(Mathf.Max(0f, damage)).ToString();
        _text.color = _baseColor;
        _text.fontSize = isHeavy ? heavyFontSize : lightFontSize;

        gameObject.SetActive(true);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.0001f, lifetime));

        // Pop in, then settle back down to normal size.
        float popT = Mathf.Clamp01(_elapsed / Mathf.Max(0.0001f, popScaleDuration));
        float scale = popT < 1f
            ? Mathf.Lerp(0.4f, popScale, popT)
            : Mathf.Lerp(popScale, 1f, Mathf.Clamp01((_elapsed - popScaleDuration) / 0.15f));
        transform.localScale = Vector3.one * scale;

        // Float upward, easing out.
        float eased = 1f - Mathf.Pow(1f - t, 2f);
        transform.position = _startPos + Vector3.up * (floatDistance * eased);

        // Fade out over the tail of the lifetime.
        float alpha = t < fadeStartFraction ? 1f : 1f - Mathf.InverseLerp(fadeStartFraction, 1f, t);
        _text.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);

        if (_elapsed >= lifetime)
        {
            gameObject.SetActive(false);
            _onComplete?.Invoke(this);
        }
    }
}
