using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GameplayFogController : Singleton<GameplayFogController>
{
    [Header("Gameplay Fog Settings (sampled)")]
    [Tooltip("Preset material that defines gameplay fog settings. This material is never modified.")]
    [SerializeField] private Material gameplayPreset;

    [Header("Fog Targets (written every frame)")]
    [Tooltip("Gameplay fog box materials and any other materials that should match.")]
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

    private float smoothedVisibilityMul = 1f;
    private float smoothedDensityMul = 1f;

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
        float dt = Application.isPlaying ? Time.deltaTime : 0.02f;
        float k = 1f - Mathf.Exp(-modifierLerpSpeed * dt);
        smoothedVisibilityMul = Mathf.Lerp(smoothedVisibilityMul, visibilityMultiplier, k);
        smoothedDensityMul    = Mathf.Lerp(smoothedDensityMul,    densityMultiplier,    k);

        ApplyFog();
    }

    public void SetVisibilityMultiplier(float value)
    {
        visibilityMultiplier = value;
    }

    public void SetVisibilityMultiplierImmediate(float value)
    {
        visibilityMultiplier = value;
        smoothedVisibilityMul = value;
    }

    public void SetDensityMultiplier(float value)
    {
        densityMultiplier = value;
    }

    void ApplyFog()
    {
        if (targets == null || targets.Count == 0) return;
        if (gameplayPreset == null) return;

        float visibility    = gameplayPreset.GetFloat(Visibility) * smoothedVisibilityMul;
        float density       = gameplayPreset.GetFloat(Density) * smoothedDensityMul;
        Color fogColor      = gameplayPreset.GetColor(FogColor);
        float heightStart   = gameplayPreset.GetFloat(HeightStart);
        float heightEnd     = gameplayPreset.GetFloat(HeightEnd);
        float heightFalloff = gameplayPreset.GetFloat(HeightFalloff);
        float noiseScale    = gameplayPreset.GetFloat(NoiseScale);
        float noiseStrength = gameplayPreset.GetFloat(NoiseStrength);
        Vector4 noiseSpeed  = gameplayPreset.GetVector(NoiseSpeed);

        foreach (var mat in targets)
        {
            if (mat == null) continue;

            if (mat.HasProperty(Visibility))    mat.SetFloat(Visibility, visibility);
            if (mat.HasProperty(Density))       mat.SetFloat(Density, density);
            if (mat.HasProperty(FogColor))      mat.SetColor(FogColor, fogColor);
            if (mat.HasProperty(HeightStart))   mat.SetFloat(HeightStart, heightStart);
            if (mat.HasProperty(HeightEnd))     mat.SetFloat(HeightEnd, heightEnd);
            if (mat.HasProperty(HeightFalloff)) mat.SetFloat(HeightFalloff, heightFalloff);
            if (mat.HasProperty(NoiseScale))    mat.SetFloat(NoiseScale, noiseScale);
            if (mat.HasProperty(NoiseStrength)) mat.SetFloat(NoiseStrength, noiseStrength);
            if (mat.HasProperty(NoiseSpeed))    mat.SetVector(NoiseSpeed, noiseSpeed);
        }
    }
}
