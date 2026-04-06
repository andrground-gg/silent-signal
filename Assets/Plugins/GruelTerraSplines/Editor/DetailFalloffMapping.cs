using UnityEngine;

namespace GruelTerraSplines
{
    /// <summary>
    /// Converts grass/detail falloff values between normalized UI space and the legacy power range.
    /// </summary>
    public static class DetailFalloffMapping
    {
        public const float MinPower = 0.1f;
        public const float MaxPower = 8f;

        public static float ToPower(float normalized)
        {
            return Mathf.Lerp(MinPower, MaxPower, Mathf.Clamp01(normalized));
        }

        public static float ToNormalized(float power)
        {
            return Mathf.InverseLerp(MinPower, MaxPower, Mathf.Clamp(power, MinPower, MaxPower));
        }
    }
}
