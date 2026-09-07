using UnityEngine;

/// <summary>
/// Drives a Light so it reads as an unseen campfire: intensity wanders around a
/// base value and the light drifts a little, both from layered Perlin noise so the
/// motion never loops audibly the way Random.value does.
/// </summary>
[RequireComponent(typeof(Light))]
public class CaveFirelightFlicker : MonoBehaviour
{
    [Tooltip("Intensity the flicker centres on. Picked up from the Light on Awake if left at 0.")]
    public float baseIntensity = 0f;

    [Tooltip("How far intensity swings either side of the base, as a fraction of it.")]
    [Range(0f, 1f)] public float intensityAmount = 0.22f;

    [Tooltip("Flicker speed. Higher is more agitated.")]
    public float speed = 2.4f;

    [Tooltip("How far the light drifts from its start position, in world units.")]
    public float positionJitter = 0.12f;

    Light _light;
    Vector3 _startPosition;
    float _seed;

    void Awake()
    {
        _light = GetComponent<Light>();
        if (baseIntensity <= 0f) baseIntensity = _light.intensity;
        _startPosition = transform.localPosition;
        _seed = Random.value * 100f;
    }

    void Update()
    {
        float t = Time.time * speed + _seed;

        // two octaves so the flame has both a slow breath and a fast crackle
        float n = Mathf.PerlinNoise(t, 0f) * 0.7f + Mathf.PerlinNoise(t * 3.1f, 5f) * 0.3f;
        _light.intensity = baseIntensity * (1f + (n - 0.5f) * 2f * intensityAmount);

        if (positionJitter > 0f)
        {
            transform.localPosition = _startPosition + new Vector3(
                (Mathf.PerlinNoise(t * 0.8f, 11f) - 0.5f) * 2f * positionJitter,
                (Mathf.PerlinNoise(t * 0.9f, 23f) - 0.5f) * 2f * positionJitter,
                (Mathf.PerlinNoise(t * 0.7f, 37f) - 0.5f) * 2f * positionJitter);
        }
    }
}
