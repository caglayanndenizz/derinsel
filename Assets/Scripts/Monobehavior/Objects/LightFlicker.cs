using UnityEngine;
using UnityEngine.Rendering.Universal;

public class LightFlicker : MonoBehaviour
{
    [Header("Variance")]
    [SerializeField] private float intensityVariance = 0.3f;
    [SerializeField] private float radiusVariance = 0.2f;
    [SerializeField] private float positionJitter = 0.05f;

    [Header("Speed")]
    [SerializeField] private float flickerSpeed = 3f;

    private Light2D _light;
    private float _baseIntensity;
    private float _baseOuterRadius;
    private Vector3 _basePosition;
    private float _seed;

    private void Awake()
    {
        _light = GetComponent<Light2D>();
        _baseIntensity = _light.intensity;
        _baseOuterRadius = _light.pointLightOuterRadius;
        _basePosition = transform.localPosition;
        _seed = Random.Range(0f, 100f);
    }

    private void Update()
    {
        float t = Time.time * flickerSpeed + _seed;
        float noise = Mathf.PerlinNoise(t, t * 0.7f);
        float noiseX = Mathf.PerlinNoise(t * 1.3f, _seed);
        float noiseY = Mathf.PerlinNoise(_seed, t * 1.1f);

        _light.intensity = _baseIntensity + (noise - 0.5f) * 2f * intensityVariance;
        _light.pointLightOuterRadius = _baseOuterRadius + (noise - 0.5f) * 2f * radiusVariance;

        transform.localPosition = _basePosition + new Vector3(
            (noiseX - 0.5f) * 2f * positionJitter,
            (noiseY - 0.5f) * 2f * positionJitter,
            0f
        );
    }
}
