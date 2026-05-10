using System;
using UnityEngine;

public class TimedInteractionZone : InteractionZone
{
    [Header("Time Window")]
    [SerializeField, Range(0, 23)] private int startHour = 21;
    [SerializeField, Range(0, 24)] private int endHour   = 24;

    [SerializeField] private AudioSource triggerSound;

    public event Action OnTimedWindowStarted;
    public event Action OnTimedWindowExpired;

    private bool _playerInside;
    private bool _wasWithinWindow;
    private bool _hasTriggeredWithinWindow;

    private void OnDisable()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.Service.OnHourChange -= HandleHourChange;
    }

    private void Start()
    {
        TimeManager.Instance.Service.OnHourChange += HandleHourChange;
        _wasWithinWindow = IsWithinTimeWindow();
        _hasTriggeredWithinWindow = false;
    }

    protected override void HandleEntered(Collider other)
    {
        _playerInside = true;

        if (IsWithinTimeWindow() && !_hasTriggeredWithinWindow)
        {
            base.HandleEntered(other);
            _hasTriggeredWithinWindow = true;

            if (triggerSound != null)
                triggerSound.Play();
        }
    }

    protected override void HandleExited(Collider other)
    {
        _playerInside = false;

        if (IsWithinTimeWindow() && _hasTriggeredWithinWindow)
            base.HandleExited(other);

        _hasTriggeredWithinWindow = false;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(tag)) return;

        _playerInside = true;

        if (IsWithinTimeWindow() && !_hasTriggeredWithinWindow)
        {
            base.HandleEntered(other);

            if (triggerSound != null)
                triggerSound.Play();
                
            _hasTriggeredWithinWindow = true;
        }
    }

    private void HandleHourChange()
    {
        bool isWithinNow = IsWithinTimeWindow();

        if (!_wasWithinWindow && isWithinNow)
            OnTimedWindowStarted?.Invoke();
        else if (_wasWithinWindow && !isWithinNow)
            OnTimedWindowExpired?.Invoke();

        _wasWithinWindow = isWithinNow;

        if (_playerInside && isWithinNow && !_hasTriggeredWithinWindow)
        {
            base.HandleEntered(null);
            _hasTriggeredWithinWindow = true;
        }

        if (!isWithinNow)
            _hasTriggeredWithinWindow = false;
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