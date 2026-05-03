using UnityEngine;

public class TimedInteractionZone : InteractionZone
{
    [Header("Time Window")]
    [SerializeField, Range(0, 23)] private int startHour = 21;
    [SerializeField, Range(0, 24)] private int endHour   = 24;

    private bool _playerInside;

    private void OnEnable()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.Service.OnHourChange += HandleHourChange;
    }

    private void OnDisable()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.Service.OnHourChange -= HandleHourChange;
    }

    protected override void HandleEntered(Collider other)
    {
        _playerInside = true;

        if (IsWithinTimeWindow())
            base.HandleEntered(other);
    }

    protected override void HandleExited(Collider other)
    {
        _playerInside = false;

        if (IsWithinTimeWindow())
            base.HandleExited(other);
    }

    private void HandleHourChange()
    {
        if (_playerInside && IsWithinTimeWindow())
            base.HandleEntered(null);
    }

    private bool IsWithinTimeWindow()
    {
        if (TimeManager.Instance == null) return false;

        int hour = TimeManager.Instance.Service.CurrentTime.Hour;

        if (startHour <= endHour)
            return hour >= startHour && hour < endHour;

        return hour >= startHour || hour < endHour;
    }
}