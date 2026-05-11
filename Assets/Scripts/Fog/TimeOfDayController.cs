using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class TimeOfDayController : Singleton<TimeOfDayController>
{
    [Header("Time of Day")]
    [SerializeField, Range(0f, 1f)]
    private float currentTime;

    // ─────────────────────────────────────────────
    // Color materials (driven by _time uniform)
    // ─────────────────────────────────────────────
    [Header("Color Materials")]
    [Tooltip("Materials that animate via the _time uniform (water gradients, sky, etc.)")]
    [SerializeField] private List<Material> colorMaterials = new List<Material>();

    // ─────────────────────────────────────────────
    // Fog preset lerping
    // ─────────────────────────────────────────────
    [Header("Fog Presets (sampled — never modified)")]
    [SerializeField] private Material fogMorning;
    [SerializeField] private Material fogNoon;
    [SerializeField] private Material fogEvening;
    [SerializeField] private Material fogNight;

    [Header("Fog Targets (written every frame)")]
    [Tooltip("Fog box material + water material + anything else that should match.")]
    [SerializeField] private List<Material> fogTargets = new List<Material>();

    [Header("Gameplay Modifiers")]
    [Tooltip("Multiplier on fog Visibility. 1 = preset value, <1 = denser fog, >1 = clearer.")]
    [SerializeField, Range(0.05f, 5f)]
    private float visibilityMultiplier = 1f;

    [Tooltip("Multiplier on fog Density. 1 = preset, >1 = thicker, <1 = thinner.")]
    [SerializeField, Range(0f, 5f)]
    private float densityMultiplier = 1f;

    [Tooltip("How fast gameplay multipliers smoothly chase their targets. Higher = snappier.")]
    [SerializeField] private float modifierLerpSpeed = 2f;

    // Smoothed values that actually get written to materials
    private float smoothedVisibilityMul = 1f;
    private float smoothedDensityMul = 1f;

    // ─────────────────────────────────────────────
    // Skybox preset lerping
    // ─────────────────────────────────────────────
    [Header("Skybox Presets (sampled — never modified)")]
    [SerializeField] private Material skyboxMorning;
    [SerializeField] private Material skyboxNoon;
    [SerializeField] private Material skyboxEvening;
    [SerializeField] private Material skyboxNight;

    [Header("Skybox Target (written every frame)")]
    [Tooltip("The single skybox material that lives in the scene / RenderSettings.skybox.")]
    [SerializeField] private Material skyboxTarget;

    // ─────────────────────────────────────────────
    // Sun rotation
    // ─────────────────────────────────────────────
    [Header("Sun Rotation")]
    [Tooltip("Directional light that represents the sun. Rotated based on currentTime.")]
    [SerializeField] private Transform sunTransform;

    [Tooltip("Axis around which the sun orbits. X = classic east-west arc.")]
    [SerializeField] private Vector3 sunRotationAxis = Vector3.right;

    [Tooltip("Compass direction of sunrise (rotation around world Y).")]
    [SerializeField, Range(-180f, 180f)] private float sunYawOffset = 0f;

    [Tooltip("Angle offset so time=0 (morning) points at the horizon, not straight down.")]
    [SerializeField, Range(-180f, 180f)] private float sunAngleOffset = -90f;

    [Tooltip("Flip if the sun moves the wrong way across the sky.")]
    [SerializeField] private bool sunInvertDirection = false;

    // Property IDs — cached for perf
    private static readonly int TimeProp      = Shader.PropertyToID("_time");

    // Fog
    private static readonly int Visibility    = Shader.PropertyToID("_Visibility");
    private static readonly int Density       = Shader.PropertyToID("_Density");
    private static readonly int FogColor      = Shader.PropertyToID("_FogColor");
    private static readonly int HeightStart   = Shader.PropertyToID("_HeightStart");
    private static readonly int HeightEnd     = Shader.PropertyToID("_HeightEnd");
    private static readonly int HeightFalloff = Shader.PropertyToID("_HeightFalloff");
    private static readonly int NoiseScale    = Shader.PropertyToID("_NoiseScale");
    private static readonly int NoiseStrength = Shader.PropertyToID("_NoiseStrength");
    private static readonly int NoiseSpeed    = Shader.PropertyToID("_NoiseSpeed");

    // Skybox
    private static readonly int SkyColor1             = Shader.PropertyToID("_Color1");
    private static readonly int SkyColor2             = Shader.PropertyToID("_Color2");
    private static readonly int SkySunColor           = Shader.PropertyToID("_Sun_Color");
    private static readonly int SkySunIntensity       = Shader.PropertyToID("_Sun_Intensity");
    private static readonly int SkySunScale           = Shader.PropertyToID("_Sun_Scale");
    private static readonly int SkyBias               = Shader.PropertyToID("_Bias");
    private static readonly int SkyTransitionSmooth   = Shader.PropertyToID("_TransitionSmoothness");

    void Update()
    {
        if (TimeManager.Instance != null)
        {
            currentTime = TimeManager.Instance.Service.NormalizedTime;
        }

        // Frame-rate independent smoothing of gameplay multipliers
        float dt = Application.isPlaying ? Time.deltaTime : 0.02f;
        float k  = 1f - Mathf.Exp(-modifierLerpSpeed * dt);
        smoothedVisibilityMul = Mathf.Lerp(smoothedVisibilityMul, visibilityMultiplier, k);
        smoothedDensityMul    = Mathf.Lerp(smoothedDensityMul,    densityMultiplier,    k);

        UpdateColorMaterials();
        UpdateFog();
        UpdateSkybox();
        UpdateSunRotation();
    }

    // ─────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────

    /// <summary>0 = morning, 0.25 = noon, 0.5 = evening, 0.75 = night, 1 = morning</summary>
    public void SetNormalizedTime(float time)
    {
        currentTime = Mathf.Repeat(time, 1f);
    }

    /// <summary>Smoothed change — for gameplay events.</summary>
    public void SetVisibilityMultiplier(float value)
    {
        visibilityMultiplier = value;
    }

    /// <summary>Instant change — for cutscenes or initial state.</summary>
    public void SetVisibilityMultiplierImmediate(float value)
    {
        visibilityMultiplier = value;
        smoothedVisibilityMul = value;
    }

    public void SetDensityMultiplier(float value)
    {
        densityMultiplier = value;
    }

    // ─────────────────────────────────────────────
    // Internals
    // ─────────────────────────────────────────────

    void UpdateColorMaterials()
    {
        if (colorMaterials == null) return;

        foreach (var mat in colorMaterials)
        {
            if (mat == null) continue;
            if (mat.HasProperty(TimeProp))
            {
                mat.SetFloat(TimeProp, currentTime);
            }
        }
    }

    /// <summary>
    /// Picks the two presets to blend and returns the local 0..1 factor between them
    /// based on the current normalized time-of-day wheel.
    /// </summary>
    void GetTimeWheelBlend(Material morning, Material noon, Material evening, Material night,
                           out Material a, out Material b, out float t)
    {
        if      (currentTime < 0.25f) { a = morning; b = noon;    t = (currentTime - 0.00f) / 0.25f; }
        else if (currentTime < 0.50f) { a = noon;    b = evening; t = (currentTime - 0.25f) / 0.25f; }
        else if (currentTime < 0.75f) { a = evening; b = night;   t = (currentTime - 0.50f) / 0.25f; }
        else                          { a = night;   b = morning; t = (currentTime - 0.75f) / 0.25f; }
    }

    void UpdateFog()
    {
        if (fogTargets == null || fogTargets.Count == 0) return;
        if (fogMorning == null || fogNoon == null || fogEvening == null || fogNight == null) return;

        GetTimeWheelBlend(fogMorning, fogNoon, fogEvening, fogNight, out var a, out var b, out float t);

        // Sample both presets, lerp every property
        float visibility    = Mathf.Lerp(a.GetFloat(Visibility),    b.GetFloat(Visibility),    t) * smoothedVisibilityMul;
        float density       = Mathf.Lerp(a.GetFloat(Density),       b.GetFloat(Density),       t) * smoothedDensityMul;
        Color fogColor      = Color.Lerp(a.GetColor(FogColor),      b.GetColor(FogColor),      t);
        float heightStart   = Mathf.Lerp(a.GetFloat(HeightStart),   b.GetFloat(HeightStart),   t);
        float heightEnd     = Mathf.Lerp(a.GetFloat(HeightEnd),     b.GetFloat(HeightEnd),     t);
        float heightFalloff = Mathf.Lerp(a.GetFloat(HeightFalloff), b.GetFloat(HeightFalloff), t);
        float noiseScale    = Mathf.Lerp(a.GetFloat(NoiseScale),    b.GetFloat(NoiseScale),    t);
        float noiseStrength = Mathf.Lerp(a.GetFloat(NoiseStrength), b.GetFloat(NoiseStrength), t);
        Vector4 noiseSpeed  = Vector4.Lerp(a.GetVector(NoiseSpeed), b.GetVector(NoiseSpeed),   t);

        // Write to all targets — HasProperty allows fog box + water to share controller
        // even though water lacks noise / height props
        foreach (var mat in fogTargets)
        {
            if (mat == null) continue;

            if (mat.HasProperty(Visibility))    mat.SetFloat (Visibility,    visibility);
            if (mat.HasProperty(Density))       mat.SetFloat (Density,       density);
            if (mat.HasProperty(FogColor))      mat.SetColor (FogColor,      fogColor);
            if (mat.HasProperty(HeightStart))   mat.SetFloat (HeightStart,   heightStart);
            if (mat.HasProperty(HeightEnd))     mat.SetFloat (HeightEnd,     heightEnd);
            if (mat.HasProperty(HeightFalloff)) mat.SetFloat (HeightFalloff, heightFalloff);
            if (mat.HasProperty(NoiseScale))    mat.SetFloat (NoiseScale,    noiseScale);
            if (mat.HasProperty(NoiseStrength)) mat.SetFloat (NoiseStrength, noiseStrength);
            if (mat.HasProperty(NoiseSpeed))    mat.SetVector(NoiseSpeed,    noiseSpeed);
        }
    }

    void UpdateSkybox()
    {
        if (skyboxTarget == null) return;
        if (skyboxMorning == null || skyboxNoon == null || skyboxEvening == null || skyboxNight == null) return;

        GetTimeWheelBlend(skyboxMorning, skyboxNoon, skyboxEvening, skyboxNight, out var a, out var b, out float t);

        // Sample both presets, lerp every property
        Color color1            = Color.Lerp(a.GetColor(SkyColor1),           b.GetColor(SkyColor1),           t);
        Color color2            = Color.Lerp(a.GetColor(SkyColor2),           b.GetColor(SkyColor2),           t);
        Color sunColor          = Color.Lerp(a.GetColor(SkySunColor),         b.GetColor(SkySunColor),         t);
        float sunIntensity      = Mathf.Lerp(a.GetFloat(SkySunIntensity),     b.GetFloat(SkySunIntensity),     t);
        float sunScale          = Mathf.Lerp(a.GetFloat(SkySunScale),         b.GetFloat(SkySunScale),         t);
        float bias              = Mathf.Lerp(a.GetFloat(SkyBias),             b.GetFloat(SkyBias),             t);
        float transitionSmooth  = Mathf.Lerp(a.GetFloat(SkyTransitionSmooth), b.GetFloat(SkyTransitionSmooth), t);

        if (skyboxTarget.HasProperty(SkyColor1))           skyboxTarget.SetColor(SkyColor1,           color1);
        if (skyboxTarget.HasProperty(SkyColor2))           skyboxTarget.SetColor(SkyColor2,           color2);
        if (skyboxTarget.HasProperty(SkySunColor))         skyboxTarget.SetColor(SkySunColor,         sunColor);
        if (skyboxTarget.HasProperty(SkySunIntensity))     skyboxTarget.SetFloat(SkySunIntensity,     sunIntensity);
        if (skyboxTarget.HasProperty(SkySunScale))         skyboxTarget.SetFloat(SkySunScale,         sunScale);
        if (skyboxTarget.HasProperty(SkyBias))             skyboxTarget.SetFloat(SkyBias,             bias);
        if (skyboxTarget.HasProperty(SkyTransitionSmooth)) skyboxTarget.SetFloat(SkyTransitionSmooth, transitionSmooth);
    }

    void UpdateSunRotation()
    {
        if (sunTransform == null) return;

        float direction = sunInvertDirection ? -1f : 1f;
        float angle = sunAngleOffset + currentTime * 360f * direction;

        sunTransform.rotation =
            Quaternion.Euler(0f, sunYawOffset, 0f) *
            Quaternion.AngleAxis(angle, sunRotationAxis);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TimeOfDayController))]
public class TimeOfDayControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var controller = (TimeOfDayController)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Time of Day", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Morning")) controller.SetNormalizedTime(0f);
            if (GUILayout.Button("Noon"))    controller.SetNormalizedTime(0.25f);
            if (GUILayout.Button("Evening")) controller.SetNormalizedTime(0.5f);
            if (GUILayout.Button("Night"))   controller.SetNormalizedTime(0.75f);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Visibility Presets (multiplier)", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Storm 0.3"))  controller.SetVisibilityMultiplierImmediate(0.3f);
            if (GUILayout.Button("Mist 0.6"))   controller.SetVisibilityMultiplierImmediate(0.6f);
            if (GUILayout.Button("Normal 1.0")) controller.SetVisibilityMultiplierImmediate(1.0f);
            if (GUILayout.Button("Clear 2.0"))  controller.SetVisibilityMultiplierImmediate(2.0f);
        }
    }
}
#endif