using UnityEngine;
using DG.Tweening;

public class SpectralModulator : Lever
{
    [Header("Light Renderer")]
    [SerializeField] private Renderer lightRenderer;

    [Header("Colors")]
    [SerializeField] private Color yellowBase     = new Color(1f, 0.85f, 0f);
    [SerializeField] private Color yellowEmission = new Color(2f, 1.4f, 0f);
    [SerializeField] private Color uvBase         = new Color(0.45f, 0f, 1f);
    [SerializeField] private Color uvEmission     = new Color(1.0f, 0f, 2.8f);

    [Header("Transition")]
    [SerializeField] private float activateDuration   = 0.7f;
    [SerializeField] private float deactivateDuration = 0.5f;

    [Header("Noise")]
    [SerializeField] private float activeNoiseSpeed     = 9f;
    [SerializeField] private float activeNoiseIntensity = 0.35f;
    [SerializeField] private float idlePulseSpeed       = 1.8f;
    [SerializeField] private float idlePulseIntensity   = 0.08f;

    [Header("Ambient Sound")]
    [SerializeField] private AudioSource startupSound;
    [SerializeField] private AudioSource shutdownSound;
    [SerializeField] private AudioSource ambientSound;
    [SerializeField] private float ambientFadeInDuration  = 0.8f;
    [SerializeField] private float ambientFadeOutDuration = 0.4f;
    [SerializeField] [Range(0f, 1f)] private float ambientTargetVolume = 0.7f;
    [SerializeField] private float pitchNoiseSpeed     = 3f;
    [SerializeField] private float pitchNoiseIntensity = 0.06f;

    private Material _mat;
    private float    _t;
    private bool     _uvActive;
    private Tween    _tween;
    private Tween    _audioTween;
    private float    _noiseSeed;

    protected override void Awake()
    {
        base.Awake();

        if (lightRenderer != null)
        {
            _mat = lightRenderer.material;
            _mat.EnableKeyword("_EMISSION");
        }

        _noiseSeed = Random.Range(0f, 100f);
        ApplyColors(0f, 1f);

        OnPulled  += () => { UVWorldState.Instance.SetUVActive(true);  Activate(); };
        OnRelease += () => { UVWorldState.Instance.SetUVActive(false); Deactivate(); };
    }

    private void Update()
    {
        if (_mat == null) return;

        float speed     = _uvActive ? activeNoiseSpeed     : idlePulseSpeed;
        float intensity = _uvActive ? activeNoiseIntensity : idlePulseIntensity;

        float noise = Mathf.PerlinNoise(Time.time * speed + _noiseSeed, _noiseSeed);
        float pulse = 1f + (noise * 2f - 1f) * intensity;

        ApplyColors(_t, pulse);

        if (_uvActive && ambientSound != null && ambientSound.isPlaying)
        {
            float pitchNoise = Mathf.PerlinNoise(Time.time * pitchNoiseSpeed + _noiseSeed + 13f, _noiseSeed);
            ambientSound.pitch = 1f + (pitchNoise * 2f - 1f) * pitchNoiseIntensity;
        }
    }

    private void Activate()
    {
        _uvActive = true;
        _tween?.Kill();

        // Flicker phase — нестабільний перехід перед тим як осісти в UV
        _tween = DOTween.Sequence()
            .Append(DOTween.To(GetT, SetT, 0.40f, 0.1f))
            .Append(DOTween.To(GetT, SetT, 0.05f, 0.04f))
            .Append(DOTween.To(GetT, SetT, 0.65f, 0.06f))
            .Append(DOTween.To(GetT, SetT, 0.10f, 0.2f))
            .Append(DOTween.To(GetT, SetT, 0.80f, 0.07f))
            .Append(DOTween.To(GetT, SetT, 0.25f, 0.05f))
            .Append(DOTween.To(GetT, SetT, 1f, activateDuration).SetEase(Ease.OutQuart));

        startupSound?.Play();
        FadeAmbientIn();
    }

    private void Deactivate()
    {
        _uvActive = false;
        _tween?.Kill();
        _tween = DOTween.To(GetT, SetT, 0f, deactivateDuration).SetEase(Ease.OutSine);

        float vol = ambientSound != null ? ambientSound.volume : ambientTargetVolume;
        DOVirtual.DelayedCall(0.05f, () =>
        {
            if (shutdownSound == null) return;
            shutdownSound.volume = vol;
            shutdownSound.Play();
        });
        StopAmbient();
    }

    private void FadeAmbientIn()
    {
        if (ambientSound == null) return;
        _audioTween?.Kill();
        ambientSound.volume = 0f;
        ambientSound.pitch  = 1f;
        if (!ambientSound.isPlaying) ambientSound.Play();
        _audioTween = ambientSound.DOFade(ambientTargetVolume, ambientFadeInDuration).SetEase(Ease.OutQuad);
    }

    private void StopAmbient()
    {
        if (ambientSound == null) return;
        _audioTween?.Kill();
        _audioTween = ambientSound.DOFade(0f, 0.4f)
            .SetEase(Ease.InQuad)
            .OnComplete(() => { ambientSound.Stop(); ambientSound.pitch = 1f; });
    }

    private float GetT()        => _t;
    private void  SetT(float v) => _t = v;

    private void ApplyColors(float t, float pulse)
    {
        _mat.SetColor("_BaseColor",     Color.Lerp(yellowBase, uvBase, t));
        _mat.SetColor("_EmissionColor", Color.Lerp(yellowEmission, uvEmission, t) * pulse);
    }

    private void OnDestroy()
    {
        _tween?.Kill();
        _audioTween?.Kill();
    }
}
