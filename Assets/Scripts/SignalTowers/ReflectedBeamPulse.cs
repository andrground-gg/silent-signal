using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ReflectedBeamPulse : MonoBehaviour
{
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Renderer targetRenderer;

    [Header("UV Mode")]
    [SerializeField] private Color uvColor         = new Color(0.4f, 0.1f, 1.0f);
    [SerializeField] private float uvColorDuration = 1f;

    private Material _mat;
    private bool     _initialized;
    private bool     _hasEmission;
    private bool     _hasBaseColor;

    private Color _originalEmission;
    private Color _originalBaseColor;
    private Color _currentEmission;
    private Color _currentBaseColor;

    private Tween _colorTween;

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();

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

    private void OnEnable()
    {
        EnsureInitialized();

        if (UVWorldState.Instance != null)
        {
            UVWorldState.Instance.OnUVStateChanged += OnUVStateChanged;
            // Промінь щойно активувався — одразу застосувати поточний UV-стан без анімації.
            ApplyState(UVWorldState.Instance.IsUVActive, instant: true);
        }
    }

    private void OnDisable()
    {
        if (UVWorldState.Instance != null)
            UVWorldState.Instance.OnUVStateChanged -= OnUVStateChanged;

        _colorTween?.Kill();
    }

    private void OnUVStateChanged(bool isUV) => ApplyState(isUV, instant: false);

    private void ApplyState(bool isUV, bool instant)
    {
        Color emissionTarget  = isUV ? uvColor : _originalEmission;
        Color baseColorTarget = isUV ? uvColor : _originalBaseColor;

        _colorTween?.Kill();

        if (instant)
        {
            if (_hasEmission)
            {
                _currentEmission = emissionTarget;
                _mat.SetColor(EmissionColor, _currentEmission);
            }
            if (_hasBaseColor)
            {
                _currentBaseColor = baseColorTarget;
                _mat.color        = _currentBaseColor;
            }
            return;
        }

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
                    _mat.SetColor(EmissionColor, _currentEmission);
                }
                if (_hasBaseColor)
                {
                    _currentBaseColor = Color.Lerp(baseColorStart, baseColorTarget, t);
                    _mat.color        = _currentBaseColor;
                }
            },
            1f,
            uvColorDuration
        ).SetLink(gameObject);
    }

    private void OnDestroy()
    {
        _colorTween?.Kill();
        if (_mat != null) Destroy(_mat);
    }
}
