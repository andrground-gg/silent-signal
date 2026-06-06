using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class DiscoveryText : MonoBehaviour
{
    [SerializeField] private CanvasGroup discoveryText;

    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float blinkDuration = 0.15f;
    [SerializeField] private int blinkCount = 6;
    [SerializeField] private float visibleTime = 2f;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource discoverySound;

    Tween currentTween;

    public void ShowDiscovery()
    {
        currentTween?.Kill();
        discoveryText.alpha = 0f;

        if (discoverySound != null)
            discoverySound.Play();

        Sequence seq = DOTween.Sequence();

        // Fade in
        seq.Append(discoveryText.DOFade(1f, fadeDuration));

        // Blinking
        for (int i = 0; i < blinkCount; i++)
        {
            seq.Append(discoveryText.DOFade(0.2f, blinkDuration));
            seq.Append(discoveryText.DOFade(1f, blinkDuration));
        }

        // Stay visible
        seq.AppendInterval(visibleTime);

        // Fade out
        seq.Append(discoveryText.DOFade(0f, fadeDuration));

        seq.OnComplete(() =>
        {
            discoveryText.gameObject.SetActive(false);
        });

        currentTween = seq;
    }
}