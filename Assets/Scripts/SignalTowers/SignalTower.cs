using System;
using DG.Tweening;
using GeneratorSystem;
using UnityEngine;

public class SignalTower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform     rotatingHead;
    [SerializeField] private Renderer      indicatorRenderer;
    [SerializeField] private ReflectedBeam reflectedBeam;

    [Header("Beam Detection")]
    [SerializeField] private string beamTag = "LightHouseBeam";

    [Header("Rotation")]
    [SerializeField] private float rotationDuration = 1f;
    [SerializeField] private Ease  rotationEase     = Ease.InOutSine;

    [Header("Indicator")]
    [SerializeField] private Color offEmission = new Color(0.15f, 0f,    0f);
    [SerializeField] private Color onEmission  = new Color(0f,    0.25f, 0.05f);

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    private int  _state;   // 0–3  →  0° / 90° / 180° / 270°
    private bool _isRotating;
    private bool _isFogBlocked;
    private bool         _isPrimaryBeamHitting;
    private bool         _isExternalBeamHitting;
    private ReflectedBeam _externalBeamSource;

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
            if (_isFogBlocked) DeactivateReflection();
            else               TryActivateReflection();
            OnCanReflectChanged?.Invoke(CanReflect);
        }
    }

    public bool CanReflect => IsPowered && !_isFogBlocked && !_isRotating;

    public event Action<bool> OnCanReflectChanged;

    private bool IsAnyBeamHitting => _isPrimaryBeamHitting || _isExternalBeamHitting;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    private void Update()
    {
        // OnTriggerExit ненадійний всередині physics callback — перевіряємо самі кожен фрейм
        if (_isExternalBeamHitting && (_externalBeamSource == null || !_externalBeamSource.gameObject.activeSelf))
        {
            _isExternalBeamHitting = false;
            _externalBeamSource    = null;
            if (!_isPrimaryBeamHitting) DeactivateReflection();
        }
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
        DeactivateReflection();
        OnCanReflectChanged?.Invoke(false);

        rotatingHead
            .DOLocalRotate(new Vector3(0f, 90f, 0f), rotationDuration, RotateMode.LocalAxisAdd)
            .SetEase(rotationEase)
            .OnComplete(() =>
            {
                _isRotating = false;
                TryActivateReflection();
                OnCanReflectChanged?.Invoke(CanReflect);
            })
            .SetLink(gameObject);
    }

    public void ClearExternalBeamHit()
    {
        _isExternalBeamHitting = false;
        _externalBeamSource    = null;
        if (!_isPrimaryBeamHitting) DeactivateReflection();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(beamTag)) return;
        var source = other.GetComponentInParent<ReflectedBeam>();
        if (source != null)
        {
            if (source == reflectedBeam) return; // власний промінь — ігноруємо
            _isExternalBeamHitting = true;
            _externalBeamSource    = source;
        }
        else
        {
            _isPrimaryBeamHitting = true;
        }
        TryActivateReflection();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(beamTag)) return;
        var source = other.GetComponentInParent<ReflectedBeam>();
        if (source != null) return; // зовнішній промінь — обробляється в Update, не тут
        _isPrimaryBeamHitting = false;
        if (!_isExternalBeamHitting) DeactivateReflection();
    }

    private void TryActivateReflection()
    {
        if (!CanReflect || reflectedBeam == null || !IsAnyBeamHitting) return;
        reflectedBeam.Activate(rotatingHead.forward);
    }

    private void DeactivateReflection()
    {
        reflectedBeam?.Deactivate();
    }

    private void OnGeneratorActivated(GeneratorID id)
    {
        if (id != GeneratorID.GENERATOR_SIGNAL_TOWERS) return;
        IsPowered = true;
        UpdateIndicator();
        TryActivateReflection();
        OnCanReflectChanged?.Invoke(CanReflect);
    }

    private void OnGeneratorDeactivated(GeneratorID id)
    {
        if (id != GeneratorID.GENERATOR_SIGNAL_TOWERS) return;
        IsPowered = false;
        UpdateIndicator();
        DeactivateReflection();
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
