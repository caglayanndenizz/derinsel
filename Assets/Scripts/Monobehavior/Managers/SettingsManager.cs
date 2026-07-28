using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Persists and applies fullscreen + brightness settings across every scene without needing a scene object.</summary>
public static class SettingsManager
{
    private const string FullscreenKey = "Settings_Fullscreen";
    private const string BrightnessKey = "Settings_Brightness";

    public const float MinBrightness = 0.3f;
    public const float MaxBrightness = 2f;
    public const float DefaultBrightness = 1f;

    // Global Light 2D base intensity is authored per-level; brightness is applied as a multiplier on top of it.
    private static readonly Dictionary<Light2D, float> _baseIntensities = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        ApplyFullscreen(GetFullscreen());
        SceneManager.sceneLoaded += (scene, mode) => ApplyGlobalLightBrightness();
    }

    public static bool GetFullscreen() => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;

    public static void SetFullscreen(bool fullscreen)
    {
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        ApplyFullscreen(fullscreen);
    }

    private static void ApplyFullscreen(bool fullscreen)
    {
        Screen.fullScreen = fullscreen;
    }

    public static float GetBrightness() => PlayerPrefs.GetFloat(BrightnessKey, DefaultBrightness);

    public static void SetBrightness(float brightness)
    {
        PlayerPrefs.SetFloat(BrightnessKey, brightness);
        ApplyGlobalLightBrightness();
    }

    public static void ApplyGlobalLightBrightness()
    {
        float multiplier = GetBrightness();
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
}
