using UnityEngine;
using System.Collections.Generic;

namespace GruelTerraSplines
{
    public class DetailOperationApplier : OperationApplierBase
    {
        static readonly Dictionary<int, Texture2D> readableNoiseCache = new Dictionary<int, Texture2D>();
        static readonly Dictionary<int, bool> noiseUseAlphaCache = new Dictionary<int, bool>();

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

        // Enhanced detail painting with MapMagic-inspired features
        public override void Apply(
            Terrain terrain,
            ISplineHeightmapCache cache,
            int childPriority,
            int[,] writePriority,
            OperationContext context)
        {
            if (!ValidateCache(cache) || context == null) return;
            var detailLayers = (context.detailLayers != null && context.detailLayers.Length > 0)
                ? context.detailLayers
                : (context.detailLayer != null ? new[] { context.detailLayer } : null);
            if (detailLayers == null || detailLayers.Length == 0) return;

            int heightmapWidth = writePriority.GetLength(1);
            int heightmapHeight = writePriority.GetLength(0);

            int detailHeight = detailLayers[0].GetLength(0);
            int detailWidth = detailLayers[0].GetLength(1);

            // If no detail toggle is active, bail.
            if (context.detailLayer == null && (context.detailLayers == null || context.detailLayers.Length == 0)) return;

            Dictionary<int, DetailNoiseLayerSettings> noiseByLayer = null;
            HashSet<int> unreadableNoiseTextureWarned = null;
            if (context.detailNoiseLayers != null && context.detailNoiseLayers.Length > 0)
            {
                noiseByLayer = new Dictionary<int, DetailNoiseLayerSettings>(context.detailNoiseLayers.Length);
                for (int i = 0; i < context.detailNoiseLayers.Length; i++)
                {
                    var entry = context.detailNoiseLayers[i];
                    if (entry == null) continue;
                    noiseByLayer[entry.detailLayerIndex] = entry;
                }

                unreadableNoiseTextureWarned = new HashSet<int>();
            }

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

            // Process the spline cache region
            ProcessCacheRegion(cache, childPriority, writePriority, terrain, context, (x, z, localX, localZ, cachedAlpha) =>
            {
                if (terrain != null && context.detailSlopeLimitDegrees < 90f)
                {
                    var td = terrain.terrainData;
                    if (td != null && td.heightmapResolution > 1)
                    {
                        float nx = Mathf.Clamp01((float)x / (td.heightmapResolution - 1));
                        float nz = Mathf.Clamp01((float)z / (td.heightmapResolution - 1));
                        float slope = td.GetSteepness(nx, nz);
                        if (slope > context.detailSlopeLimitDegrees)
                        {
                            return;
                        }
                    }
                }

                // Apply detail painting based on the spline alpha mask
                float detailAlpha = cachedAlpha * Mathf.Clamp01(context.strength) * Mathf.Clamp01(context.detailStrength);
                float falloffPower = Mathf.Max(0.1f, context.detailFalloffPower);
                detailAlpha = Mathf.Pow(detailAlpha, falloffPower);
                if (detailAlpha <= 0.0001f) return;

                // Update write priority
                writePriority[z, x] = childPriority;

                // Calculate detail map coordinates
                int heightmapWidthMinusOne = Mathf.Max(1, heightmapWidth - 1);
                int heightmapHeightMinusOne = Mathf.Max(1, heightmapHeight - 1);
                int detailX = Mathf.Clamp(Mathf.RoundToInt((float)x / heightmapWidthMinusOne * (detailWidth - 1)), 0, detailWidth - 1);
                int detailZ = Mathf.Clamp(Mathf.RoundToInt((float)z / heightmapHeightMinusOne * (detailHeight - 1)), 0, detailHeight - 1);

                int maxDensity = Mathf.Max(0, context.detailTargetDensity);
                int radius = Mathf.Max(0, context.detailSpreadRadius);

                for (int dz = -radius; dz <= radius; dz++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        float distance = Mathf.Sqrt(dx * dx + dz * dz);
                        if (distance > radius) continue;

                        float spreadAlpha = detailAlpha;
                        if (radius > 0)
                        {
                            float spreadFalloff = Mathf.Clamp01(1f - (distance / (radius + 1f)));
                            spreadAlpha *= spreadFalloff;
                            if (spreadAlpha <= 0.0001f) continue;
                        }

                        int targetX = Mathf.Clamp(detailX + dx, 0, detailWidth - 1);
                        int targetZ = Mathf.Clamp(detailZ + dz, 0, detailHeight - 1);

                        for (int layer = 0; layer < detailLayers.Length; layer++)
                        {
                            var detailLayer = detailLayers[layer];
                            if (detailLayer == null) continue;
                            float layerAlpha = spreadAlpha;
                            if (noiseByLayer != null)
                            {
                                int layerIndex = (context.detailLayerIndices != null && layer < context.detailLayerIndices.Length)
                                    ? context.detailLayerIndices[layer]
                                    : context.detailLayerIndex;
                                if (noiseByLayer.TryGetValue(layerIndex, out var noise) && noise != null)
                                {
                                    var noiseTex = noise.noiseTexture;
                                    if (noiseTex != null)
                                    {
                                        if (!noiseTex.isReadable && unreadableNoiseTextureWarned != null)
                                        {
                                            int id = noiseTex.GetInstanceID();
                                            if (unreadableNoiseTextureWarned.Add(id))
                                            {
                                                Debug.LogWarning($"Detail noise texture '{noiseTex.name}' is not readable; using a temporary GPU-readback copy. Enable 'Read/Write' in import settings for better performance.");
                                            }
                                        }

                                        var readableNoise = GetReadableTexture(noiseTex);
                                        if (readableNoise != null)
                                        {
                                            bool useAlpha = ShouldUseAlpha(readableNoise);
                                            float widthMinusOne = Mathf.Max(1, detailWidth - 1);
                                            float heightMinusOne = Mathf.Max(1, detailHeight - 1);
                                            float nx = targetX / widthMinusOne;
                                            float nz = targetZ / heightMinusOne;
                                            float worldX = terrainPosX + nx * terrainSizeX;
                                            float worldZ = terrainPosZ + nz * terrainSizeZ;

                                            float worldSize = Mathf.Max(0.001f, noise.noiseWorldSizeMeters);
                                            float u = Wrap01((worldX / worldSize) + noise.noiseOffset.x);
                                            float v = Wrap01((worldZ / worldSize) + noise.noiseOffset.y);
                                            Color c = readableNoise.GetPixelBilinear(u, v);
                                            float sample = useAlpha ? c.a : c.grayscale;
                                            if (noise.noiseInvert) sample = 1f - sample;
                                            if (noise.noiseThreshold > 0f)
                                            {
                                                sample = Mathf.Clamp01(Mathf.InverseLerp(noise.noiseThreshold, 1f, sample));
                                            }

                                            layerAlpha *= sample;
                                        }
                                    }
                                }
                            }

                            if (layerAlpha <= 0.0001f) continue;

                            // Apply MapMagic-inspired density calculation
                            // Scale alpha to density range with some randomization for natural look.
                            // Note: Terrain detail layer values are per-cell instance counts; typical useful ranges are low (e.g. 0-16).
                            float baseDensity = layerAlpha * maxDensity;
                            if (maxDensity > 0)
                            {
                                float variation = (Mathf.PerlinNoise(targetX * 0.1f, targetZ * 0.1f) - 0.5f) * 0.2f * maxDensity;
                                baseDensity += variation;
                            }

                            int targetDensity = Mathf.Clamp(Mathf.FloorToInt(baseDensity), 0, maxDensity);
                            int currentDensity = detailLayer[targetZ, targetX];

                            if (context.detailMode == DetailOperationMode.Add)
                            {
                                if (targetDensity > currentDensity)
                                {
                                    detailLayer[targetZ, targetX] = targetDensity;
                                }
                            }
                            else if (context.detailMode == DetailOperationMode.Remove)
                            {
                                if (layerAlpha >= Mathf.Clamp01(context.detailRemoveThreshold))
                                {
                                    detailLayer[targetZ, targetX] = 0;
                                }
                                else
                                {
                                    int newDensity = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(currentDensity, 0, layerAlpha)), 0, currentDensity);
                                    detailLayer[targetZ, targetX] = newDensity;
                                }
                            }
                        }
                    }
                }
            });
        }
    }
}
