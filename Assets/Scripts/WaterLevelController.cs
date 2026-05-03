using UnityEngine;
using DG.Tweening;

public class WaterLevelController : MonoBehaviour
{
    private const string PREF_FIRST_DROP_TRIGGERED = "WaterLevel.FirstDropTriggered";

    [Header("References")]
    [SerializeField] private Transform waterTransform;
    [SerializeField] private TimedInteractionZone keepersDenZone;

    [Header("Level Anchors")]
    [SerializeField] private Transform slowLevelAnchor;
    [SerializeField] private Transform normalLevelAnchor;
    [SerializeField] private Transform fastLevelAnchor;

    [Header("Transition")]
    [SerializeField, Range(10f, 30f)] private float transitionDuration = 15f;
    [SerializeField] private Ease transitionEase = Ease.InOutSine;

    private SpeedState currentState = SpeedState.Normal;
    private SpeedState pendingState = SpeedState.Normal;
    private Tween activeTween;

    private bool _firstDropUnlocked;

    private void Start()
    {
        _firstDropUnlocked = PlayerPrefs.GetInt(PREF_FIRST_DROP_TRIGGERED, 0) == 1;

        LighthouseManager.Instance.OnSpeedChanged += HandleLighthouseSpeedChanged;
        keepersDenZone.OnEnteredInteractionZone += HandlePlayerEnteredDen;

        Vector3 pos = waterTransform.position;
        pos.y = GetTargetLevelFor(currentState);
        waterTransform.position = pos;
    }

    private void OnDisable()
    {
        if (LighthouseManager.Instance != null)
            LighthouseManager.Instance.OnSpeedChanged -= HandleLighthouseSpeedChanged;

        if (keepersDenZone != null)
            keepersDenZone.OnEnteredInteractionZone -= HandlePlayerEnteredDen;
    }

    private void HandleLighthouseSpeedChanged(SpeedState newState)
    {
        pendingState = newState;

        SetState(newState);
    }

    private void HandlePlayerEnteredDen()
    {
        if (_firstDropUnlocked) return;

        _firstDropUnlocked = true;
        PlayerPrefs.SetInt(PREF_FIRST_DROP_TRIGGERED, 1);
        PlayerPrefs.Save();

        SetState(pendingState);
    }

    private void SetState(SpeedState newState)
    {
        if (newState == currentState && activeTween != null && activeTween.IsActive())
            return;

        currentState = newState;

        float targetY = GetTargetLevelFor(newState);

        activeTween?.Kill();

        activeTween = waterTransform
            .DOMoveY(targetY, transitionDuration)
            .SetEase(transitionEase)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() => activeTween = null);
    }

    private float GetTargetLevelFor(SpeedState state)
    {
        Transform anchor = state switch
        {
            SpeedState.Slow   => slowLevelAnchor,
            SpeedState.Normal => normalLevelAnchor,
            SpeedState.Fast   => fastLevelAnchor,
            _                 => normalLevelAnchor
        };

        if (anchor == null)
        {
            Debug.LogWarning($"[WaterLevelController] Anchor for state {state} is not assigned.", this);
            return waterTransform.position.y;
        }

        return anchor.position.y;
    }

    private void OnDestroy()
    {
        activeTween?.Kill();
    }
}