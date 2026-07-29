using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class TransitionFader : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration;
    [SerializeField] private bool blockRaycastsDuringFade = true;

    private Coroutine _runningFadeRoutine;

    public float FadeDuration => Mathf.Max(0f, fadeDuration);

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        // Keep canvas active but invisible at game start.
        SetState(0f, false);
    }

    public IEnumerator FadeOutIn(System.Action onBlackReached)
    {
        yield return FadeTo(1f);
        onBlackReached?.Invoke();
        yield return FadeTo(0f);
    }

    public IEnumerator FadeTo(float targetAlpha)
    {
        if (canvasGroup == null)
            yield break;

        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        if (_runningFadeRoutine != null)
            StopCoroutine(_runningFadeRoutine);

        bool shouldBlock = blockRaycastsDuringFade && targetAlpha > 0f;
        canvasGroup.blocksRaycasts = shouldBlock;
        canvasGroup.interactable = false;

        _runningFadeRoutine = StartCoroutine(FadeRoutine(targetAlpha));
        yield return _runningFadeRoutine;
        _runningFadeRoutine = null;

        // Keep raycasts disabled when invisible after fade completes.
        if (Mathf.Approximately(targetAlpha, 0f))
            canvasGroup.blocksRaycasts = false;
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        float duration = FadeDuration;
        float startAlpha = canvasGroup.alpha;

        if (duration <= 0f)
        {
            canvasGroup.alpha = Mathf.Clamp01(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = Mathf.Clamp01(targetAlpha);
    }

    private void SetState(float alpha, bool blockRaycasts)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = Mathf.Clamp01(alpha);
        canvasGroup.blocksRaycasts = blockRaycasts;
        canvasGroup.interactable = false;
    }
}
