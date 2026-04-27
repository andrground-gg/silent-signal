using GeneratorSystem;
using UnityEngine;

public class LighthouseManager : Singleton<LighthouseManager>
{
    [SerializeField] private Transform       beamPivot;

    [Header("Angular velocities (degrees per second)")]
    [SerializeField] private float slowSpeed   = 20f;
    [SerializeField] private float normalSpeed = 60f;
    [SerializeField] private float fastSpeed   = 150f;

    [Header("Transition")]
    [SerializeField] private float lerpRate = 3f;

    private float _targetVelocity;
    private float _currentVelocity;

    private void OnEnable()
    {
        LeverController.Instance.OnSpeedChanged += HandleSpeedChanged;
        GeneratorManager.Instance.OnGeneratorActivated += HandleOnGeneratorActivated;
    }

    private void OnDisable()
    {
        LeverController.Instance.OnSpeedChanged -= HandleSpeedChanged;
        GeneratorManager.Instance.OnGeneratorActivated -= HandleOnGeneratorActivated;
    }

    private void Start()
    {
        _targetVelocity  = VelocityFor(LeverController.Instance.Current);
        _currentVelocity = _targetVelocity;
    }

    private void Update()
    {
        _currentVelocity = Mathf.Lerp(_currentVelocity, _targetVelocity, lerpRate * Time.deltaTime);
        beamPivot.Rotate(Vector3.up, _currentVelocity * Time.deltaTime, Space.World);
    }

    private void HandleSpeedChanged(SpeedState newState)
    {
        _targetVelocity = VelocityFor(newState);
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
        Debug.Log($"Lighthouse generator activated for {id}");
    }
}
