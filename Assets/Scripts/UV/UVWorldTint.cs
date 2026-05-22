using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies a subtle full-screen bluish-purple overlay when UV mode is active.
/// Add to a Screen Space - Overlay Canvas. Assign a UI Image that covers the
/// full screen (set to stretch in all directions). Set Ray Cast Target to false
/// on the Image so it doesn't block clicks.
/// </summary>
public class UVWorldTint : MonoBehaviour
{
    [SerializeField] private Image tintImage;

    [Tooltip("The tint color. Keep alpha very low (0.05–0.12) to stay subtle.")]
    [SerializeField] private Color uvTintColor = new Color(0.28f, 0.08f, 0.55f, 0.08f);

    [SerializeField] private float fadeDuration = 1f;

    private Tween _tween;

    private void Awake()
    {
        // Start fully transparent, preserving the RGB so DOFade has the right target color
        Color c = uvTintColor;
        c.a = 0f;
        tintImage.color = c;
    }

    private void Start()
    {
        UVWorldState.Instance.OnUVStateChanged += OnUVStateChanged;
    }

    private void OnDisable()
    {
        if (UVWorldState.Instance != null)
            UVWorldState.Instance.OnUVStateChanged -= OnUVStateChanged;
    }

    private void OnUVStateChanged(bool isActive)
    {
        _tween?.Kill();
        float targetAlpha = isActive ? uvTintColor.a : 0f;
        _tween = tintImage.DOFade(targetAlpha, fadeDuration).SetLink(gameObject);
    }

    private void OnDestroy() => _tween?.Kill();
}