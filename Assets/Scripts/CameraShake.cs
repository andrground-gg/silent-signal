using UnityEngine;

/// <summary>
/// Standalone positional camera shake. Two ways to drive it:
///  • <see cref="SetShake"/> — a continuous shake you update every frame.
///  • <see cref="Kick"/> — a single decaying jolt (e.g. one per impact).
/// Both use Perlin noise in LateUpdate so they layer on top of look/movement,
/// and always offset from the rest local position.
/// </summary>
public class CameraShake : MonoBehaviour
{
    [Tooltip("Transform that gets shaken. Defaults to this object's transform.")]
    [SerializeField] private Transform shakeTarget;

    private Vector3 _restLocalPos;
    private float _seed;

    // Continuous shake.
    private float _amplitude;
    private float _frequency = 20f;
    private bool _continuous;

    // One-shot decaying jolt.
    private float _kickAmp;
    private float _kickTime;
    private float _kickDuration;

    private void Awake()
    {
        if (shakeTarget == null) shakeTarget = transform;
        _restLocalPos = shakeTarget.localPosition;
        _seed = Random.value * 100f;
    }

    /// <summary>Continuous shake. amplitude in local units, frequency in Hz-ish.</summary>
    public void SetShake(float amplitude, float frequency)
    {
        _amplitude = Mathf.Max(0f, amplitude);
        if (frequency > 0f) _frequency = frequency;
        _continuous = _amplitude > 0f;
    }

    /// <summary>
    /// A single jolt that decays to zero over <paramref name="duration"/>. Stacks
    /// by taking the stronger value, so rapid kicks never cut each other short.
    /// </summary>
    public void Kick(float amplitude, float duration, float frequency = 0f)
    {
        if (amplitude <= 0f || duration <= 0f) return;
        if (frequency > 0f) _frequency = frequency;

        float currentKick = _kickTime > 0f ? _kickAmp * Mathf.Clamp01(_kickTime / _kickDuration) : 0f;
        _kickAmp      = Mathf.Max(currentKick, amplitude);
        _kickDuration = duration;
        _kickTime     = duration;
    }

    public void StopShake()
    {
        _amplitude  = 0f;
        _continuous = false;
        _kickAmp    = 0f;
        _kickTime   = 0f;
        shakeTarget.localPosition = _restLocalPos;
    }

    private void LateUpdate()
    {
        float amp = _continuous ? _amplitude : 0f;

        if (_kickTime > 0f)
        {
            _kickTime -= Time.deltaTime;
            float k = _kickAmp * Mathf.Clamp01(_kickTime / _kickDuration);
            if (k > amp) amp = k;
        }

        if (amp <= 0f)
        {
            if (shakeTarget.localPosition != _restLocalPos)
                shakeTarget.localPosition = _restLocalPos;
            return;
        }

        float t = Time.time * _frequency;
        float x = (Mathf.PerlinNoise(_seed, t) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(_seed + 11.3f, t) - 0.5f) * 2f;
        float z = (Mathf.PerlinNoise(_seed + 23.7f, t) - 0.5f) * 2f;

        shakeTarget.localPosition = _restLocalPos + new Vector3(x, y, z) * amp;
    }
}
