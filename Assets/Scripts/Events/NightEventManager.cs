using UnityEngine;

public class NightEventManager : Singleton<NightEventManager>
{
    private const string PREF_FIRST_NIGHT_SEEN = "WaterLevel.FirstDropTriggered";

    [Header("References")]
    [SerializeField] private TimedInteractionZone keepersDenZone;

    public event System.Action OnNightEventTriggered;
    public event System.Action OnNightEventTriggeredWithEffects;

    private bool _firstNightSeen;
    private bool _resolvedThisNight;

    private void Start()
    {
        _firstNightSeen = PlayerPrefs.GetInt(PREF_FIRST_NIGHT_SEEN, 0) == 1;

        keepersDenZone.OnTimedWindowStarted   += HandleZoneWindowStarted;
        keepersDenZone.OnEnteredInteractionZone += HandleZoneTriggered;
        keepersDenZone.OnTimedWindowExpired   += HandleZoneWindowExpired;
    }

    private void OnDisable()
    {
        if (keepersDenZone == null) return;

        keepersDenZone.OnTimedWindowStarted   -= HandleZoneWindowStarted;
        keepersDenZone.OnEnteredInteractionZone -= HandleZoneTriggered;
        keepersDenZone.OnTimedWindowExpired   -= HandleZoneWindowExpired;
    }

    private void HandleZoneWindowStarted()
    {
        _resolvedThisNight = false;

        if (_firstNightSeen)
        {
            TriggerSlow(withEffects: false);
            _resolvedThisNight = true;
        }
    }

    private void HandleZoneTriggered()
    {
        if (_firstNightSeen) return;
        if (_resolvedThisNight) return;

        TriggerSlow(withEffects: true);
        MarkFirstNightSeen();
        _resolvedThisNight = true;
    }

    private void HandleZoneWindowExpired()
    {
        if (_resolvedThisNight) return;
        if (_firstNightSeen) return;

        TriggerSlow(withEffects: false);
        _resolvedThisNight = true;
    }

    private void TriggerSlow(bool withEffects)
    {
        OnNightEventTriggered?.Invoke();
        if (withEffects)
            OnNightEventTriggeredWithEffects?.Invoke();
    }

    private void MarkFirstNightSeen()
    {
        _firstNightSeen = true;
        PlayerPrefs.SetInt(PREF_FIRST_NIGHT_SEEN, 1);
        PlayerPrefs.Save();
    }
}