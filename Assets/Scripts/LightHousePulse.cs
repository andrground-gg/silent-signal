using DG.Tweening;
using UnityEngine;

public class LightHousePulse : MonoBehaviour
{
    private static readonly int EmissionColor =
        Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Renderer targetRenderer;

    [SerializeField] private float boostedIntensity = 6f;
    [SerializeField] private float tweenDuration = 0.5f;

    private Material _mat;

    private Color _baseEmissionColor;
    private float _originalIntensity;
    private Tween _tween;

    private void Awake()
    {
        _mat = new Material(targetRenderer.sharedMaterial);
        targetRenderer.material = _mat;

        _mat.EnableKeyword("_EMISSION");

        Color emission = _mat.GetColor(EmissionColor);

        // HDR color intensity
        _originalIntensity = emission.maxColorComponent;

        if (_originalIntensity <= 0.0001f)
            _originalIntensity = 1f;

        _baseEmissionColor = emission / _originalIntensity;
    }

    public void BoostEmission()
    {
        AnimateIntensity(boostedIntensity);
    }

    public void RestoreEmission()
    {
        AnimateIntensity(_originalIntensity);
    }

    private void AnimateIntensity(float targetIntensity)
    {
        _tween?.Kill();

        float currentIntensity =
            _mat.GetColor(EmissionColor).maxColorComponent;

        _tween = DOTween.To(
            () => currentIntensity,
            x =>
            {
                currentIntensity = x;
                _mat.SetColor(
                    EmissionColor,
                    _baseEmissionColor * currentIntensity
                );
            },
            targetIntensity,
            tweenDuration
        );
    }
}