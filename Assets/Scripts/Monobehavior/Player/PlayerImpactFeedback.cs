using UnityEngine;
using Unity.Cinemachine;

public class PlayerImpactFeedback : MonoBehaviour
{
    [Header("Hit Stop")]
    [SerializeField] private HitStopManager hitStopManager;
    [Range(0.01f, 1f)] [SerializeField] private float lightHitStopTimeScale = 0.14f;
    [SerializeField] private float lightHitStopDuration = 0.02f;
    [Range(0.01f, 1f)] [SerializeField] private float heavyHitStopTimeScale = 0.08f;
    [SerializeField] private float heavyHitStopDuration = 0.045f;

    [Header("Impact Feedback")]
    [SerializeField] private CinemachineImpulseSource lightHitImpulse;
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private AudioSource hitAudioSource;
    [SerializeField] private AudioClip lightHitSfx;
    [SerializeField] private AudioClip heavyHitSfx;
    [Range(0.5f, 1f)] [SerializeField] private float lightHitPitch = 0.92f;
    [Range(0.5f, 1f)] [SerializeField] private float heavyHitPitch = 0.82f;

    public void PlayHeavyHit(Vector3 worldPos, CinemachineImpulseSource fallbackLightImpulse = null)
    {
        SpawnHitVfx(worldPos);
        PlayImpactFeedback(true, fallbackLightImpulse);
    }

    public void PlayLightHit(Vector3 worldPos, CinemachineImpulseSource fallbackLightImpulse = null)
    {
        SpawnHitVfx(worldPos);
        PlayImpactFeedback(false, fallbackLightImpulse);
    }

    private void PlayImpactFeedback(bool isHeavy, CinemachineImpulseSource fallbackLightImpulse)
    {
        EnsureHitStopManager();
        if (hitStopManager != null)
        {
            float rawTimeScale = isHeavy ? heavyHitStopTimeScale : lightHitStopTimeScale;
            float rawDuration = isHeavy ? heavyHitStopDuration : lightHitStopDuration;

            float minimumSmoothScale = isHeavy ? 0.08f : 0.14f;
            float maximumSmoothDuration = isHeavy ? 0.045f : 0.02f;
            float timeScale = Mathf.Clamp(rawTimeScale, minimumSmoothScale, 1f);
            float duration = Mathf.Clamp(rawDuration, 0f, maximumSmoothDuration);
            hitStopManager.TriggerHitStop(timeScale, duration);
        }

        if (!isHeavy)
        {
            CinemachineImpulseSource lightImpulseSource = lightHitImpulse != null ? lightHitImpulse : fallbackLightImpulse;
            if (lightImpulseSource != null)
                lightImpulseSource.GenerateImpulse();
        }

        PlayHitSfx(isHeavy);
    }

    private void EnsureHitStopManager()
    {
        if (hitStopManager == null)
            hitStopManager = HitStopManager.Instance != null
                ? HitStopManager.Instance
                : Object.FindAnyObjectByType<HitStopManager>();

        if (hitStopManager == null)
        {
            GameObject managerObject = new GameObject("HitStopManager");
            hitStopManager = managerObject.AddComponent<HitStopManager>();
        }
    }

    private void PlayHitSfx(bool isHeavy)
    {
        if (hitAudioSource == null) return;

        AudioClip clip = isHeavy ? heavyHitSfx : lightHitSfx;
        if (clip == null) return;

        float previousPitch = hitAudioSource.pitch;
        hitAudioSource.pitch = isHeavy ? heavyHitPitch : lightHitPitch;
        hitAudioSource.PlayOneShot(clip);
        hitAudioSource.pitch = previousPitch;
    }

    [Tooltip("hitVfxPrefab'ın sahnede kalacağı süre (saniye). Sonra otomatik yok edilir.")]
    [SerializeField] private float hitVfxLifetime = 2f;

    private void SpawnHitVfx(Vector3 worldPos)
    {
        if (hitVfxPrefab == null) return;
        GameObject vfx = Instantiate(hitVfxPrefab, worldPos, Quaternion.identity);
        Destroy(vfx, hitVfxLifetime);
    }
}
