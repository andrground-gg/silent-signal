using UnityEngine;

namespace GruelTerraSplines
{
    public static class BrushFalloffUtils
    {
        /// <summary>
        /// Calculate brush falloff strength based on normalized distance and hardness
        /// </summary>
        /// <param name="normalizedDistance">Distance from center (0-1, where 1 is at brush edge)</param>
        /// <param name="hardness">Brush hardness (0-1, where 0=soft, 0.5=linear, 1=hard)</param>
        /// <returns>Falloff strength (0-1)</returns>
        public static float CalculateFalloff(float normalizedDistance, float hardness)
        {
            // Clamp inputs to valid ranges
            normalizedDistance = Mathf.Clamp01(normalizedDistance);
            hardness = Mathf.Clamp01(hardness);

            // Map hardness to power curve exponent
            // 0 = soft (exponent ~0.5), 0.5 = linear (exponent ~2), 1 = hard (exponent ~5+)
            float exponent = Mathf.Lerp(0.5f, 5f, hardness);

            // Calculate smoothstep from center (0) to edge (1)
            float smoothstepValue = 1f - Mathf.SmoothStep(0f, 1f, normalizedDistance);

            // Apply power curve
            float falloff = Mathf.Pow(smoothstepValue, exponent);

            return Mathf.Clamp01(falloff);
        }

        /// <summary>
        /// Generate a preview texture showing the brush falloff pattern
        /// </summary>
        /// <param name="size">Texture size (width and height)</param>
        /// <param name="hardness">Brush hardness (0-1)</param>
        /// <returns>RGBA32 texture showing the falloff pattern</returns>
        public static Texture2D GenerateBrushPreviewTexture(int size, float hardness)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            texture.wrapMode = TextureWrapMode.Clamp;

            var colors = new Color32[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDistance = size * 0.5f; // Distance from center to edge

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Calculate distance from center
                    Vector2 pixelPos = new Vector2(x, y);
                    float distance = Vector2.Distance(pixelPos, center);
                    float normalizedDistance = distance / maxDistance;

                    // Calculate falloff
                    float falloff = CalculateFalloff(normalizedDistance, hardness);

                    // Convert to grayscale color
                    byte grayscale = (byte)Mathf.Clamp(Mathf.RoundToInt(falloff * 255f), 0, 255);
                    colors[y * size + x] = new Color32(grayscale, grayscale, grayscale, 255);
                }
            }

            texture.SetPixels32(colors);
            texture.Apply(false, false);

            return texture;
        }

        /// <summary>
        /// Get a default brush preview texture with medium hardness
        /// </summary>
        /// <param name="size">Texture size</param>
        /// <returns>Default brush preview texture</returns>
        public static Texture2D GetDefaultBrushPreview(int size = 64)
        {
            return GenerateBrushPreviewTexture(size, 0.5f);
        }
    }
}