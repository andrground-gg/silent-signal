using UnityEngine;

public class LighthouseManager : MonoBehaviour
{
    [SerializeField] private LeverController leverController;
    [SerializeField] private Transform       beamPivot;

    [Header("Angular velocities (degrees per second)")]
    [SerializeField] private float slowSpeed   = 20f;
    [SerializeField] private float normalSpeed = 60f;
    [SerializeField] private float fastSpeed   = 150f;

    [Header("Transition")]
    [SerializeField] private float lerpRate = 3f;  // how quickly velocity changes

    private float _targetVelocity;
    private float _currentVelocity;

    private void OnEnable()
    {
        leverController.OnSpeedChanged += HandleSpeedChanged;
    }

    private void OnDisable()
    {
        leverController.OnSpeedChanged -= HandleSpeedChanged;
    }

    private void Start()
    {
        // Sync with whatever state the controller initialised to
        _targetVelocity  = VelocityFor(leverController.Current);
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
}
