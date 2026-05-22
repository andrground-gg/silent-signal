using DG.Tweening;
using UnityEngine;

public class LightHousePulse : MonoBehaviour
{
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Renderer targetRenderer;

    [Tooltip("Multiplier applied to the original emission. 1 = original, 2 = twice as bright, 0.5 = half.")]
    [SerializeField] private float boostMultiplier = 2f;
    [SerializeField] private float tweenDuration = 0.5f;

    [Header("UV Mode")]
    [Tooltip("Color when UV mode is active. Applied to both _EmissionColor and _Color (base), whichever the material has.")]
    [SerializeField] private Color uvColor = new Color(0.4f, 0.1f, 1.0f);
    [SerializeField] private float uvColorDuration = 1f;

    private Material _mat;

    private bool  _hasEmission;
    private bool  _hasBaseColor;

    private Color _originalEmission;
    private Color _originalBaseColor;

    private Color _currentEmission;
    private Color _currentBaseColor;
    private float _currentMultiplier = 1f;

    private Tween _multiplierTween;
    private Tween _colorTween;

    private void Awake()
    {
        _mat = new Material(targetRenderer.sharedMaterial);
        targetRenderer.material = _mat;

        _hasEmission  = _mat.HasProperty(EmissionColor);
        _hasBaseColor = _mat.HasProperty("_Color");

        if (_hasEmission)
        {
            _mat.EnableKeyword("_EMISSION");
            _originalEmission = _mat.GetColor(EmissionColor);
            _currentEmission  = _originalEmission;
        }

        if (_hasBaseColor)
        {
            _originalBaseColor = _mat.color;
            _currentBaseColor  = _originalBaseColor;
        }
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

    public void BoostEmission()   => AnimateToMultiplier(boostMultiplier);
    public void RestoreEmission() => AnimateToMultiplier(1f);

    public void SetIntensityMultiplier(float multiplier) => AnimateToMultiplier(multiplier);

    private void OnUVStateChanged(bool isUV)
    {
        Color emissionTarget  = isUV ? uvColor          : _originalEmission;
        Color baseColorTarget = isUV ? uvColor          : _originalBaseColor;

        _colorTween?.Kill();

        // Tween both properties in parallel via a single 0→1 progress tween
        Color emissionStart  = _currentEmission;
        Color baseColorStart = _currentBaseColor;

        float progress = 0f;
        _colorTween = DOTween.To(
            () => progress,
            t  =>
            {
                progress = t;
                if (_hasEmission)
                {
                    _currentEmission = Color.Lerp(emissionStart, emissionTarget, t);
                    ApplyEmission();
                }
                if (_hasBaseColor)
                {
                    _currentBaseColor = Color.Lerp(baseColorStart, baseColorTarget, t);
                    _mat.color = _currentBaseColor;
                }
            },
            1f,
            uvColorDuration
        ).SetLink(gameObject);
    }

    private void AnimateToMultiplier(float target)
    {
        _multiplierTween?.Kill();
        _multiplierTween = DOTween.To(
            () => _currentMultiplier,
            x  => { _currentMultiplier = x; ApplyEmission(); },
            target,
            tweenDuration
        ).SetLink(gameObject);
    }

    private void ApplyEmission()
    {
        if (_hasEmission)
            _mat.SetColor(EmissionColor, _currentEmission * _currentMultiplier);
    }

    private void OnDestroy()
    {
        _multiplierTween?.Kill();
        _colorTween?.Kill();
    }
}
