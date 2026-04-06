using UnityEngine;

namespace GruelTerraSplines
{
    public class PaintOperationApplier : OperationApplierBase
    {
        static readonly System.Collections.Generic.Dictionary<int, Texture2D> readableNoiseCache = new System.Collections.Generic.Dictionary<int, Texture2D>();
        static readonly System.Collections.Generic.Dictionary<int, bool> noiseUseAlphaCache = new System.Collections.Generic.Dictionary<int, bool>();

        static Texture2D GetReadableTexture(Texture2D src)
        {
            if (src == null) return null;
            if (src.isReadable) return src;

            int id = src.GetInstanceID();
            if (readableNoiseCache.TryGetValue(id, out var cached) && cached != null)
            {
                return cached;
            }

            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;

                var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true)
                {
                    name = $"{src.name}_ReadableCopy",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = src.filterMode,
                    hideFlags = HideFlags.HideAndDontSave
                };

                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply(false, false);

                readableNoiseCache[id] = tex;
                return tex;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        static bool ShouldUseAlpha(Texture2D readableNoise)
        {
            if (readableNoise == null) return false;

            int id = readableNoise.GetInstanceID();
            if (noiseUseAlphaCache.TryGetValue(id, out var cached))
            {
                return cached;
            }

            float minA = 1f;
            float maxA = 0f;
            void Accumulate(Texture2D t, float u, float v, ref float min, ref float max)
            {
                float a = t.GetPixelBilinear(u, v).a;
                min = Mathf.Min(min, a);
                max = Mathf.Max(max, a);
            }

            Accumulate(readableNoise, 0.13f, 0.13f, ref minA, ref maxA);
            Accumulate(readableNoise, 0.87f, 0.13f, ref minA, ref maxA);
            Accumulate(readableNoise, 0.13f, 0.87f, ref minA, ref maxA);
            Accumulate(readableNoise, 0.87f, 0.87f, ref minA, ref maxA);
            Accumulate(readableNoise, 0.50f, 0.50f, ref minA, ref maxA);

            bool useAlpha = (maxA - minA) > 0.01f && minA < 0.999f;
            noiseUseAlphaCache[id] = useAlpha;
            return useAlpha;
        }

