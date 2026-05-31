using System;
using DG.Tweening;
using UnityEngine;

public class LighthouseLens : BaseInteractable
{
    [Header("Cleanliness")]
    [SerializeField] private bool  startClean        = true;
    [SerializeField] private float secondsUntilDirty = 300f;
    [SerializeField] private float cleanDuration      = 2f;

    [Header("Visual")]
    [SerializeField] private GameObject dirtObject;

    public bool IsClean { get; private set; }

    public event Action<bool> OnCleanStateChanged;

    private Tween _cleanTween;
    private float _timeSinceClean;

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

    public override void Interact()
    {
        base.Interact();

        canInteract = false;
        _cleanTween?.Kill();
        _cleanTween = DOVirtual.DelayedCall(cleanDuration, () =>
        {
            IsClean = true;
            _timeSinceClean = 0f;
            if (dirtObject != null) dirtObject.SetActive(false);
            OnCleanStateChanged?.Invoke(true);
            canInteract = true;
        }).SetLink(gameObject);
    }

    private void OnDestroy() => _cleanTween?.Kill();
}
