using System;
using GeneratorSystem;
using UnityEngine;

public class LighthouseManager : Singleton<LighthouseManager>
{
    [Header("Event Managers")]
    [SerializeField] private NightEventManager nightEventManager;
    
    [SerializeField] private Transform  beamPivot;
    [SerializeField] private LightHousePulse  beamPulse;
    [SerializeField] private LeverController leverController;
    
    [Header("Generator Settings")]
    [SerializeField] private float generatorEnabledValue = 2f;
    [SerializeField] private float generatorDisabledValue = 1f;
    
    [Header("Angular velocities (degrees per second)")]
    [SerializeField] private float slowSpeed   = 20f;
    [SerializeField] private float normalSpeed = 60f;
    [SerializeField] private float fastSpeed   = 150f;

    [Header("Transition")]
    [SerializeField] private float lerpRate = 3f;
    
    private float _targetVelocity;
    private float _currentVelocity;
    public event Action<SpeedState> OnSpeedChanged;

    private void OnDisable()
    {
        leverController.OnSpeedChanged -= HandleSpeedChanged;
        GeneratorManager.Instance.OnGeneratorActivated -= HandleOnGeneratorActivated;
        GeneratorManager.Instance.OnGeneratorDeactivated -= HandleOnGeneratorDeactivated;
        nightEventManager.OnNightEventTriggered -= OnNightEventTriggered;
    }
    
    private void Start()
    {
        _targetVelocity  = VelocityFor(leverController.Current);
        _currentVelocity = _targetVelocity;
        
        leverController.OnSpeedChanged += HandleSpeedChanged;
        GeneratorManager.Instance.OnGeneratorActivated += HandleOnGeneratorActivated;
        GeneratorManager.Instance.OnGeneratorDeactivated += HandleOnGeneratorDeactivated;
        nightEventManager.OnNightEventTriggered += OnNightEventTriggered;

    }

    private void Update()
    {
        _currentVelocity = Mathf.Lerp(_currentVelocity, _targetVelocity, lerpRate * Time.deltaTime);
        beamPivot.Rotate(Vector3.up, _currentVelocity * Time.deltaTime, Space.World);
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
        GameplayFogController.Instance?.SetVisibilityMultiplier(generatorEnabledValue);
        beamPulse.BoostEmission();
    }
    
    private void HandleOnGeneratorDeactivated(GeneratorID id)
    {
        if (id != GeneratorID.GENERATOR_LIGHTHOUSE) return;
        GameplayFogController.Instance?.SetVisibilityMultiplier(generatorDisabledValue);
        beamPulse.RestoreEmission();
    }

    private void OnNightEventTriggered()
    {
        HandleSpeedChanged(SpeedState.Slow);
    }
}