        public override void Apply(
            Terrain terrain,
            ISplineHeightmapCache cache,
            int childPriority,
            int[,] writePriority,
            OperationContext context)
        {
            if (context.alphamaps == null || !ValidateCache(cache)) return;

            System.Collections.Generic.Dictionary<int, PaintNoiseLayerSettings> noiseByLayer = null;
            System.Collections.Generic.HashSet<int> unreadableNoiseTextureWarned = null;
            if (context.paintNoiseLayers != null && context.paintNoiseLayers.Length > 0)
            {
                noiseByLayer = new System.Collections.Generic.Dictionary<int, PaintNoiseLayerSettings>(context.paintNoiseLayers.Length);
                for (int i = 0; i < context.paintNoiseLayers.Length; i++)
                {
                    var entry = context.paintNoiseLayers[i];
                    if (entry == null) continue;
                    noiseByLayer[entry.paintLayerIndex] = entry;
                }

                unreadableNoiseTextureWarned = new System.Collections.Generic.HashSet<int>();
            }

            int heightmapWidth = writePriority.GetLength(1);
            int heightmapHeight = writePriority.GetLength(0);
            int alphamapWidth = context.alphamaps.GetLength(1);
            int alphamapHeight = context.alphamaps.GetLength(0);

            float alphaScaleX = (heightmapWidth > 1 && alphamapWidth > 1)
                ? (alphamapWidth - 1f) / (heightmapWidth - 1f)
                : 1f;
            float alphaScaleZ = (heightmapHeight > 1 && alphamapHeight > 1)
                ? (alphamapHeight - 1f) / (heightmapHeight - 1f)
                : 1f;

            float terrainPosX = 0f;
            float terrainPosZ = 0f;
            float terrainSizeX = 1f;
            float terrainSizeZ = 1f;
            if (terrain != null && terrain.terrainData != null)
            {
                terrainPosX = terrain.transform.position.x;
                terrainPosZ = terrain.transform.position.z;
                terrainSizeX = Mathf.Max(0.0001f, terrain.terrainData.size.x);
                terrainSizeZ = Mathf.Max(0.0001f, terrain.terrainData.size.z);
            }

            float Wrap01(float t) => t - Mathf.Floor(t);

            ProcessCacheRegion(cache, childPriority, writePriority, terrain, context, (x, z, localX, localZ, cachedAlpha) =>
            {
                int alphaX = Mathf.Clamp(Mathf.RoundToInt(x * alphaScaleX), 0, alphamapWidth - 1);
                int alphaZ = Mathf.Clamp(Mathf.RoundToInt(z * alphaScaleZ), 0, alphamapHeight - 1);

                // Accumulate mask for paint operations
                if (context.paintSplineAlphaMask != null)
                {
                    context.paintSplineAlphaMask[z, x] = Mathf.Max(context.paintSplineAlphaMask[z, x], cachedAlpha);
                }

                // Apply paint strength
                float paintAlpha = cachedAlpha * context.paintStrength;

                if (noiseByLayer != null
                    && noiseByLayer.TryGetValue(context.targetLayerIndex, out var noise)
                    && noise != null
                    && noise.noiseTexture != null)
                {
                    var noiseTex = noise.noiseTexture;
                    if (!noiseTex.isReadable && unreadableNoiseTextureWarned != null)
                    {
                        int id = noiseTex.GetInstanceID();
                        if (unreadableNoiseTextureWarned.Add(id))
                        {
                            Debug.LogWarning($"Paint noise texture '{noiseTex.name}' is not readable; using a temporary GPU-readback copy. Enable 'Read/Write' in import settings for better performance.");
                        }
                    }

                    var readableNoise = GetReadableTexture(noiseTex);
                    if (readableNoise != null)
                    {
                        bool useAlpha = ShouldUseAlpha(readableNoise);
                        float widthMinusOne = Mathf.Max(1, alphamapWidth - 1);
                        float heightMinusOne = Mathf.Max(1, alphamapHeight - 1);
                        float nx = alphaX / widthMinusOne;
                        float nz = alphaZ / heightMinusOne;
                        float worldX = terrainPosX + nx * terrainSizeX;
                        float worldZ = terrainPosZ + nz * terrainSizeZ;

                        float worldSize = Mathf.Max(0.001f, noise.noiseWorldSizeMeters);
                        float u = Wrap01((worldX / worldSize) + noise.noiseOffset.x);
                        float v = Wrap01((worldZ / worldSize) + noise.noiseOffset.y);
                        Color c = readableNoise.GetPixelBilinear(u, v);
                        float sample = useAlpha ? c.a : c.grayscale;
                        if (noise.noiseInvert) sample = 1f - sample;

                        float noiseStrength = Mathf.Clamp01(noise.noiseStrength);
                        float edge = Mathf.Clamp(noise.noiseEdge, -1f, 1f);
                        float alpha = Mathf.Clamp01(cachedAlpha);
                        float edgeFactor = edge >= 0f
                            ? 1f - (edge * alpha)
                            : 1f - (-edge * (1f - alpha));
                        sample = Mathf.Lerp(1f, sample, noiseStrength * edgeFactor);
                        paintAlpha *= sample;
                    }
                }

                if (paintAlpha <= 0.0001f) return;

                // Update write priority
                writePriority[z, x] = childPriority;

                // Smooth paint blending with gradual layer clearing
                float currentTargetValue = context.alphamaps[alphaZ, alphaX, context.targetLayerIndex];
                float newTargetValue = Mathf.Lerp(currentTargetValue, 1f, paintAlpha);

                // Calculate how much we need to reduce other layers to maintain smooth falloff
                float reductionFactor = paintAlpha * paintAlpha; // Quadratic falloff for smoother edges

                // Gradually reduce other layers based on paint strength
                for (int l = 0; l < context.alphamaps.GetLength(2); l++)
                {
                    if (l != context.targetLayerIndex)
                    {
                        context.alphamaps[alphaZ, alphaX, l] *= (1f - reductionFactor);
                    }
                }

                // Set target layer
                context.alphamaps[alphaZ, alphaX, context.targetLayerIndex] = newTargetValue;

                // Track pixels that need normalization instead of normalizing immediately
                float otherLayersSum = 0f;
                for (int l = 0; l < context.alphamaps.GetLength(2); l++)
                {
                    if (l != context.targetLayerIndex)
                        otherLayersSum += context.alphamaps[alphaZ, alphaX, l];
                }

                if (otherLayersSum > 0.01f) // Only normalize if other layers have meaningful values
                {
                    // Add to tracking set for batch normalization
                    if (context.pixelsNeedingNormalization != null)
                    {
                        context.pixelsNeedingNormalization.Add((alphaX, alphaZ));
                    }
                    else
                    {
                        // Fallback: normalize immediately if tracking not available
                        TerraSplinesTool.NormalizeAlphamaps(context.alphamaps, alphaX, alphaZ);
                    }
                }
            });
        }
    }
}