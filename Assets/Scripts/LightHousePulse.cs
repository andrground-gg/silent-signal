using DG.Tweening;
using UnityEngine;

public class LightHousePulse : MonoBehaviour
{
    private static readonly int EmissionColor =
        Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Renderer targetRenderer;

    [Tooltip("Multiplier applied to the original emission. 1 = original, 2 = twice as bright, 0.5 = half.")]
    [SerializeField] private float boostMultiplier = 2f;
    [SerializeField] private float tweenDuration = 0.5f;

    private Material _mat;

    // The emission color as authored in the material — used as the absolute reference.
    private Color _originalEmission;

    // Current multiplier being animated. 1 = original brightness.
    private float _currentMultiplier = 1f;

    private Tween _tween;

    private void Awake()
    {
        // Instance the material so we don't touch the asset
        _mat = new Material(targetRenderer.sharedMaterial);
        targetRenderer.material = _mat;
        _mat.EnableKeyword("_EMISSION");

        // Snapshot the authored HDR emission color — this is our 1.0 reference.
        // Includes exponential intensity already (Unity stores HDR colors as
        // pre-multiplied RGB where (1,1,1) at intensity 3 = (8,8,8)).
        _originalEmission = _mat.GetColor(EmissionColor);
    }

    public void BoostEmission()
    {
        AnimateToMultiplier(boostMultiplier);
    }

    public void RestoreEmission()
    {
        AnimateToMultiplier(1f);
    }

    /// <summary>Set custom multiplier. 1 = original, 1.1 = barely brighter, 2 = twice bright.</summary>
    public void SetIntensityMultiplier(float multiplier)
    {
        AnimateToMultiplier(multiplier);
    }

    private void AnimateToMultiplier(float target)
    {
        _tween?.Kill();

        _tween = DOTween.To(
            () => _currentMultiplier,
            x =>
            {
                _currentMultiplier = x;
                _mat.SetColor(EmissionColor, _originalEmission * x);
            },
            target,
            tweenDuration
        ).SetLink(gameObject);
    }

    private void OnDestroy()
    {
        _tween?.Kill();
    }
}