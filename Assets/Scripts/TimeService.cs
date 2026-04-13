using System;

public class TimeService {
    readonly TimeSettings settings;
    DateTime currentTime;
    readonly TimeSpan sunriseTime;
    readonly TimeSpan sunsetTime;

    public DateTime CurrentTime => currentTime;
    public float NormalizedTime => (float)(currentTime.TimeOfDay.TotalHours / 24.0);

    public event Action OnSunrise = delegate { };
    public event Action OnSunset = delegate { };
    public event Action OnHourChange = delegate { };
    public event Action<float> OnTimeChanged = delegate { };

    readonly Observable<bool> isDayTime;
    readonly Observable<int> currentHour;

    public TimeService(TimeSettings settings) {
        this.settings = settings;
        currentTime = DateTime.Now.Date + TimeSpan.FromHours(settings.startHour);
        sunriseTime = TimeSpan.FromHours(settings.sunriseHour);
        sunsetTime  = TimeSpan.FromHours(settings.sunsetHour);

        isDayTime   = new Observable<bool>(IsDayTime());
        currentHour = new Observable<int>(currentTime.Hour);

        isDayTime.ValueChanged   += day => (day ? OnSunrise : OnSunset).Invoke();
        currentHour.ValueChanged += _   => OnHourChange.Invoke();
    }

    public void UpdateTime(float deltaTime) {
        currentTime = currentTime.AddSeconds(deltaTime * settings.timeMultiplier);
        isDayTime.Value   = IsDayTime();
        currentHour.Value = currentTime.Hour;
        OnTimeChanged.Invoke(NormalizedTime);
    }

    bool IsDayTime() => currentTime.TimeOfDay > sunriseTime && currentTime.TimeOfDay < sunsetTime;

    TimeSpan CalculateDifference(TimeSpan from, TimeSpan to) {
        TimeSpan difference = to - from;
        return difference.TotalHours < 0 ? difference + TimeSpan.FromHours(24) : difference;
    }
}