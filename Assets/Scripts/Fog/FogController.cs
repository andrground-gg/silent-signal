using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class FogController : MonoBehaviour
{
    [Header("Time of Day")]
    [SerializeField, Range(0f, 1f)]
    private float currentTime;

    [Header("Preset Materials (sampled — never modified)")]
    [SerializeField] private Material morning;
    [SerializeField] private Material noon;
    [SerializeField] private Material evening;
    [SerializeField] private Material night;

    [Header("Target Materials (written every frame)")]
    [Tooltip("Fog box material + water material + anything else that should match.")]
    [SerializeField] private List<Material> targets = new List<Material>();

    [Header("Gameplay Modifiers")]
    [Tooltip("Multiplier on Visibility. 1 = preset value, <1 = denser fog, >1 = clearer.")]
    [SerializeField, Range(0.05f, 5f)]
    private float visibilityMultiplier = 1f;

    [Tooltip("Multiplier on Density. 1 = preset, >1 = thicker, <1 = thinner.")]
    [SerializeField, Range(0f, 5f)]
    private float densityMultiplier = 1f;

    [Tooltip("How fast gameplay multipliers smoothly chase their targets. Higher = snappier.")]
    [SerializeField] private float modifierLerpSpeed = 2f;

    // Smoothed values that actually get written to materials
    private float smoothedVisibilityMul = 1f;
    private float smoothedDensityMul = 1f;

    // Property name constants — must match shader
    private static readonly int Visibility    = Shader.PropertyToID("_Visibility");
    private static readonly int Density       = Shader.PropertyToID("_Density");
    private static readonly int FogColor      = Shader.PropertyToID("_FogColor");
    private static readonly int HeightStart   = Shader.PropertyToID("_HeightStart");
    private static readonly int HeightEnd     = Shader.PropertyToID("_HeightEnd");
    private static readonly int HeightFalloff = Shader.PropertyToID("_HeightFalloff");
    private static readonly int NoiseScale    = Shader.PropertyToID("_NoiseScale");
    private static readonly int NoiseStrength = Shader.PropertyToID("_NoiseStrength");
    private static readonly int NoiseSpeed    = Shader.PropertyToID("_NoiseSpeed");

    void Update()
    {
        if (TimeManager.Instance != null)
        {
            currentTime = TimeManager.Instance.Service.NormalizedTime;
        }

        // Smooth the gameplay multipliers so visibility shifts feel natural
        // rather than snapping. Editor uses unscaled time; runtime uses delta.
        float dt = Application.isPlaying ? Time.deltaTime : 0.02f;
        float k = 1f - Mathf.Exp(-modifierLerpSpeed * dt); // frame-rate independent lerp
        smoothedVisibilityMul = Mathf.Lerp(smoothedVisibilityMul, visibilityMultiplier, k);
        smoothedDensityMul    = Mathf.Lerp(smoothedDensityMul,    densityMultiplier,    k);

        ApplyFog();
    }

    public void SetNormalizedTime(float time)
    {
        currentTime = Mathf.Repeat(time, 1f);
    }

    /// <summary>Set instantly without smoothing — for cutscenes or initial state.</summary>
    public void SetVisibilityMultiplierImmediate(float value)
    {
        visibilityMultiplier = value;
        smoothedVisibilityMul = value;
    }

    /// <summary>Set with smoothing — for gameplay events.</summary>
    public void SetVisibilityMultiplier(float value)
    {
        visibilityMultiplier = value;
    }

    public void SetDensityMultiplier(float value)
    {
        densityMultiplier = value;
    }

    void ApplyFog()
    {
        if (targets == null || targets.Count == 0) return;
        if (morning == null || noon == null || evening == null || night == null) return;

        // Pick the two presets we're between, plus the local 0..1 blend factor.
        // Time wheel: 0.0 = morning, 0.25 = noon, 0.5 = evening, 0.75 = night, 1.0 = morning.
        Material a, b;
        float t;

        if      (currentTime < 0.25f) { a = morning; b = noon;    t = (currentTime - 0.00f) / 0.25f; }
        else if (currentTime < 0.50f) { a = noon;    b = evening; t = (currentTime - 0.25f) / 0.25f; }
        else if (currentTime < 0.75f) { a = evening; b = night;   t = (currentTime - 0.50f) / 0.25f; }
        else                          { a = night;   b = morning; t = (currentTime - 0.75f) / 0.25f; }

        // Sample both presets, lerp every property, write to all targets.
        float visibility    = Mathf.Lerp(a.GetFloat(Visibility),    b.GetFloat(Visibility),    t) * smoothedVisibilityMul;
        float density       = Mathf.Lerp(a.GetFloat(Density),       b.GetFloat(Density),       t) * smoothedDensityMul;
        Color fogColor      = Color.Lerp(a.GetColor(FogColor),      b.GetColor(FogColor),      t);
        float heightStart   = Mathf.Lerp(a.GetFloat(HeightStart),   b.GetFloat(HeightStart),   t);
        float heightEnd     = Mathf.Lerp(a.GetFloat(HeightEnd),     b.GetFloat(HeightEnd),     t);
        float heightFalloff = Mathf.Lerp(a.GetFloat(HeightFalloff), b.GetFloat(HeightFalloff), t);
        float noiseScale    = Mathf.Lerp(a.GetFloat(NoiseScale),    b.GetFloat(NoiseScale),    t);
        float noiseStrength = Mathf.Lerp(a.GetFloat(NoiseStrength), b.GetFloat(NoiseStrength), t);
        Vector4 noiseSpeed  = Vector4.Lerp(a.GetVector(NoiseSpeed), b.GetVector(NoiseSpeed),   t);

        foreach (var mat in targets)
        {
            if (mat == null) continue;

            // Float / color writes — silently skipped by Unity if a target lacks the property,
            // so the same controller can drive fog box + water (which has fewer props).
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
}

#if UNITY_EDITOR
[CustomEditor(typeof(FogController))]
public class FogControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var controller = (FogController)target;

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
            if (GUILayout.Button("Storm 0.3"))   controller.SetVisibilityMultiplierImmediate(0.3f);
            if (GUILayout.Button("Mist 0.6"))    controller.SetVisibilityMultiplierImmediate(0.6f);
            if (GUILayout.Button("Normal 1.0"))  controller.SetVisibilityMultiplierImmediate(1.0f);
            if (GUILayout.Button("Clear 2.0"))   controller.SetVisibilityMultiplierImmediate(2.0f);
        }
    }
}
#endif
