using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(GraphicRaycaster))]
public class TransitionFader : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private bool blockRaycastsDuringFade = true;
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private int sortingOrder = 100;

    private Coroutine _runningFadeRoutine;

    public float FadeDuration => Mathf.Max(0f, fadeDuration);

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        EnsureFadeImage();

        // Keep canvas active but invisible at game start.
        SetState(0f, false);
    }

    // Builds the full-screen black Image child if the object doesn't already have one,
    // so dragging this script onto a plain empty GameObject is enough to get a working fader.
    private void EnsureFadeImage()
    {
        if (GetComponentInChildren<Image>() != null)
            return;

        var imageGO = new GameObject("FadeImage", typeof(RectTransform), typeof(Image));
        imageGO.transform.SetParent(transform, false);

        var rect = (RectTransform)imageGO.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = imageGO.GetComponent<Image>();
        image.color = fadeColor;
        image.raycastTarget = false;
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
            elapsed += Time.unscaledDeltaTime;
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
