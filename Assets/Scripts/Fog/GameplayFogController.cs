using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GameplayFogController : Singleton<GameplayFogController>
{
    [Header("Fog Targets (written every frame)")]
    [SerializeField] private List<Material> targets = new List<Material>();

    [Header("Base Fog Values")]
    [Tooltip("Baseline Visibility when multiplier = 1. Set this manually to match your material.")]
    [SerializeField] private float baseVisibility = 100f;
    [Tooltip("Baseline Density when multiplier = 1.")]
    [SerializeField] private float baseDensity = 1f;

    [Header("Gameplay Modifiers")]
    [SerializeField, Range(0.05f, 5f)] private float visibilityMultiplier = 1f;
    [SerializeField, Range(0f, 5f)]    private float densityMultiplier    = 1f;
    [SerializeField] private float modifierLerpSpeed = 2f;

    private float smoothedVisibilityMul = 1f;
    private float smoothedDensityMul    = 1f;

    private static readonly int Visibility = Shader.PropertyToID("_Visibility");
    private static readonly int Density    = Shader.PropertyToID("_Density");

    void Update()
    {
        float dt = Application.isPlaying ? Time.deltaTime : 0.02f;
        float k  = 1f - Mathf.Exp(-modifierLerpSpeed * dt);
        smoothedVisibilityMul = Mathf.Lerp(smoothedVisibilityMul, visibilityMultiplier, k);
        smoothedDensityMul    = Mathf.Lerp(smoothedDensityMul,    densityMultiplier,    k);

        ApplyFog();
    }

    public float CurrentVisibility => smoothedVisibilityMul;

    public void SetVisibilityMultiplier(float value)         => visibilityMultiplier = value;
    public void SetVisibilityMultiplierImmediate(float value) { visibilityMultiplier = value; smoothedVisibilityMul = value; }
    public void SetDensityMultiplier(float value)            => densityMultiplier    = value;

    void ApplyFog()
    {
        if (targets == null || targets.Count == 0) return;

        foreach (var mat in targets)
        {
            if (mat == null) continue;
            if (mat.HasProperty(Visibility)) mat.SetFloat(Visibility, baseVisibility * smoothedVisibilityMul);
            if (mat.HasProperty(Density))    mat.SetFloat(Density,    baseDensity    * smoothedDensityMul);
        }
    }
}
