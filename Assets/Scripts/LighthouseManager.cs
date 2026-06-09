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
    [SerializeField] private float lowMultiplier     = 0.3f;
    [SerializeField] private float highMultiplier    = 3.0f;
    [SerializeField] private float intensitySpeed    = 1.5f;
    [SerializeField] private float timeOfDaySpeed    = 1.5f;
    [SerializeField] private float fogSpeed          = 1.5f;

    [Header("Generator Visibility Multipliers")]
    [SerializeField] private float timeOfDayDisabledValue = 1f;
    [SerializeField] private float timeOfDayEnabledValue  = 2f;

    [Header("Gameplay Fog Per Intensity")]
    [SerializeField] private float fogLowVisibility    = 1f;
    [SerializeField] private float fogLowDensity       = 5f;
    [SerializeField] private float fogHighVisibility   = 50f;
    [SerializeField] private float fogHighDensity      = 0f;

    [Header("Angular velocities (degrees per second)")]
    [SerializeField] private float slowSpeed   = 20f;
    [SerializeField] private float normalSpeed = 60f;
    [SerializeField] private float fastSpeed   = 150f;

    [Header("Transition")]
    [SerializeField] private float velocitySpeed = 3f;

    [Header("Debug (read-only)")]
    [SerializeField] private float _dbgCurrentMultiplier;
    [SerializeField] private float _dbgTargetMultiplier;
    [SerializeField] private bool  _dbgLensClean;
    [SerializeField] private float _dbgFogVisibility;

    public float CurrentMultiplier => _currentMultiplier;
    public float HighMultiplier    => highMultiplier;

    private float _targetVelocity;
    private float _currentVelocity;

    private bool  _generatorOn;
    private float _targetMultiplier;
    private float _currentMultiplier;
    private float _todMultiplier;
    private float _fogMultiplier;

    public event Action<SpeedState> OnSpeedChanged;

    private void Start()
    {
        _generatorOn       = GeneratorManager.Instance.IsActive(GeneratorID.GENERATOR_LIGHTHOUSE);
        _targetMultiplier  = ComputeTargetMultiplier();
        _currentMultiplier = _targetMultiplier;
        _todMultiplier     = _targetMultiplier;
        _fogMultiplier     = _targetMultiplier;

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
        _currentVelocity = Mathf.MoveTowards(_currentVelocity, _targetVelocity, velocitySpeed * Time.deltaTime);
        beamPivot.Rotate(Vector3.up, _currentVelocity * Time.deltaTime, Space.World);

        // beam intensity — drives beamPulse directly
        _currentMultiplier = Mathf.MoveTowards(_currentMultiplier, _targetMultiplier, intensitySpeed * Time.deltaTime);
        beamPulse.SetMultiplierDirect(_currentMultiplier);

        // time of day — independent speed
        _todMultiplier = Mathf.MoveTowards(_todMultiplier, _targetMultiplier, timeOfDaySpeed * Time.deltaTime);
        float tTod   = Mathf.InverseLerp(lowMultiplier, highMultiplier, _todMultiplier);
        float todVis = Mathf.Lerp(timeOfDayDisabledValue, timeOfDayEnabledValue, tTod);
        TimeOfDayController.Instance.SetVisibilityMultiplierImmediate(todVis);

        // fog — independent speed
        _fogMultiplier = Mathf.MoveTowards(_fogMultiplier, _targetMultiplier, fogSpeed * Time.deltaTime);
        float tFog = Mathf.InverseLerp(lowMultiplier, highMultiplier, _fogMultiplier);
        float fogVis, fogDen;
        float t2 = tFog / 0.5f;
        fogVis = Mathf.Lerp(fogLowVisibility,    fogHighVisibility, t2);
        fogDen = Mathf.Lerp(fogLowDensity,       fogHighDensity,    t2);

        GameplayFogController.Instance.SetVisibilityImmediate(fogVis);
        GameplayFogController.Instance.SetDensityImmediate(fogDen);

        // debug
        _dbgCurrentMultiplier = _currentMultiplier;
        _dbgTargetMultiplier  = _targetMultiplier;
        _dbgLensClean         = lens == null || lens.IsClean;
        _dbgFogVisibility     = fogVis;
    }

    private float ComputeTargetMultiplier()
    {
        return _generatorOn switch
        {
            true => highMultiplier,
            false => lowMultiplier,
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