using System;
using GeneratorSystem;
using UnityEngine;

public class LighthouseManager : Singleton<LighthouseManager>
{
    [Header("Event Managers")]
    [SerializeField] private NightEventManager nightEventManager;

    [SerializeField] private Transform       beamPivot;
    public             Transform       BeamPivot => beamPivot;
    [SerializeField] private LightHousePulse beamPulse;
    [SerializeField] private LeverController leverController;
    [SerializeField] private LighthouseLens  lens;

    [Header("Intensity Levels")]
    [SerializeField] private float lowMultiplier    = 0.3f;
    [SerializeField] private float mediumMultiplier = 1.0f;
    [SerializeField] private float highMultiplier   = 3.0f;
    [SerializeField] private float intensityLerpRate = 1.5f;

    [Header("Generator Visibility Multipliers")]
    [SerializeField] private float timeOfDayDisabledValue = 1f;
    [SerializeField] private float timeOfDayEnabledValue  = 2f;
    [SerializeField] private float gameplayFogDisabledValue = 1f;
    [SerializeField] private float gameplayFogEnabledValue  = 2f;

    [Header("Angular velocities (degrees per second)")]
    [SerializeField] private float slowSpeed   = 20f;
    [SerializeField] private float normalSpeed = 60f;
    [SerializeField] private float fastSpeed   = 150f;

    [Header("Transition")]
    [SerializeField] private float lerpRate = 3f;

    [Header("Debug (read-only)")]
    [SerializeField] private float _dbgCurrentMultiplier;
    [SerializeField] private float _dbgTargetMultiplier;
    [SerializeField] private bool  _dbgLensClean;
    [SerializeField] private float _dbgFogVisibility;

    public float CurrentMultiplier => _currentMultiplier;
    public float MediumMultiplier  => mediumMultiplier;
    public float HighMultiplier    => highMultiplier;

    private float _targetVelocity;
    private float _currentVelocity;

    private bool  _generatorOn;
    private float _targetMultiplier;
    private float _currentMultiplier;

    public event Action<SpeedState> OnSpeedChanged;

    private void Start()
    {
        _generatorOn      = GeneratorManager.Instance.IsActive(GeneratorID.GENERATOR_LIGHTHOUSE);
        _targetMultiplier = ComputeTargetMultiplier();
        _currentMultiplier = _targetMultiplier;

        _targetVelocity  = VelocityFor(leverController.Current);
        _currentVelocity = _targetVelocity;

        leverController.OnSpeedChanged                    += HandleSpeedChanged;
        GeneratorManager.Instance.OnGeneratorActivated   += HandleOnGeneratorActivated;
        GeneratorManager.Instance.OnGeneratorDeactivated += HandleOnGeneratorDeactivated;
        nightEventManager.OnNightEventTriggered           += OnNightEventTriggered;

        if (lens != null)
            lens.OnCleanStateChanged += _ => _targetMultiplier = ComputeTargetMultiplier();
    }

    private void OnDisable()
    {
        leverController.OnSpeedChanged                    -= HandleSpeedChanged;
        GeneratorManager.Instance.OnGeneratorActivated   -= HandleOnGeneratorActivated;
        GeneratorManager.Instance.OnGeneratorDeactivated -= HandleOnGeneratorDeactivated;
        nightEventManager.OnNightEventTriggered           -= OnNightEventTriggered;

        if (lens != null)
            lens.OnCleanStateChanged -= _ => _targetMultiplier = ComputeTargetMultiplier();
    }

    private void Update()
    {
        // beam rotation
        _currentVelocity = Mathf.Lerp(_currentVelocity, _targetVelocity, lerpRate * Time.deltaTime);
        beamPivot.Rotate(Vector3.up, _currentVelocity * Time.deltaTime, Space.World);

        // intensity — smooth chase
        _currentMultiplier = Mathf.Lerp(_currentMultiplier, _targetMultiplier, intensityLerpRate * Time.deltaTime);
        beamPulse.SetMultiplierDirect(_currentMultiplier);

        // fog visibility — map current multiplier into [min, max] range
        float t      = Mathf.InverseLerp(lowMultiplier, highMultiplier, _currentMultiplier);
        float todVis = Mathf.Lerp(timeOfDayDisabledValue,   timeOfDayEnabledValue,   t);
        float fogVis = Mathf.Lerp(gameplayFogDisabledValue, gameplayFogEnabledValue, t);
        TimeOfDayController.Instance?.SetVisibilityMultiplierImmediate(todVis);
        GameplayFogController.Instance?.SetVisibilityMultiplierImmediate(fogVis);

        // debug
        _dbgCurrentMultiplier = _currentMultiplier;
        _dbgTargetMultiplier  = _targetMultiplier;
        _dbgLensClean         = lens == null || lens.IsClean;
        _dbgFogVisibility     = fogVis;
    }

    private float ComputeTargetMultiplier()
    {
        bool clean = lens == null || lens.IsClean;
        return (_generatorOn, clean) switch
        {
            (false, false) => lowMultiplier,
            (true,  false) => mediumMultiplier,
            (false, true)  => mediumMultiplier,
            (true,  true)  => highMultiplier,
        };
    }

    private void HandleSpeedChanged(SpeedState newState)
    {
        _targetVelocity = VelocityFor(newState);
        OnSpeedChanged?.Invoke(newState);
    }

    private float VelocityFor(SpeedState state) => state switch
    {
        SpeedState.Slow   => slowSpeed,
        SpeedState.Normal => normalSpeed,
        SpeedState.Fast   => fastSpeed,
        _                 => normalSpeed
    };

    private void HandleOnGeneratorActivated(GeneratorID id)
    {
        if (id != GeneratorID.GENERATOR_LIGHTHOUSE) return;
        _generatorOn      = true;
        _targetMultiplier = ComputeTargetMultiplier();
    }

    private void HandleOnGeneratorDeactivated(GeneratorID id)
    {
        if (id != GeneratorID.GENERATOR_LIGHTHOUSE) return;
        _generatorOn      = false;
        _targetMultiplier = ComputeTargetMultiplier();
    }

    private void OnNightEventTriggered() => HandleSpeedChanged(SpeedState.Slow);
}
