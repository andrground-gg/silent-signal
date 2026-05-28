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
    [SerializeField] private AudioSource rotateSound;

    [Header("Indicator")]
    [SerializeField] private Color offEmission = new Color(0.15f, 0f,    0f);
    [SerializeField] private Color onEmission  = new Color(0f,    0.25f, 0.05f);

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    private int  _state;   // 0–3  →  0° / 90° / 180° / 270°
    private bool _isRotating;
    private bool _isFogBlocked;
    private bool          _isPrimaryBeamHitting;
    private bool          _isExternalBeamHitting;
    private ReflectedBeam _externalBeamSource;
    private Vector3       _lastIncomingDir;
    private float         _primaryBeamLastStayTime = -1f;

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

    public bool CanReflect => IsPowered && !_isFogBlocked;

    public event Action<bool> OnCanReflectChanged;

    private bool IsAnyBeamHitting => _isPrimaryBeamHitting || _isExternalBeamHitting;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (_isExternalBeamHitting && (_externalBeamSource == null || !_externalBeamSource.gameObject.activeSelf))
        {
            _isExternalBeamHitting = false;
            _externalBeamSource    = null;
            if (!_isPrimaryBeamHitting) DeactivateReflection();
        }

        if (_isPrimaryBeamHitting && Time.time - _primaryBeamLastStayTime > Time.fixedDeltaTime * 3f)
        {
            _isPrimaryBeamHitting = false;
            if (!_isExternalBeamHitting) DeactivateReflection();
        }

        if (_isRotating && IsAnyBeamHitting)
            TryActivateReflection(updateCollider: false);
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

        _state = (_state + 1) % 8;

        _isRotating = true;
        rotateSound.Play();

        rotatingHead
            .DOLocalRotate(new Vector3(0f, 45f, 0f), rotationDuration, RotateMode.LocalAxisAdd)
            .SetEase(rotationEase)
            .OnComplete(() =>
            {
                _isRotating = false;
                TryActivateReflection();
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
            _lastIncomingDir       = -source.BeamDirection;
        }
        else
        {
            _isPrimaryBeamHitting    = true;
            _primaryBeamLastStayTime = Time.time;
            _lastIncomingDir         = GetBeamForward(other);
        }
        TryActivateReflection();
    }

    private static Vector3 GetBeamForward(Collider other)
    {
        var t = other.transform;
        while (t.parent != null && !t.CompareTag("LightHouseBeam"))
            t = t.parent;
        return t.forward;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(beamTag)) return;
        var source = other.GetComponentInParent<ReflectedBeam>();
        if (source != null)
        {
            if (source == reflectedBeam) return;
            bool wasHitting = _isExternalBeamHitting && _externalBeamSource == source;
            _isExternalBeamHitting = true;
            _externalBeamSource    = source;
            var newExternalDir     = -source.BeamDirection;
            if (!wasHitting || Vector3.Angle(newExternalDir, _lastIncomingDir) > 0.5f)
            {
                _lastIncomingDir = newExternalDir;
                TryActivateReflection();
            }
            else
            {
                _lastIncomingDir = newExternalDir;
            }
        }
        else
        {
            var newDir               = GetBeamForward(other);
            bool wasHitting          = _isPrimaryBeamHitting;
            _isPrimaryBeamHitting    = true;
            _primaryBeamLastStayTime = Time.time;
            if (!wasHitting)
            {
                _lastIncomingDir = newDir;
                TryActivateReflection();
            }
            else if (Vector3.Angle(newDir, _lastIncomingDir) > 0.5f)
            {
                _lastIncomingDir = newDir;
                TryActivateReflection();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(beamTag)) return;
        var source = other.GetComponentInParent<ReflectedBeam>();
        if (source != null)
        {
            if (source != _externalBeamSource) return;
            _isExternalBeamHitting = false;
            _externalBeamSource    = null;
            if (!_isPrimaryBeamHitting) DeactivateReflection();
        }
        else
        {
            _isPrimaryBeamHitting = false;
            if (!_isExternalBeamHitting) DeactivateReflection();
        }
    }

    private void TryActivateReflection(bool updateCollider = true)
    {
        if (!CanReflect || reflectedBeam == null || !IsAnyBeamHitting) return;
        reflectedBeam.Activate(_lastIncomingDir, updateCollider);
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
