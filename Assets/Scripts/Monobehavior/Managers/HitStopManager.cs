using System.Collections;
using UnityEngine;

public class HitStopManager : MonoBehaviour
{
    public static HitStopManager Instance { get; private set; }

    [Header("Stacking")]
    [Tooltip("Aynı anda gelen ek vuruşlar bu kadar (sn) uzatır.")]
    [SerializeField] private float stackDurationBonus = 0.004f;
    [Tooltip("Toplam uzatma tavanı (sn).")]
    [SerializeField] private float maxStackedBonus = 0.012f;

    private Coroutine _hitStopRoutine;
    private float _activeBaseDuration;
    private float _currentAddedDuration;
    private float _activeTimeScale = 1f;
    private float _restoreFixedDeltaTime;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _restoreFixedDeltaTime = Time.fixedDeltaTime;
    }

    public void TriggerHitStop(float requestedTimeScale, float duration)
    {
        float clampedScale = Mathf.Clamp(requestedTimeScale, 0.01f, 1f);
        float clampedDuration = Mathf.Max(0f, duration);
        if (clampedDuration <= 0f) return;

        if (_hitStopRoutine != null)
        {
            _activeTimeScale = Mathf.Min(_activeTimeScale, clampedScale);
            _activeBaseDuration = Mathf.Max(_activeBaseDuration, clampedDuration);
            _currentAddedDuration = Mathf.Min(_currentAddedDuration + stackDurationBonus, maxStackedBonus);
            return;
        }

        _activeTimeScale = clampedScale;
        _activeBaseDuration = clampedDuration;
        _currentAddedDuration = 0f;
        _hitStopRoutine = StartCoroutine(HitStopRoutine());
    }

    private IEnumerator HitStopRoutine()
    {
        ApplyTimeScale(_activeTimeScale);

        float elapsedRealtime = 0f;
        while (elapsedRealtime < _activeBaseDuration + _currentAddedDuration)
        {
            yield return null;
            elapsedRealtime += Time.unscaledDeltaTime;
        }

        ApplyTimeScale(1f);
        _hitStopRoutine = null;
        _activeBaseDuration = 0f;
        _currentAddedDuration = 0f;
        _activeTimeScale = 1f;
    }

    private void ApplyTimeScale(float scale)
    {
        Time.timeScale = scale;
        Time.fixedDeltaTime = _restoreFixedDeltaTime * scale;
    }

    void OnDisable()
    {
        if (Instance == this && Time.timeScale != 1f)
            ApplyTimeScale(1f);
    }
}
