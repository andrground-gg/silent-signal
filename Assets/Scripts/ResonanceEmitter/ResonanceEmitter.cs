using System;
using DG.Tweening;
using UnityEngine;

public class ResonanceEmitter : BaseInteractable
{
    public enum EmitterState { Off, Active }

    [Header("Required Panels")]
    [SerializeField] private SolarPanel[] requiredPanels;

    [Header("Mechanical Parts (vibrate on activation)")]
    [SerializeField] private Transform[] vibratingParts;

    [Header("Indicators")]
    [SerializeField] private Renderer[] indicatorRenderers;
    [SerializeField] private Color idleEmission    = new Color(0.05f, 0.05f, 0.05f);
    [SerializeField] private Color activeEmission  = new Color(0f,    0.8f,  0.3f);
    [SerializeField] private Color failEmission    = new Color(1f,    0f,    0f);
    [SerializeField] private float idleFlickerSpeed     = 2.5f;
    [SerializeField] private float idleFlickerIntensity = 0.4f;
    [SerializeField] private float activePulseSpeed     = 4f;

    [Header("Audio — Idle")]
    [SerializeField] private AudioSource ambientHumSound;
    [SerializeField] private AudioSource highFreqToneSound;

    [Header("Audio — Activation")]
    [SerializeField] private AudioSource buttonSound;
    [SerializeField] private AudioSource mechanicalStartupSound;
    [SerializeField] private AudioSource rumbleSound;

    [Header("Audio — Failure")]
    [SerializeField] private AudioSource unstableSound;
    [SerializeField] private AudioSource shutdownSound;

    [Header("Audio — Success")]
    [SerializeField] private AudioSource successResonanceSound;
    [SerializeField] private AudioSource caveResonanceSound;

    [Header("Vibration")]
    [SerializeField] private float failVibrationStrength  = 0.05f;
    [SerializeField] private float failVibrationDuration  = 1.5f;
    [SerializeField] private float successVibrationStrength = 0.12f;
    [SerializeField] private float successVibrationDuration = 3f;

    [Header("Timing")]
    [SerializeField] private float failShutdownDelay = 2f;

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public EmitterState State      { get; private set; }
    public bool         IsUnlocked { get; private set; }

    public event Action OnActivationSuccess;
    public event Action OnActivationFailure;

    private MaterialPropertyBlock _mpb;
    private Sequence _sequence;
    private float    _noiseSeed;
    private float    _activeT;
    private bool     _isActivating;

    protected override void Awake()
    {
        base.Awake();
        _mpb       = new MaterialPropertyBlock();
        _noiseSeed = UnityEngine.Random.Range(0f, 100f);
        canInteract = false;

        foreach (var r in indicatorRenderers)
            if (r != null) EnableEmission(r);

        PlayIdleAmbient();
    }

    private void Update()
    {
        if (_isActivating) return;

        if (State == EmitterState.Active)
        {
            _activeT += Time.deltaTime * activePulseSpeed;
            float pulse = 0.7f + Mathf.Sin(_activeT) * 0.3f;
            SetIndicators(activeEmission * pulse);
        }
        else
        {
            float noise = Mathf.PerlinNoise(Time.time * idleFlickerSpeed + _noiseSeed, _noiseSeed);
            float flicker = 1f - idleFlickerIntensity + noise * idleFlickerIntensity;
            SetIndicators(idleEmission * flicker);
        }
    }

    public void Unlock()
    {
        if (IsUnlocked) return;
        IsUnlocked  = true;
        canInteract = true;
    }

    public override void Interact()
    {
        base.Interact();
        if (_isActivating) return;

        buttonSound?.Play();

        if (CheckWorldState())
            PlaySuccessSequence();
        else
            PlayFailureSequence();
    }

    private bool CheckWorldState()
    {
        foreach (var panel in requiredPanels)
            if (panel == null || !panel.IsActivated) return false;
        return true;
    }

    private void PlaySuccessSequence()
    {
        _isActivating = true;
        canInteract   = false;

        _sequence?.Kill();
        _sequence = DOTween.Sequence()
            .AppendCallback(() =>
            {
                mechanicalStartupSound?.Play();
                VibrateAll(successVibrationStrength, successVibrationDuration);
            })
            .AppendInterval(0.4f)
            .AppendCallback(() =>
            {
                if (rumbleSound != null) { rumbleSound.loop = true; rumbleSound.Play(); }
                successResonanceSound?.Play();
                caveResonanceSound?.Play();
            })
            .AppendInterval(successVibrationDuration - 0.4f)
            .AppendCallback(() =>
            {
                State         = EmitterState.Active;
                _isActivating = false;
                OnActivationSuccess?.Invoke();
            });
    }

    private void PlayFailureSequence()
    {
        _isActivating = true;
        canInteract   = false;

        _sequence?.Kill();
        _sequence = DOTween.Sequence()
            .AppendCallback(() =>
            {
                mechanicalStartupSound?.Play();
                unstableSound?.Play();
                VibrateAll(failVibrationStrength, failVibrationDuration);
            })
            .AppendInterval(failVibrationDuration * 0.5f)
            .AppendCallback(() => FlashIndicators(failEmission, 3))
            .AppendInterval(failShutdownDelay)
            .AppendCallback(() =>
            {
                shutdownSound?.Play();
                mechanicalStartupSound?.Stop();
                unstableSound?.Stop();
            })
            .AppendInterval(0.5f)
            .AppendCallback(() =>
            {
                _isActivating = false;
                canInteract   = true;
                OnActivationFailure?.Invoke();
            });
    }

    private void VibrateAll(float strength, float duration)
    {
        foreach (var part in vibratingParts)
            if (part != null)
                part.DOShakePosition(duration, strength, 30, 90f, false, false)
                    .SetLink(part.gameObject);
    }

    private void FlashIndicators(Color color, int flashes)
    {
        var seq = DOTween.Sequence();
        for (int i = 0; i < flashes; i++)
        {
            seq.AppendCallback(() => SetIndicators(color * 3f))
               .AppendInterval(0.12f)
               .AppendCallback(() => SetIndicators(idleEmission))
               .AppendInterval(0.1f);
        }
    }

    private void SetIndicators(Color emission)
    {
        foreach (var r in indicatorRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColor, emission);
            r.SetPropertyBlock(_mpb);
        }
    }

    private void EnableEmission(Renderer r)
    {
        foreach (var mat in r.materials)
            mat.EnableKeyword("_EMISSION");
    }

    private void PlayIdleAmbient()
    {
        if (ambientHumSound != null)   { ambientHumSound.loop   = true; ambientHumSound.Play(); }
        if (highFreqToneSound != null) { highFreqToneSound.loop = true; highFreqToneSound.Play(); }
    }

    private void OnDestroy() => _sequence?.Kill();
}
