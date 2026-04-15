using DG.Tweening;
using UnityEngine;

public class Lever : BaseInteractable
{
    [SerializeField] private LeverController controller;
    [SerializeField] private Transform levelPart;
    [SerializeField] private SpeedState      speedState;

    [Header("Animation")]
    [SerializeField] private float pulledAngle   =  30f;
    [SerializeField] private float releasedAngle = -30f;
    [SerializeField] private float pullDuration   = 0.25f;
    [SerializeField] private float releaseDuration = 0.2f;
    [SerializeField] private Ease  pullEase    = Ease.OutBack;
    [SerializeField] private Ease  releaseEase = Ease.OutSine;

    private Tween _tween;

    public SpeedState SpeedState => speedState;

    public override void Interact()
    {
        base.Interact();
        controller.SetSpeed(speedState);
    }

    public void AnimatePull()
    {
        KillTween();
        _tween = levelPart
            .DOLocalRotate(new Vector3(pulledAngle, 0f, 0f), pullDuration)
            .SetEase(pullEase);
    }

    public void AnimateRelease()
    {
        KillTween();
        _tween = levelPart
            .DOLocalRotate(new Vector3(releasedAngle, 0f, 0f), releaseDuration)
            .SetEase(releaseEase);
    }

    public void SnapToReleased()
    {
        KillTween();
        levelPart.localRotation = Quaternion.Euler(releasedAngle, 0f, 0f);
    }

    private void KillTween() => _tween?.Kill();
}