using DG.Tweening;
using UnityEngine;

/// <summary>
/// Place on any UV clue object (arrow, symbol, warning mark, note).
/// Hidden in normal light, fades in when UV mode is active.
///
/// Material must use a transparency-enabled shader (Standard in Fade/Transparent mode).
/// If markingRenderers is left empty, all Renderer children are used automatically.
/// </summary>
public class UVMarkingObject : MonoBehaviour
{
    [Tooltip("Leave empty to auto-detect all Renderer components on this object and its children.")]
    [SerializeField] private Renderer[] markingRenderers;

    [SerializeField] private float fadeDuration = 1f;

    private Material[] _mats;
    private Tween[]    _tweens;
    private float[]    _alphas;

    private void Awake()
    {
        if (markingRenderers == null || markingRenderers.Length == 0)
            markingRenderers = GetComponentsInChildren<Renderer>(true);

        _mats   = new Material[markingRenderers.Length];
        _tweens = new Tween[markingRenderers.Length];
        _alphas = new float[markingRenderers.Length];

        for (int i = 0; i < markingRenderers.Length; i++)
        {
            _mats[i] = markingRenderers[i].material;
            _alphas[i] = 0f;
            SetAlpha(_mats[i], 0f);
        }
    }

    private void Start()
    {
        UVWorldState.Instance.OnUVStateChanged += OnUVStateChanged;

        // Animate in if UV was already active when this object spawned
        if (UVWorldState.Instance.IsUVActive)
            OnUVStateChanged(true);
    }

    private void OnDisable()
    {
        if (UVWorldState.Instance != null)
            UVWorldState.Instance.OnUVStateChanged -= OnUVStateChanged;
    }

    private void OnUVStateChanged(bool isActive)
    {
        float target = isActive ? 1f : 0f;
        for (int i = 0; i < _mats.Length; i++)
            AnimateAlpha(i, target);
    }

    private void AnimateAlpha(int index, float targetAlpha)
    {
        _tweens[index]?.Kill();
        int capturedIndex = index;
        Material mat = _mats[index];
        _tweens[index] = DOTween.To(
            () => _alphas[capturedIndex],
            a  =>
            {
                _alphas[capturedIndex] = a;
                SetAlpha(mat, a);
            },
            targetAlpha,
            fadeDuration
        ).SetLink(gameObject);
    }

    private void SetAlpha(Material mat, float alpha)
    {
        Color color = mat.color;
        color.a = alpha;
        mat.color = color;
    }

    private void OnDestroy()
    {
        foreach (var t in _tweens)
            t?.Kill();
    }
}
