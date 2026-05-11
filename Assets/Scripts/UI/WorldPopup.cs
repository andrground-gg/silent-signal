using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class WorldPopup : MonoBehaviour
{
    [SerializeField] private InteractionZone interactionZone;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 0.25f;
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private Ease fadeInEase = Ease.OutQuad;
    [SerializeField] private Ease fadeOutEase = Ease.InQuad;

    [Tooltip("If true, the CanvasGroup blocks raycasts only while visible.")]
    [SerializeField] private bool toggleInteractable = true;

    private CanvasGroup _canvasGroup;
    private Tween _fadeTween;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        SetInteractable(false);
    }

    private void Start()
    {
        interactionZone.OnEnteredInteractionZone += OnEnteredInteractionZone;
        interactionZone.OnExitedInteractionZone += OnExitedInteractionZone;
    }

    private void OnDisable()
    {
        if (interactionZone != null)
        {
            interactionZone.OnEnteredInteractionZone -= OnEnteredInteractionZone;
            interactionZone.OnExitedInteractionZone -= OnExitedInteractionZone;
        }

        _fadeTween?.Kill();
    }

    private void OnEnteredInteractionZone()
    {
        Show();
    }

    private void OnExitedInteractionZone()
    {
        Hide();
    }

    private void Show()
    {
        _fadeTween?.Kill();

        if (toggleInteractable) SetInteractable(true);

        _fadeTween = _canvasGroup
            .DOFade(1f, fadeInDuration)
            .SetEase(fadeInEase)
            .SetLink(gameObject);
    }

    private void Hide()
    {
        _fadeTween?.Kill();

        _fadeTween = _canvasGroup
            .DOFade(0f, fadeOutDuration)
            .SetEase(fadeOutEase)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                if (toggleInteractable) SetInteractable(false);
            });
    }

    private void SetInteractable(bool value)
    {
        _canvasGroup.interactable = value;
        _canvasGroup.blocksRaycasts = value;
    }
}