using System;
using UnityEngine;

public class LeverController : MonoBehaviour
{
    [SerializeField] private Lever[]    levers;
    [SerializeField] private SpeedState defaultState = SpeedState.Normal;

    public event Action<SpeedState> OnSpeedChanged;

    public SpeedState Current { get; private set; }

    private void Awake()
    {
        foreach (var lever in levers)
            lever.SnapToReleased();
    }

    private void Start()
    {
        Current = defaultState;
        GetLever(defaultState)?.AnimatePull();
    }

    public void SetSpeed(SpeedState newState)
    {
        if (newState == Current) return;

        GetLever(Current)?.AnimateRelease();
        GetLever(newState)?.AnimatePull();

        Current = newState;
        OnSpeedChanged?.Invoke(newState);
    }

    private Lever GetLever(SpeedState state)
    {
        foreach (var lever in levers)
            if (lever.SpeedState == state) return lever;

        Debug.LogWarning($"[LeverController] No lever found for state {state}");
        return null;
    }
}