using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Persists and applies brightness + sound settings across every scene without needing a scene object.</summary>
public static class SettingsManager
{
    private const string BrightnessKey = "Settings_Brightness";
    private const string MutedKey = "Settings_Muted";

    public const float MinBrightness = -50f;
    public const float MaxBrightness = 50f;
    public const float DefaultBrightness = 0f;

    // Global Light 2D base intensity is authored per-level; brightness is applied as a multiplier on top of it.
    private static readonly Dictionary<Light2D, float> _baseIntensities = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        ApplyMuted(GetMuted());
        SceneManager.sceneLoaded += (scene, mode) => ApplyGlobalLightBrightness();
    }

    public static float GetBrightness() => PlayerPrefs.GetFloat(BrightnessKey, DefaultBrightness);

    public static void SetBrightness(float brightness)
    {
        PlayerPrefs.SetFloat(BrightnessKey, brightness);
        ApplyGlobalLightBrightness();
    }

    public static void ApplyGlobalLightBrightness()
    {
        float multiplier = 1f + (GetBrightness() / 100f);
        Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
        foreach (Light2D light in lights)
        {
            if (light.lightType != Light2D.LightType.Global)
                continue;

            if (!_baseIntensities.TryGetValue(light, out float baseIntensity))
            {
                baseIntensity = light.intensity;
                _baseIntensities[light] = baseIntensity;
            }

            light.intensity = baseIntensity * multiplier;
        }
    }

    public static bool GetMuted() => PlayerPrefs.GetInt(MutedKey, 0) == 1;

    public static void SetMuted(bool muted)
    {
        PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
        ApplyMuted(muted);
    }

    private static void ApplyMuted(bool muted)
    {
        AudioListener.volume = muted ? 0f : 1f;
    }
}
