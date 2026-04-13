using TMPro;
using UnityEngine;

public class TimeManager : MonoBehaviour {
    [SerializeField] TimeSettings timeSettings;
    [SerializeField] TextMeshProUGUI timeText;

    public TimeService Service { get; private set; }

    void Awake() => Service = new TimeService(timeSettings);

    void Update() {
        Service.UpdateTime(Time.deltaTime);
        if (timeText != null) timeText.text = Service.CurrentTime.ToString("hh:mm");
    }
}