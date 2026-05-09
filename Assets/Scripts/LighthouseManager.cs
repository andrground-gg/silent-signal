using System;
using GeneratorSystem;
using UnityEngine;

public class LighthouseManager : Singleton<LighthouseManager>
{
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

    [Header("Night Event")]
    [SerializeField, Range(0, 23)] private int nightEventHour = 21;

    private float _targetVelocity;
    private float _currentVelocity;
    private bool _generatorActivated;
    public event Action<SpeedState> OnSpeedChanged;

    private void OnDisable()
    {
        leverController.OnSpeedChanged -= HandleSpeedChanged;
        GeneratorManager.Instance.OnGeneratorActivated -= HandleOnGeneratorActivated;

        if (TimeManager.Instance != null)
            TimeManager.Instance.Service.OnHourChange -= HandleHourChange;
    }

    private void Start()
    {
        _targetVelocity  = VelocityFor(leverController.Current);
        _currentVelocity = _targetVelocity;
        
        leverController.OnSpeedChanged += HandleSpeedChanged;
        GeneratorManager.Instance.OnGeneratorActivated += HandleOnGeneratorActivated;
        TimeManager.Instance.Service.OnHourChange += HandleHourChange;
    }

    private void Update()
    {
        _currentVelocity = Mathf.Lerp(_currentVelocity, _targetVelocity, lerpRate * Time.deltaTime);
        beamPulse.transform.Rotate(Vector3.up, _currentVelocity * Time.deltaTime, Space.World);
    }

    private void HandleHourChange()
    {
        if (TimeManager.Instance.Service.CurrentTime.Hour == nightEventHour)
        {
            TriggerNightSpeed(SpeedState.Slow);
        }
    }

    private void TriggerNightSpeed(SpeedState state)
    {
        _targetVelocity = VelocityFor(state);
        Debug.Log($"[Lighthouse] Night event triggered at {nightEventHour}:00 → {state}");
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
        _generatorActivated = !_generatorActivated;
        if (_generatorActivated)
        {
            TimeOfDayController.Instance?.SetVisibilityMultiplier(generatorEnabledValue);
            beamPulse.BoostEmission();
        }
        else
        {
            TimeOfDayController.Instance?.SetVisibilityMultiplier(generatorDisabledValue);
            beamPulse.RestoreEmission();
        }

        Debug.Log($"Lighthouse generator activated for {id}");
    }
}