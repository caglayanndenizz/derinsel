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

    private AudioSource _audioSource;

    private void Start()
    {
        _audioSource = GetComponentInParent<AudioSource>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
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
