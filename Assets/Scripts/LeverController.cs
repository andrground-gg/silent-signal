using System;
using UnityEngine;

public class LeverController : MonoBehaviour
{
    [SerializeField] private LeverAnimator leverAnimator;
    [SerializeField] private SpeedState    defaultState = SpeedState.Normal;

    public event Action<SpeedState> OnSpeedChanged;

    public SpeedState Current { get; private set; }

    private void Awake()
    {
        // Snap all levers to released position before animating the default
        int count = Enum.GetValues(typeof(SpeedState)).Length;
        for (int i = 0; i < count; i++)
            leverAnimator.SnapToDefault(i);
    }

    private void Start()
    {
        // Pull the default lever without firing the event — initial state only
        Current = defaultState;
        leverAnimator.AnimatePull((int)defaultState);
    }

    /// <summary>Called by each lever's UnityEvent (OnClick / trigger).</summary>
    public void SetSpeed(SpeedState newState)
    {
        if (newState == Current) return;

        leverAnimator.AnimateRelease((int)Current);
        leverAnimator.AnimatePull((int)newState);

        Current = newState;
        OnSpeedChanged?.Invoke(newState);
    }

    // Convenience wrappers so UnityEvent buttons can call these directly
    public void SetSlow()   => SetSpeed(SpeedState.Slow);
    public void SetNormal() => SetSpeed(SpeedState.Normal);
    public void SetFast()   => SetSpeed(SpeedState.Fast);
}
