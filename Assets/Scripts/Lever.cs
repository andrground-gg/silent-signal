using System;
using DG.Tweening;
using UnityEngine;

public class Lever : BaseInteractable
{
    [SerializeField] private Transform levelPart;

    [Header("Animation")]
    [SerializeField] private Vector3 pulledAngle    = Vector3.zero;
    [SerializeField] private Vector3 releasedAngle  = Vector3.zero;
    [SerializeField] private float pullDuration   = 0.25f;
    [SerializeField] private float releaseDuration = 0.2f;
    [SerializeField] private Ease  pullEase    = Ease.OutBack;
    [SerializeField] private Ease  releaseEase = Ease.OutSine;
    private bool _isPulled;
    private Tween _tween;

    public event Action OnPulled;
    public event Action OnRelease;

    
    public override void Interact()
    {
        base.Interact();
        if (_isPulled)
            AnimateRelease();
        else
            AnimatePull();
    }

    public void AnimatePull()
    {
        _isPulled = true;
        KillTween();
        _tween = levelPart
            .DOLocalRotate(pulledAngle, pullDuration)
            .SetEase(pullEase)
            .OnComplete(() => OnPulled?.Invoke());
    }

    public void AnimateRelease()
    {
        _isPulled = false;
        KillTween();
        _tween = levelPart
            .DOLocalRotate(releasedAngle, releaseDuration)
            .SetEase(releaseEase)
            .OnComplete(() => OnRelease?.Invoke());
    }

    public void SnapToReleased()
    {
        _isPulled = false;
        KillTween();
        levelPart.localRotation = Quaternion.Euler(releasedAngle);
    }

    private void KillTween() => _tween?.Kill();
}