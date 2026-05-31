using DG.Tweening;
using UnityEngine;

public class LightHousePulse : MonoBehaviour
{
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Renderer targetRenderer;

    [Header("UV Mode")]
    [SerializeField] private Color uvColor        = new Color(0.4f, 0.1f, 1.0f);
    [SerializeField] private float uvColorDuration = 1f;

    private Material _mat;
    private bool     _hasEmission;
    private bool     _hasBaseColor;

    private Color _originalEmission;
    private Color _originalBaseColor;
    private Color _currentEmission;
    private Color _currentBaseColor;
    private float _currentMultiplier = 1f;

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

    public void SetMultiplierDirect(float multiplier)
    {
        _currentMultiplier = multiplier;
        ApplyEmission();
    }

    private void OnUVStateChanged(bool isUV)
    {
        Color emissionTarget  = isUV ? uvColor : _originalEmission;
        Color baseColorTarget = isUV ? uvColor : _originalBaseColor;

        _colorTween?.Kill();

        Color emissionStart  = _currentEmission;
        Color baseColorStart = _currentBaseColor;

        float progress = 0f;
        _colorTween = DOTween.To(
            () => progress,
            t =>
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

    private void ApplyEmission()
    {
        if (_hasEmission)
            _mat.SetColor(EmissionColor, _currentEmission * _currentMultiplier);
    }

    private void OnDestroy() => _colorTween?.Kill();
}
