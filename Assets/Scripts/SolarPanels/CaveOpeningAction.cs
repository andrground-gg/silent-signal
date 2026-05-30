using DG.Tweening;
using UnityEngine;

public class CaveOpeningAction : SolarPanelAction
{
    [Header("Door")]
    [SerializeField] private Transform door;
    [SerializeField] private float     dropDistance = 4f;
    [SerializeField] private float     dropDuration = 1.8f;
    [SerializeField] private Ease      dropEase     = Ease.InCubic;

    [Header("Shake Before Drop")]
    [SerializeField] private float preShakeDuration = 1.2f;
    [SerializeField] private float preShakeStrength = 0.15f;
    [SerializeField] private int   preShakeVibrato  = 30;

    [Header("Shake During Drop")]
    [SerializeField] private float dropShakeStrength = 3f;
    [SerializeField] private int   dropShakeVibrato  = 20;

    [Header("Audio")]
    [SerializeField] private AudioSource rumbleSound;

    public override SolarPanelActionType ActionType => SolarPanelActionType.CaveOpening;

    public override void Execute()
    {
        if (door == null) return;

        Vector3 startPos  = door.localPosition;
        Vector3 targetPos = startPos + Vector3.down * dropDistance;

        if (rumbleSound != null)
        {
            rumbleSound.loop = true;
            rumbleSound.Play();
        }

        DOTween.Sequence()
            .Append(door.DOShakePosition(preShakeDuration, preShakeStrength, preShakeVibrato, 90f, false, true))
            .Append(door.DOLocalMove(targetPos, dropDuration).SetEase(dropEase))
            .Join(door.DOShakeRotation(dropDuration, dropShakeStrength, dropShakeVibrato, 90f, false))
            .OnComplete(() =>
            {
                if (rumbleSound != null)
                {
                    rumbleSound.loop = false;
                    rumbleSound.Stop();
                }
            })
            .SetLink(door.gameObject);
    }
}
