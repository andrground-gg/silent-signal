using UnityEngine;

namespace GruelTerraSplines
{
    public abstract class OperationApplierBase : IOperationApplier
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

        public abstract void Apply(
            Terrain terrain,
            ISplineHeightmapCache cache,
            int childPriority,
            int[,] writePriority,
            OperationContext context
        );

        protected bool ValidateCache(ISplineHeightmapCache cache)
        {
            return cache != null && cache.isValid && cache.cachedAlpha != null;
        }

        protected void ProcessCacheRegion(
            ISplineHeightmapCache cache,
            int childPriority,
            int[,] writePriority,
            System.Action<int, int, int, int, float> processPixel)
        {
            ProcessCacheRegion(cache, childPriority, writePriority, null, null, processPixel);
        }

        protected void ProcessCacheRegion(
            ISplineHeightmapCache cache,
            int childPriority,
            int[,] writePriority,
            Terrain terrain,
            System.Action<int, int, int, int, float> processPixel)
        {
            ProcessCacheRegion(cache, childPriority, writePriority, terrain, null, processPixel);
        }

        protected void ProcessCacheRegion(
            ISplineHeightmapCache cache,
            int childPriority,
            int[,] writePriority,
            Terrain terrain,
            OperationContext context,
            System.Action<int, int, int, int, float> processPixel)
        {
            if (!ValidateCache(cache)) return;

            // Get terrain bounds if provided
            int terrainRes = 0;
            float terrainPosX = 0f;
            float terrainPosZ = 0f;
            float terrainSizeX = 1f;
            float terrainSizeZ = 1f;
            if (terrain != null && terrain.terrainData != null)
            {
                terrainRes = terrain.terrainData.heightmapResolution;
                terrainPosX = terrain.transform.position.x;
                terrainPosZ = terrain.transform.position.z;
                terrainSizeX = Mathf.Max(0.0001f, terrain.terrainData.size.x);
                terrainSizeZ = Mathf.Max(0.0001f, terrain.terrainData.size.z);
            }

            var brushNoise = context?.brushNoise;
            var noiseTexture = brushNoise?.noiseTexture;
            Texture2D readableNoise = null;
            bool useAlpha = false;
            bool warnedNoise = false;

            float worldSize = 1f;
            float noiseStrength = 1f;
            float noiseEdge = 0f;
            Vector2 noiseOffset = Vector2.zero;
            bool noiseInvert = false;
            if (noiseTexture != null)
            {
                readableNoise = GetReadableTexture(noiseTexture);
                useAlpha = readableNoise != null && ShouldUseAlpha(readableNoise);
                worldSize = Mathf.Max(0.001f, brushNoise.noiseWorldSizeMeters);
                noiseStrength = Mathf.Clamp01(brushNoise.noiseStrength);
                noiseEdge = Mathf.Clamp(brushNoise.noiseEdge, -1f, 1f);

                noiseOffset = brushNoise.noiseOffset;
                noiseInvert = brushNoise.noiseInvert;
            }

            float Wrap01(float t) => t - Mathf.Floor(t);

            for (int z = cache.minZ; z <= cache.maxZ; z++)
            {
                for (int x = cache.minX; x <= cache.maxX; x++)
                {
                    // Convert to local cache coordinates
                    int localX = x - cache.minX;
                    int localZ = z - cache.minZ;

                    // Bounds check: skip if outside terrain bounds (for seamless gradient across terrain boundaries)
                    if (terrain != null && (x < 0 || x >= terrainRes || z < 0 || z >= terrainRes))
                    {
                        continue;
                    }

                    float cachedAlpha = cache.cachedAlpha[localZ, localX];
                    if (cachedAlpha <= 0.0001f) continue;

                    // Check priority (only if within bounds)
                    if (x >= 0 && x < writePriority.GetLength(1) && z >= 0 && z < writePriority.GetLength(0))
                    {
                        if (childPriority < writePriority[z, x]) continue;
                    }

                    if (readableNoise != null)
                    {
                        if (!noiseTexture.isReadable && !warnedNoise)
                        {
                            warnedNoise = true;
                            Debug.LogWarning($"Brush noise texture '{noiseTexture.name}' is not readable; using a temporary GPU-readback copy. Enable 'Read/Write' in import settings for better performance.");
                        }

                        float resMinusOne = Mathf.Max(1f, (terrainRes > 0 ? terrainRes - 1f : 1f));
                        float nx = terrainRes > 1 ? x / resMinusOne : 0f;
                        float nz = terrainRes > 1 ? z / resMinusOne : 0f;
                        float worldX = terrainPosX + nx * terrainSizeX;
                        float worldZ = terrainPosZ + nz * terrainSizeZ;

                        float u = Wrap01((worldX / worldSize) + noiseOffset.x);
                        float v = Wrap01((worldZ / worldSize) + noiseOffset.y);
                        Color c = readableNoise.GetPixelBilinear(u, v);
                        float sample = useAlpha ? c.a : c.grayscale;
                        if (noiseInvert) sample = 1f - sample;

                        float alpha = Mathf.Clamp01(cachedAlpha);
                        float edgeFactor = noiseEdge >= 0f
                            ? 1f - (noiseEdge * alpha)
                            : 1f - (-noiseEdge * (1f - alpha));

                        sample = Mathf.Lerp(1f, sample, noiseStrength * edgeFactor);
                        cachedAlpha *= sample;
                        if (cachedAlpha <= 0.0001f) continue;
                    }

                    processPixel(x, z, localX, localZ, cachedAlpha);
                }
            }
        }
    }
}