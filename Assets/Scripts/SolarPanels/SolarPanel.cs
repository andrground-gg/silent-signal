using System;
using UnityEngine;

public class SolarPanel : MonoBehaviour
{
    [Header("Beam Detection")]
    [Tooltip("Tag on the beam collider that hits this panel (lighthouse or reflected).")]
    [SerializeField] private string beamTag = "LightHouseBeam";

    [Header("Charge")]
    [SerializeField] private float chargeRate     = 10f;   // % per second while beam hits
    [SerializeField] private float decayStartTime = 180f;  // seconds idle before decay begins
    [SerializeField] private float decayRate      = 2f;    // % per second once decay starts

    [Header("Glow")]
    [SerializeField] private Renderer panelRenderer;
    [SerializeField] private Color    baseEmissionColor = Color.yellow;
    [SerializeField] private float    maxGlowMultiplier = 4f;

    [Header("Meter")]
    [Tooltip("Renderer for the charge meter indicator — color shifts red→yellow→green.")]
    [SerializeField] private Renderer meterRenderer;
    [SerializeField] private float    meterGlowMultiplier = 3f;
    
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public float       Charge      { get; private set; }
    public bool        IsActivated { get; private set; }

    public event Action              OnPanelActivated;

    private bool     _isBeamHitting;
    private float    _timeSinceLastHit;
    private Material _mat;
    private Material _meterMat;

    private void Awake()
    {
        if (panelRenderer != null && panelRenderer.sharedMaterials.Length > 1)
        {
            _mat = panelRenderer.materials[1];
            if (_mat.HasProperty(EmissionColor))
                _mat.EnableKeyword("_EMISSION");
        }

        if (meterRenderer != null && meterRenderer.sharedMaterials.Length > 1)
        {
            _meterMat = meterRenderer.materials[1];
            if (_meterMat.HasProperty(EmissionColor))
                _meterMat.EnableKeyword("_EMISSION");
        }
    }

    private void Update()
    {
        if (IsActivated) return;

        if (_isBeamHitting)
        {
            _timeSinceLastHit = 0f;
            Charge = Mathf.Min(100f, Charge + chargeRate * Time.deltaTime);
        }
        else
        {
            _timeSinceLastHit += Time.deltaTime;
            if (_timeSinceLastHit >= decayStartTime)
                Charge = Mathf.Max(0f, Charge - decayRate * Time.deltaTime);
        }

        ApplyVisuals();

        if (Charge >= 100f)
            Activate();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(beamTag))
            _isBeamHitting = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(beamTag)) return;
        _isBeamHitting = false;
        _timeSinceLastHit = 0f;
    }

    private void ApplyVisuals()
    {
        float t = Charge / 100f;

        if (_mat != null)
            _mat.SetColor(EmissionColor, baseEmissionColor * (t * maxGlowMultiplier));

        if (_meterMat != null)
        {
            Color meterColor = t <= 0.5f
                ? Color.Lerp(Color.red, Color.yellow, t * 2f)
                : Color.Lerp(Color.yellow, Color.green, (t - 0.5f) * 2f);
            _meterMat.SetColor(EmissionColor, meterColor * meterGlowMultiplier);
        }
    }

    private void Activate()
    {
        Debug.Log($"Activate: {gameObject.name}");
        IsActivated = true;
        Charge = 100f;
        OnPanelActivated?.Invoke();
    }
}
