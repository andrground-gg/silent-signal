using DG.Tweening;
using UnityEngine;

public class LeverAnimator : MonoBehaviour
{
    [SerializeField] private Transform[] levers;

    [Header("Animation")]
    [SerializeField] private float pullDuration  = 0.25f;
    [SerializeField] private float releaseDuration = 0.2f;
    [SerializeField] private float pulledAngle   = 30f;   // local X rotation when down
    [SerializeField] private float releasedAngle = -30f;  // local X rotation when up
    [SerializeField] private Ease  pullEase    = Ease.OutBack;
    [SerializeField] private Ease  releaseEase = Ease.OutSine;

    private Tween[] _tweens;

    private void Awake()
    {
        _tweens = new Tween[levers.Length];
    }

    public void AnimatePull(int index)
    {
        if (!IsValid(index)) return;
        KillTween(index);
        _tweens[index] = levers[index]
            .DOLocalRotate(new Vector3(pulledAngle, 0f, 0f), pullDuration)
            .SetEase(pullEase);
    }

    public void AnimateRelease(int index)
    {
        if (!IsValid(index)) return;
        KillTween(index);
        _tweens[index] = levers[index]
            .DOLocalRotate(new Vector3(releasedAngle, 0f, 0f), releaseDuration)
            .SetEase(releaseEase);
    }

    public void SnapToDefault(int index)
    {
        if (!IsValid(index)) return;
        KillTween(index);
        levers[index].localRotation = Quaternion.Euler(releasedAngle, 0f, 0f);
    }

    private void KillTween(int index) => _tweens[index]?.Kill();

    private bool IsValid(int index) => index >= 0 && index < levers.Length;
}
