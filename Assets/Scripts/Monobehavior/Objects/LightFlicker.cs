using UnityEngine;
using UnityEngine.Rendering.Universal;

public class LightFlicker : MonoBehaviour
{
    [Header("Intensity")]
    [SerializeField] private float baseIntensity = 1f;
    [SerializeField] private float intensityVariance = 0.3f;

    [Header("Radius")]
    [SerializeField] private float baseOuterRadius = 3f;
    [SerializeField] private float radiusVariance = 0.2f;

    [Header("Speed")]
    [SerializeField] private float flickerSpeed = 3f;

    private Light2D _light;
    private float _seed;

    private void Awake()
    {
        _light = GetComponent<Light2D>();
        _seed = Random.Range(0f, 100f);
    }

    private void Update()
    {
        float t = Time.time * flickerSpeed + _seed;
        float noise = Mathf.PerlinNoise(t, t * 0.7f);

        _light.intensity = baseIntensity + (noise - 0.5f) * 2f * intensityVariance;
        _light.pointLightOuterRadius = baseOuterRadius + (noise - 0.5f) * 2f * radiusVariance;
    }
}
