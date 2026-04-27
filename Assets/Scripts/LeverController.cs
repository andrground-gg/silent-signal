using System;
using UnityEngine;

public class LeverController : Singleton<LeverController>
{
    [Serializable]
    public class LeverEntry
    {
        public Lever      lever;
        public SpeedState speedState;
    }

    [SerializeField] private LeverEntry[] leverEntries;
    [SerializeField] private SpeedState   defaultState = SpeedState.Normal;
    
    public event Action<SpeedState> OnSpeedChanged;

    public SpeedState Current { get; private set; }

#if UNITY_EDITOR
    [ContextMenu("Find All Levers")]
    private void FindAllLevers()
    {
        var found = FindObjectsByType<Lever>(FindObjectsSortMode.None);
        leverEntries = new LeverEntry[found.Length];
        for (int i = 0; i < found.Length; i++)
        {
            leverEntries[i] = new LeverEntry
            {
                lever      = found[i],
                speedState = defaultState
            };
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void Start()
    {
        Current = defaultState;

        foreach (var entry in leverEntries)
        {
            entry.lever.OnPulled  += () => SetSpeed(entry.speedState);
            entry.lever.OnRelease += () => { };
        }

        GetEntry(defaultState)?.lever?.AnimatePull();
    }

    public void SetSpeedByLever(Lever lever)
    {
        var entry = GetEntryByLever(lever);
        if (entry == null)
        {
            Debug.LogWarning($"[LeverController] Lever {lever.name} is not registered.");
            return;
        }
        SetSpeed(entry.speedState);
    }

    public void SetSpeed(SpeedState newState)
    {
        if (newState == Current) return;

        var currentEntry = GetEntry(Current);
        if (currentEntry != null)
        {
            currentEntry.lever?.AnimateRelease();
        }

        var newEntry = GetEntry(newState);
        if (newEntry != null)
        {
            newEntry.lever?.AnimatePull();
        }

        Current = newState;
        OnSpeedChanged?.Invoke(newState);
    }

    private LeverEntry GetEntry(SpeedState state)
    {
        foreach (var entry in leverEntries)
            if (entry.speedState == state) return entry;

        Debug.LogWarning($"[LeverController] No entry found for state {state}");
        return null;
    }

    private LeverEntry GetEntryByLever(Lever lever)
    {
        foreach (var entry in leverEntries)
            if (entry.lever == lever) return entry;
        return null;
    }
}