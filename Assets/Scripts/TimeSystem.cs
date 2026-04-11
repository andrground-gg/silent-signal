using System;
using UnityEngine;

public class TimeSystem : Singleton<TimeSystem>
{
    [Header("Settings")]
    [SerializeField, Range(0.01f, 100f)] private float timeScale = 0.1f;
    [SerializeField, Range(0f, 24f)]     private float startTime = 8f;

    public event Action<int> OnHourChanged;

    private float _timeOfDay;
    private int   _lastHour = -1;

    // --- Public API ---

    /// <summary>Поточний час у форматі "ГГ:ХХ".</summary>
    public string CurrentTime => FormatTime(_timeOfDay);

    /// <summary>Поточна година (0–23).</summary>
    public int Hours => Mathf.FloorToInt(_timeOfDay);

    /// <summary>Поточні хвилини (0–59).</summary>
    public int Minutes => Mathf.FloorToInt((_timeOfDay % 1f) * 60f);

    /// <summary>Нормалізований прогрес дня (0.0 = північ, 0.5 = полудень).</summary>
    public float DayProgress => _timeOfDay / 24f;

    // ------------------

    private void Start()
    {
        _timeOfDay = Mathf.Clamp(startTime, 0f, 23.999f);
    }

    private void Update()
    {
        _timeOfDay += Time.deltaTime * timeScale;

        if (_timeOfDay >= 24f)
            _timeOfDay -= 24f;

        int currentHour = Mathf.FloorToInt(_timeOfDay);

        if (currentHour != _lastHour)
        {
            _lastHour = currentHour;
            OnHourChanged?.Invoke(currentHour);
        }
    }

    // --- Helpers ---

    private static string FormatTime(float time)
    {
        int h = Mathf.FloorToInt(time);
        int m = Mathf.FloorToInt((time % 1f) * 60f);
        return $"{h:D2}:{m:D2}";
    }
}