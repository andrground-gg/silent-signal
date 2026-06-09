using UnityEngine;

// Continuously spins a transform; the lever speed state (Slow / Normal / Fast)
// selects how fast it spins.
public class SpeedDial : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private LeverController _leverController;

    [Header("Dial")]
    [Tooltip("Object that rotates. Defaults to this transform if unset.")]
    [SerializeField] private Transform _dial;
    [Tooltip("Local axis to spin around.")]
    [SerializeField] private Vector3 _axis = Vector3.up;

    [Header("Spin speed per state (degrees / second)")]
    [SerializeField] private float _slowSpeed   = 30f;
    [SerializeField] private float _normalSpeed = 90f;
    [SerializeField] private float _fastSpeed   = 240f;

    private float _currentSpeed;

    private void Awake()
    {
        if (_dial == null) _dial = transform;
    }

    private void Start()
    {
        if (_leverController == null)
        {
            Debug.LogWarning($"[{name}] No LeverController assigned.", this);
            return;
        }

        _leverController.OnSpeedChanged += HandleSpeedChanged;
        _currentSpeed = SpeedFor(_leverController.Current);
    }

    private void OnDestroy()
    {
        if (_leverController != null)
            _leverController.OnSpeedChanged -= HandleSpeedChanged;
    }

    private void Update()
    {
        _dial.Rotate(_axis, _currentSpeed * Time.deltaTime, Space.Self);
    }

    private void HandleSpeedChanged(SpeedState state) => _currentSpeed = SpeedFor(state);

    private float SpeedFor(SpeedState state) => state switch
    {
        SpeedState.Slow   => _slowSpeed,
        SpeedState.Normal => _normalSpeed,
        SpeedState.Fast   => _fastSpeed,
        _                 => _normalSpeed
    };
}
