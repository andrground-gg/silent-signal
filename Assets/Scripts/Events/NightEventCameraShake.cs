using UnityEngine;
using DG.Tweening;

public class NightEventCameraShake : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Initial Punch")]
    [SerializeField] private float punchStrength = 0.8f;
    [SerializeField] private float punchDuration = 0.6f;
    [SerializeField, Range(1, 20)] private int punchVibrato = 12;
    [SerializeField, Range(0f, 1f)] private float punchElasticity = 0.8f;

    [Header("Sustained Tremor")]
    [SerializeField] private float tremorStrength = 0.25f;
    [SerializeField] private float tremorDuration = 4f;
    [SerializeField, Range(1, 50)] private int tremorVibrato = 25;
    [SerializeField, Range(0f, 180f)] private float tremorRandomness = 90f;

    private Vector3 _originalLocalPosition;
    private Sequence _shakeSequence;

    private void Awake()
    {
        if (cameraTransform == null)
            cameraTransform = Camera.main != null ? Camera.main.transform : null;

        if (cameraTransform != null)
            _originalLocalPosition = cameraTransform.localPosition;
    }

    private void Start()
    {
        if (NightEventManager.Instance != null)
            NightEventManager.Instance.OnNightEventTriggeredWithEffects += HandleNightEvent;
    }

    private void OnDisable()
    {
        if (NightEventManager.Instance != null)
            NightEventManager.Instance.OnNightEventTriggeredWithEffects -= HandleNightEvent;

        _shakeSequence?.Kill();

        if (cameraTransform != null)
            cameraTransform.localPosition = _originalLocalPosition;
    }

    private void HandleNightEvent()
    {
        if (cameraTransform == null)
        {
            Debug.LogWarning("[NightEventCameraShake] Camera transform not assigned.", this);
            return;
        }

        _shakeSequence?.Kill();
        cameraTransform.localPosition = _originalLocalPosition;

        _shakeSequence = DOTween.Sequence()
            .Append(cameraTransform.DOPunchPosition(
                punch: Random.insideUnitSphere.normalized * punchStrength,
                duration: punchDuration,
                vibrato: punchVibrato,
                elasticity: punchElasticity))
            .Append(cameraTransform.DOShakePosition(
                duration: tremorDuration,
                strength: tremorStrength,
                vibrato: tremorVibrato,
                randomness: tremorRandomness,
                snapping: false,
                fadeOut: true))
            .OnComplete(() => cameraTransform.localPosition = _originalLocalPosition)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }
    
    [ContextMenu("Test Shake")]
    private void TestShake()
    {
        HandleNightEvent();
    }
}