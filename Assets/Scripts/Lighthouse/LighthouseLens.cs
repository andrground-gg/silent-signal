using System;
using UnityEngine;

public class LighthouseLens : HoldInteractable
{
    [Header("Cleanliness")]
    [SerializeField] private bool  startClean        = true;
    [SerializeField] private float secondsUntilDirty = 300f;
    [SerializeField] private float holdDuration      = 2f;

    [Header("Visual")]
    [SerializeField] private GameObject dirtObject;

    public bool IsClean { get; private set; }

    public event Action<bool> OnCleanStateChanged;

    private float _timeSinceClean;

    public override float HoldDuration => holdDuration;
    public override bool  CanHold      => !IsClean;

    public override void OnHoverEnter()
    {
        if (!IsClean) base.OnHoverEnter();
    }

    protected override void Awake()
    {
        base.Awake();
        IsClean = startClean;
        if (dirtObject != null) dirtObject.SetActive(!startClean);
    }

    private void Update()
    {
        if (!IsClean) return;

        _timeSinceClean += Time.deltaTime;

        if (_timeSinceClean >= secondsUntilDirty)
        {
            IsClean = false;
            if (dirtObject != null) dirtObject.SetActive(true);
            OnCleanStateChanged?.Invoke(false);
        }
    }

    public override void OnHoldComplete()
    {
        IsClean = true;
        _timeSinceClean = 0f;
        if (dirtObject != null) dirtObject.SetActive(false);
        OnCleanStateChanged?.Invoke(true);
    }
}
