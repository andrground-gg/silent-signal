using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GameplayFogController : Singleton<GameplayFogController>
{
    [Header("Fog Targets (written every frame)")]
    [SerializeField] private List<Material> targets = new List<Material>();

    [Header("Base Fog Values (used as fallback / editor default)")]
    [SerializeField] private float baseVisibility = 1f;
    [SerializeField] private float baseDensity    = 5f;

    [Header("Time of Day Colors")]
    [SerializeField] private Color morningColor = new Color(0.70f, 0.75f, 0.80f, 1f);
    [SerializeField] private Color noonColor    = new Color(0.75f, 0.80f, 0.85f, 1f);
    [SerializeField] private Color eveningColor = new Color(0.60f, 0.50f, 0.55f, 1f);
    [SerializeField] private Color nightColor   = new Color(0.10f, 0.12f, 0.20f, 1f);

    [Header("Smooth Speed")]
    [SerializeField] private float lerpSpeed = 2f;

    private float _targetVisibility;
    private float _targetDensity;
    private float _currentVisibility;
    private float _currentDensity;

    private static readonly int PropVisibility = Shader.PropertyToID("_Visibility");
    private static readonly int PropDensity    = Shader.PropertyToID("_Density");
    private static readonly int PropFogColor   = Shader.PropertyToID("_FogColor");


    void Start()
    {
        _targetVisibility  = baseVisibility;
        _targetDensity     = baseDensity;
        _currentVisibility = baseVisibility;
        _currentDensity    = baseDensity;
    }

    void Update()
    {
        float dt = Application.isPlaying ? Time.deltaTime : 0.02f;
        float k  = 1f - Mathf.Exp(-lerpSpeed * dt);
        _currentVisibility = Mathf.Lerp(_currentVisibility, _targetVisibility, k);
        _currentDensity    = Mathf.Lerp(_currentDensity,    _targetDensity,    k);
        ApplyFog();
    }

    public void SetVisibilityImmediate(float value)
    {
        _targetVisibility  = value;
        _currentVisibility = value;
    }

    public void SetDensityImmediate(float value)
    {
        _targetDensity  = value;
        _currentDensity = value;
    }

    public void SetVisibility(float value) => _targetVisibility = value;
    public void SetDensity(float value)    => _targetDensity    = value;

    Color SampleTimeOfDayColor()
    {
        float t = 0f;
        if (TimeManager.Instance != null)
            t = TimeManager.Instance.Service.NormalizedTime;

        // Same wheel as TimeOfDayController: 0=morning, 0.25=noon, 0.5=evening, 0.75=night
        Color a, b;
        float blend;

        if      (t < 0.25f) { a = morningColor; b = noonColor;    blend = t / 0.25f; }
        else if (t < 0.50f) { a = noonColor;    b = eveningColor; blend = (t - 0.25f) / 0.25f; }
        else if (t < 0.75f) { a = eveningColor; b = nightColor;   blend = (t - 0.50f) / 0.25f; }
        else                 { a = nightColor;   b = morningColor; blend = (t - 0.75f) / 0.25f; }

        return Color.Lerp(a, b, blend);
    }

    void ApplyFog()
    {
        if (targets == null || targets.Count == 0) return;
        Color fogColor = SampleTimeOfDayColor();

        foreach (var mat in targets)
        {
            if (mat == null) continue;
            if (mat.HasProperty(PropVisibility)) mat.SetFloat(PropVisibility, _currentVisibility);
            if (mat.HasProperty(PropDensity))    mat.SetFloat(PropDensity,    _currentDensity);
            if (mat.HasProperty(PropFogColor))   mat.SetColor(PropFogColor,   fogColor);
        }
    }
}
