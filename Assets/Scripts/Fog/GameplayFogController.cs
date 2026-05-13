using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GameplayFogController : Singleton<GameplayFogController>
{
    [Header("Fog Targets (written every frame)")]
    [Tooltip("Gameplay fog box materials that should be adjusted by the multipliers.")]
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

    private struct BaseFogSettings
    {
        public float Visibility;
        public float Density;
    }

    private readonly Dictionary<Material, BaseFogSettings> baseSettings = new Dictionary<Material, BaseFogSettings>();

    private void OnEnable()
    {
        RebuildBaseCache();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            RebuildBaseCache();
        }
    }

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

    public void RefreshBaseValues()
    {
        RebuildBaseCache();
    }

    private void RebuildBaseCache()
    {
        baseSettings.Clear();
        if (targets == null) return;

        foreach (var mat in targets)
        {
            if (mat == null) continue;
            CacheMaterial(mat);
        }
    }

    private void CacheMaterial(Material mat)
    {
        BaseFogSettings settings = new BaseFogSettings
        {
            Visibility = mat.HasProperty(Visibility) ? mat.GetFloat(Visibility) : 0f,
            Density = mat.HasProperty(Density) ? mat.GetFloat(Density) : 0f
        };

        baseSettings[mat] = settings;
    }

    void ApplyFog()
    {
        if (targets == null || targets.Count == 0) return;

        foreach (var mat in targets)
        {
            if (mat == null) continue;

            if (!baseSettings.TryGetValue(mat, out var baseValues))
            {
                CacheMaterial(mat);
                baseValues = baseSettings[mat];
            }

            if (mat.HasProperty(Visibility)) mat.SetFloat(Visibility, baseValues.Visibility * smoothedVisibilityMul);
            if (mat.HasProperty(Density))    mat.SetFloat(Density, baseValues.Density * smoothedDensityMul);
        }
    }
}
