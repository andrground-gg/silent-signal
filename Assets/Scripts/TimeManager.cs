using TMPro;
using UnityEngine;

public class TimeManager : Singleton<TimeManager> {
    [SerializeField] TimeSettings timeSettings;
    [SerializeField] TextMeshProUGUI timeText;

    public TimeService Service { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        Service = new TimeService(timeSettings);
    }

    void Update() {
        Service.UpdateTime(Time.deltaTime);
        if (timeText != null) timeText.text = Service.CurrentTime.ToString("HH:mm");
    }
}