using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LightManager : MonoBehaviour {
    [SerializeField] TimeManager timeManager;

    [Header("Lights")]
    [SerializeField] Light sun;
    [SerializeField] Light moon;
    [SerializeField] float maxSunIntensity = 1f;
    [SerializeField] float maxMoonIntensity = 0.3f;

    [Header("Curves")]
    [SerializeField] AnimationCurve lightIntensityCurve;
    [SerializeField] AnimationCurve shadowStrengthCurve;   // soft at dawn/dusk, hard at noon

    [Header("Sun color temperature")]
    [SerializeField] Gradient sunColorGradient;            // midnight blue → orange dawn → white noon → orange dusk → midnight blue

    [Header("Moon color")]
    [SerializeField] Color moonColor = new Color(0.4f, 0.5f, 0.7f);   // cool blue-white

    [Header("Ambient")]
    [SerializeField] Color dayAmbientLight;
    [SerializeField] Color nightAmbientLight;
    [SerializeField] Color horizonAmbientLight;            // warm orange, used at dawn/dusk
    [SerializeField] AnimationCurve horizonBlendCurve;     // spikes at ~0.25 and ~0.75 (sunrise/sunset)

    [Header("Skybox")]
    [SerializeField] Material skyboxMaterial;

    [Header("Post-processing")]
    [SerializeField] Volume volume;

    ColorAdjustments colorAdjustments;
    Bloom bloom;
    TimeService service;

    void Start() {
        volume.profile.TryGet(out colorAdjustments);
        volume.profile.TryGet(out bloom);

        service = timeManager.Service;
        service.OnTimeChanged += HandleTimeChanged;
    }

    void OnDisable() {
        if (service != null) service.OnTimeChanged -= HandleTimeChanged;
    }

    void HandleTimeChanged(float t) {
        RotateSun(t);
        UpdateSunLight(t);
        UpdateMoonLight(t);
        UpdateAmbient(t);
        UpdateSkyBlend(t);
        UpdatePostProcessing(t);
    }

    void RotateSun(float t) {
        sun.transform.rotation  = Quaternion.AngleAxis(t * 360f - 90f, Vector3.right);
        moon.transform.rotation = Quaternion.AngleAxis(t * 360f + 90f, Vector3.right);
    }

    void UpdateSunLight(float t) {
        float dot       = Vector3.Dot(sun.transform.forward, Vector3.down);
        float intensity = lightIntensityCurve.Evaluate(dot);

        sun.intensity     = Mathf.Lerp(0f, maxSunIntensity, intensity);
        sun.color         = sunColorGradient.Evaluate(t);           // warm at dawn/dusk, white at noon
        sun.shadowStrength = shadowStrengthCurve.Evaluate(dot);     // soft long shadows near horizon
    }

    void UpdateMoonLight(float t) {
        float dot          = Vector3.Dot(moon.transform.forward, Vector3.down);
        float moonIntensity = lightIntensityCurve.Evaluate(dot);

        moon.intensity      = Mathf.Lerp(0f, maxMoonIntensity, moonIntensity);
        moon.color          = moonColor;
        moon.shadowStrength = Mathf.Lerp(0f, 0.4f, moonIntensity);  // moon casts soft shadows only
    }

    void UpdateAmbient(float t) {
        float dot       = Vector3.Dot(sun.transform.forward, Vector3.down);
        float intensity = lightIntensityCurve.Evaluate(dot);

        // Base day/night blend
        Color baseAmbient = Color.Lerp(nightAmbientLight, dayAmbientLight, intensity);

        // Horizon bounce: spikes at sunrise (~t=0.25) and sunset (~t=0.75)
        float horizonBlend = horizonBlendCurve.Evaluate(t);
        Color ambient      = Color.Lerp(baseAmbient, horizonAmbientLight, horizonBlend);

        RenderSettings.ambientLight = ambient;

        if (colorAdjustments != null)
            colorAdjustments.colorFilter.value = ambient;
    }

    void UpdateSkyBlend(float t) {
        float blend = lightIntensityCurve.Evaluate(Vector3.Dot(sun.transform.forward, Vector3.up));
        skyboxMaterial.SetFloat("_Blend", blend);
    }

    void UpdatePostProcessing(float t) {
        if (bloom == null) return;

        float dot       = Vector3.Dot(sun.transform.forward, Vector3.down);
        float intensity = lightIntensityCurve.Evaluate(dot);

        float horizonBlend  = horizonBlendCurve.Evaluate(t);
        bloom.intensity.value = Mathf.Lerp(0.2f, 1.2f, horizonBlend);

        if (colorAdjustments != null) {
            colorAdjustments.saturation.value = Mathf.Lerp(-20f, 0f, intensity); // desaturate at night
        }
    }
}