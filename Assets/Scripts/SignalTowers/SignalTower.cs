using System;
using DG.Tweening;
using GeneratorSystem;
using UnityEngine;

public class SignalTower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform rotatingHead;
    [SerializeField] private Renderer  indicatorRenderer;

    [Header("Rotation")]
    [SerializeField] private float rotationDuration = 1f;
    [SerializeField] private Ease  rotationEase     = Ease.InOutSine;

    [Header("Indicator")]
    [SerializeField] private Color offEmission = new Color(0.15f, 0f,    0f);
    [SerializeField] private Color onEmission  = new Color(0f,    0.25f, 0.05f);

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    private int  _state;   // 0–3  →  0° / 90° / 180° / 270°
    private bool _isRotating;
    private bool  _isFogBlocked;

    private MaterialPropertyBlock _mpb;

    public bool IsPowered  { get; private set; }
    public bool IsRotating => _isRotating;

    public bool IsFogBlocked
    {
        get => _isFogBlocked;
        set
        {
            if (_isFogBlocked == value) return;
            _isFogBlocked = value;
            UpdateIndicator();
            OnCanReflectChanged?.Invoke(CanReflect);
        }
    }

    public bool CanReflect => IsPowered && !_isFogBlocked && !_isRotating;

    public event Action<bool> OnCanReflectChanged;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    private void Start()
    {
        GeneratorManager.Instance.OnGeneratorActivated   += OnGeneratorActivated;
        GeneratorManager.Instance.OnGeneratorDeactivated += OnGeneratorDeactivated;

        IsPowered = GeneratorManager.Instance.IsActive(GeneratorID.GENERATOR_SIGNAL_TOWERS);
        UpdateIndicator();
    }

    private void OnDisable()
    {
        if (GeneratorManager.Instance == null) return;
        GeneratorManager.Instance.OnGeneratorActivated   -= OnGeneratorActivated;
        GeneratorManager.Instance.OnGeneratorDeactivated -= OnGeneratorDeactivated;
    }

    public void TryRotate()
    {
        if (_isRotating) return;

        _state = (_state + 1) % 4;

        _isRotating = true;
        OnCanReflectChanged?.Invoke(false);

        rotatingHead
            .DOLocalRotate(new Vector3(0f, 90f, 0f), rotationDuration, RotateMode.LocalAxisAdd)
            .SetEase(rotationEase)
            .OnComplete(() =>
            {
                _isRotating = false;
                OnCanReflectChanged?.Invoke(CanReflect);
            })
            .SetLink(gameObject);
    }

    private void OnGeneratorActivated(GeneratorID id)
    {
        if (id != GeneratorID.GENERATOR_SIGNAL_TOWERS) return;
        IsPowered = true;
        UpdateIndicator();
        OnCanReflectChanged?.Invoke(CanReflect);
    }

    private void OnGeneratorDeactivated(GeneratorID id)
    {
        if (id != GeneratorID.GENERATOR_SIGNAL_TOWERS) return;
        IsPowered = false;
        UpdateIndicator();
        OnCanReflectChanged?.Invoke(CanReflect);
    }

    private void UpdateIndicator()
    {
        if (indicatorRenderer == null) return;
        Color emission = (IsPowered && !_isFogBlocked) ? onEmission : offEmission;
        indicatorRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(EmissionColor, emission);
        indicatorRenderer.SetPropertyBlock(_mpb);
    }
}
