using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Hover + click SFX for any UI element. Uses pointer events directly (not Button.onClick)
/// so it keeps working even if another script on the same object calls
/// button.onClick.RemoveAllListeners() when re-configuring the button (e.g.
/// AugmentOptionButton re-using pooled option buttons for every new offer).
/// Plays through the nearest AudioSource up the hierarchy (e.g. the Canvas) instead of
/// owning one itself, so the sound survives even if this button's own panel gets
/// hidden/deactivated right after the click.
/// </summary>
public class ButtonSFX : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public AudioClip hoverSFX;
    public AudioClip clickSFX;

    [Tooltip("Minimum seconds between hover sounds, shared across every ButtonSFX — prevents a sound storm when the mouse sweeps quickly across many buttons (e.g. the augment selection grid).")]
    public float hoverSfxCooldown = 0.08f;

    // Shared across all instances (static) — the cooldown limits the total hover-sound rate,
    // not just repeats on the same button. Unscaled time because augment selection pauses
    // Time.timeScale, and the cooldown must still expire while the game is paused.
    private static float _lastHoverSfxTime = -999f;

    private AudioSource _audioSource;

    private void Start()
    {
        _audioSource = GetComponentInParent<AudioSource>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Time.unscaledTime - _lastHoverSfxTime < hoverSfxCooldown) return;
        _lastHoverSfxTime = Time.unscaledTime;
        PlaySFX(hoverSFX);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PlaySFX(clickSFX);
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null || _audioSource == null) return;
        _audioSource.PlayOneShot(clip);
    }
}
