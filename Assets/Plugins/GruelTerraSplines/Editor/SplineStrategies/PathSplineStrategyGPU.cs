using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GruelTerraSplines
{
    public class PathSplineStrategyGPU : ISplineStrategy
    {
        Dictionary<TerrainSplineCacheKey, PathHeightmapCache> pathHeightmapCaches = new Dictionary<TerrainSplineCacheKey, PathHeightmapCache>();
        ComputeShader pathRasterizationShader;
        bool computeShaderSupported = false;
        bool actuallyUsingGPU = false; // Track if we're actually using GPU or falling back to CPU

        // GPU data structures
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct SplinePointGPU
        {
            public Vector3 position; // 12 bytes
            public float brushSizeMultiplier; // 4 bytes

            public float normalizedDistance; // 4 bytes
            // Total: 20 bytes
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct TerrainParamsGPU
        {
            public Vector3 terrainPosition; // 12 bytes
            public Vector3 terrainSize; // 12 bytes
            public int heightmapResolution; // 4 bytes
            public float brushSizeMeters; // 4 bytes
            public float sampleStepMeters; // 4 bytes
            public float brushHardness; // 4 bytes
            public int splinePointCount; // 4 bytes
            public int minX; // 4 bytes
            public int minZ; // 4 bytes
            public int maxX; // 4 bytes

            public int maxZ; // 4 bytes
            // Total: 60 bytes
        }

        public PathSplineStrategyGPU()
        {
            // Check compute shader support
            computeShaderSupported = SystemInfo.supportsComputeShaders;

            if (computeShaderSupported)
            {
                // Load compute shader
                pathRasterizationShader = Resources.Load<ComputeShader>("Compute/PathRasterization");
                if (pathRasterizationShader == null)
                {
#if UNITY_EDITOR
                    // Fallback: try to load from the Compute folder
                    pathRasterizationShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Plugins/DKSplineTerrain/Compute/PathRasterization.compute");
#endif
                }

                if (pathRasterizationShader == null)
                {
                    Debug.LogWarning("PathRasterization compute shader not found. Falling back to CPU implementation.");
                    computeShaderSupported = false;
                }
                else
                {
                    // Verify the kernel exists
                    int kernelIndex = pathRasterizationShader.FindKernel("RasterizePath");
                    if (kernelIndex < 0)
                    {
                        Debug.LogError("PathRasterization compute shader loaded but 'RasterizePath' kernel not found!");
                        computeShaderSupported = false;
                    }
                }
            }
        }

        public IEnumerable<ISplineHeightmapCache> GetCachesForContainer(SplineContainer container)
        {
            foreach (var kvp in pathHeightmapCaches)
            {
                if (kvp.Key.container == container && kvp.Value.isValid)
                {
                    yield return kvp.Value;
                }
            }
        }

        /// <summary>
        /// Single rasterization method that creates and caches spline rasterization data.
        /// This replaces all the old RasterizeTo* methods which are now obsolete.
        /// </summary>
        public ISplineHeightmapCache RasterizeToCache(
            Terrain terrain,
            SplineContainer container,
            float brushHardness,
            float brushSizeMeters,
            float sampleStepMeters,
            SplineApplyMode mode,
            AnimationCurve brushSizeMultiplier
        )
        {
            // Create terrain-specific cache key
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            
            // Get or build cache
            PathHeightmapCache cache;
            if (!pathHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                cache.IsDirty(container, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                if (computeShaderSupported && pathRasterizationShader != null)
                {
                    try
                    {
                        cache = RasterizePathToCacheGPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                        actuallyUsingGPU = true;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[PathSplineStrategyGPU] GPU rasterization failed, falling back to CPU: {e.Message}");
                        computeShaderSupported = false; // Disable GPU for future attempts
                        actuallyUsingGPU = false;
                        cache = RasterizePathToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                    }
                }
                else
                {
                    // Fallback to CPU implementation
                    actuallyUsingGPU = false;
                    cache = RasterizePathToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                }

                pathHeightmapCaches[cacheKey] = cache;
            }

            return cache;
        }

        public void RasterizeToHeights(
            Terrain terrain,
            SplineContainer container,
            float brushHardness,
            float brushSizeMeters,
            float strength,
            float sampleStepMeters,
            int childPriority,
            int[,] writePriority,
            float[,] heights,
            SplineApplyMode mode,
            AnimationCurve brushSizeMultiplier
        )
        {
            // Create terrain-specific cache key
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            
            // Get or build heightmap cache (always needed, even for holes)
            PathHeightmapCache cache;
            if (!pathHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                cache.IsDirty(container, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                if (computeShaderSupported && pathRasterizationShader != null)
                {
                    try
                    {
                        cache = RasterizePathToCacheGPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                        actuallyUsingGPU = true;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[PathSplineStrategyGPU] GPU rasterization failed, falling back to CPU: {e.Message}");
                        computeShaderSupported = false; // Disable GPU for future attempts
                        actuallyUsingGPU = false;
                        cache = RasterizePathToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                    }
                }
                else
                {
                    // Fallback to CPU implementation
                    string reason = !computeShaderSupported ? "compute shaders not supported" : "pathRasterizationShader is null";
                    actuallyUsingGPU = false;
                    cache = RasterizePathToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                }

                pathHeightmapCaches[cacheKey] = cache;
            }

            if (!cache.isValid || cache.cachedHeights == null)
            {
                return;
            }

            // Fast blend from cache - this is the key optimization!
            int pixelsProcessed = 0;
            int pixelsWithAlpha = 0;
            float maxAlpha = 0f;
            float minAlpha = 1f;
            for (int z = cache.minZ; z <= cache.maxZ; z++)
            {
                for (int x = cache.minX; x <= cache.maxX; x++)
                {
                    pixelsProcessed++;
                    // Convert to local cache coordinates
                    int localX = x - cache.minX;
                    int localZ = z - cache.minZ;

                    float cachedAlpha = cache.cachedAlpha[localZ, localX];
                    if (cachedAlpha <= 0.0001f) continue;

                    pixelsWithAlpha++;
                    maxAlpha = Mathf.Max(maxAlpha, cachedAlpha);
                    minAlpha = Mathf.Min(minAlpha, cachedAlpha);

                    float targetHeight = cache.cachedHeights[localZ, localX];

                    // Apply with priority
                    if (childPriority >= writePriority[z, x])
                    {
                        float src = heights[z, x];
                        float a = cachedAlpha * Mathf.Clamp01(strength);
                        heights[z, x] = Mathf.Lerp(src, targetHeight, a);
                        writePriority[z, x] = childPriority;
                    }
                }
            }
        }

        public void RasterizeToHoles(
            Terrain terrain,
            SplineContainer container,
            float brushHardness,
            float brushSizeMeters,
            float strength,
            float sampleStepMeters,
            int childPriority,
            int[,] writePriority,
            bool[,] holes,
            SplineApplyMode mode,
            AnimationCurve brushSizeMultiplier
        )
        {
            // Create terrain-specific cache key
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            
            // Get or build heightmap cache (reuse existing cache for holes)
            PathHeightmapCache cache;
            if (!pathHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                cache.IsDirty(container, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                if (computeShaderSupported && pathRasterizationShader != null)
                {
                    try
                    {
                        cache = RasterizePathToCacheGPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                        actuallyUsingGPU = true;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[PathSplineStrategyGPU] GPU rasterization failed, falling back to CPU: {e.Message}");
                        computeShaderSupported = false; // Disable GPU for future attempts
                        actuallyUsingGPU = false;
                        cache = RasterizePathToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                    }
                }
                else
                {
                    // Fallback to CPU implementation
                    actuallyUsingGPU = false;
                    cache = RasterizePathToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                }

                pathHeightmapCaches[cacheKey] = cache;
            }

            if (!cache.isValid || cache.cachedAlpha == null) return;

            // Apply holes based on cached alpha values
            for (int z = cache.minZ; z <= cache.maxZ; z++)
            {
                for (int x = cache.minX; x <= cache.maxX; x++)
                {
                    // Convert to local cache coordinates
                    int localX = x - cache.minX;
                    int localZ = z - cache.minZ;

                    float cachedAlpha = cache.cachedAlpha[localZ, localX];
                    if (cachedAlpha <= 0.0001f) continue;

                    // Apply hole cutting with priority (using heightmap coordinates directly)
                    if (childPriority >= writePriority[z, x])
                    {
                        float a = cachedAlpha * Mathf.Clamp01(strength);
                        // Where alpha is high, cut hole (set to false)
                        if (a > 0.01f)
                        {
                            holes[z, x] = false;
                            writePriority[z, x] = childPriority;
                        }
                    }
                }
            }
        }

        public void RasterizeToFill(
            Terrain terrain,
            SplineContainer container,
            float brushHardness,
            float brushSizeMeters,
            float strength,
            float sampleStepMeters,
            int childPriority,
            int[,] writePriority,
            bool[,] holes,
            SplineApplyMode mode,
            AnimationCurve brushSizeMultiplier
        )
        {
            // Create terrain-specific cache key
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            
            // Get or build heightmap cache (reuse existing cache for fill)
            PathHeightmapCache cache;
            if (!pathHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                cache.IsDirty(container, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                if (computeShaderSupported && pathRasterizationShader != null)
                {
                    try
                    {
                        cache = RasterizePathToCacheGPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                        actuallyUsingGPU = true;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[PathSplineStrategyGPU] GPU rasterization failed, falling back to CPU: {e.Message}");
                        computeShaderSupported = false; // Disable GPU for future attempts
                        actuallyUsingGPU = false;
                        cache = RasterizePathToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                    }
                }
                else
                {
                    // Fallback to CPU implementation
                    actuallyUsingGPU = false;
                    cache = RasterizePathToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                }

                pathHeightmapCaches[cacheKey] = cache;
            }

            if (!cache.isValid || cache.cachedAlpha == null) return;

            // Apply fill based on cached alpha values
            for (int z = cache.minZ; z <= cache.maxZ; z++)
            {
                for (int x = cache.minX; x <= cache.maxX; x++)
                {
                    // Convert to local cache coordinates
                    int localX = x - cache.minX;
                    int localZ = z - cache.minZ;

                    float cachedAlpha = cache.cachedAlpha[localZ, localX];
                    if (cachedAlpha <= 0.0001f) continue;

                    // Apply fill with priority (using heightmap coordinates directly)
                    if (childPriority >= writePriority[z, x])
                    {
                        float a = cachedAlpha * Mathf.Clamp01(strength);
                        // Where alpha is high, fill hole (set to true)
                        if (a > 0.01f)
                        {
                            holes[z, x] = true;
                            writePriority[z, x] = childPriority;
                        }
                    }
                }
            }
        }


        public void ClearCaches()
        {
            // Clean up preview textures before clearing
            foreach (var cache in pathHeightmapCaches.Values)
            {
                if (cache.cachedPreviewTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(cache.cachedPreviewTexture);
                    cache.cachedPreviewTexture = null;
                }
            }
            pathHeightmapCaches.Clear();
        }

        public int GetCacheCount()
        {
            return pathHeightmapCaches.Count;
        }

        public float GetCacheMemoryMB()
        {
            float totalMB = 0f;
            foreach (var cache in pathHeightmapCaches.Values)
            {
                if (cache.cachedHeights != null)
                {
                    int width = cache.cachedHeights.GetLength(1);
                    int height = cache.cachedHeights.GetLength(0);
                    // 2 arrays (heights + alpha) * 4 bytes per float
                    totalMB += (width * height * 2 * 4) / (1024f * 1024f);
                }
            }

            return totalMB;
        }

        /// <summary>
        /// Get the actual backend being used (true = GPU, false = CPU fallback)
        /// </summary>
        public bool IsActuallyUsingGPU()
        {
            // Check if we can use GPU (shader loaded and compute shaders supported)
            return computeShaderSupported && pathRasterizationShader != null;
        }

        /// <summary>
        /// Get individual cache for profiling purposes
        /// </summary>
        public bool TryGetIndividualCache(Terrain terrain, SplineContainer container, out ISplineHeightmapCache cache)
        {
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            if (pathHeightmapCaches.TryGetValue(cacheKey, out var pathCache))
            {
                cache = pathCache;
                return true;
            }

            cache = null;
            return false;
        }


        public PathHeightmapCache RasterizePathToCacheGPU(
            Terrain terrain,
            SplineContainer container,
            float brushHardness,
            float brushSizeMeters,
            float sampleStepMeters,
            SplineApplyMode mode,
            AnimationCurve brushSizeMultiplier
        )
        {
            var cache = new PathHeightmapCache();

            // Collect all spline points with their normalized distances and brush size multipliers
            List<(Vector3 point, float normalizedDistance, float brushSizeMultiplier)> splinePoints = new List<(Vector3, float, float)>();
            foreach (var spline in container.Splines)
            {
                float length = SplineUtility.CalculateLength(spline, container.transform.localToWorldMatrix);
                if (length <= 0.0001f) continue;
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(0.05f, sampleStepMeters)));
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    var world = container.transform.TransformPoint(SplineUtility.EvaluatePosition(spline, t));
                    float sizeMultiplier = brushSizeMultiplier != null ? brushSizeMultiplier.Evaluate(t) : 1f;
                    splinePoints.Add((world, t, sizeMultiplier));
                }
            }

            if (splinePoints.Count == 0)
            {
                cache.isValid = false;
                return cache;
            }

            var td = terrain.terrainData;
            int res = td.heightmapResolution;

            // Calculate bounding box around all spline points with brush radius expansion
            // Use unclamped conversion to allow splines outside terrain boundaries
            float minXf = float.MaxValue, minZf = float.MaxValue, maxXf = float.MinValue, maxZf = float.MinValue;
            float maxBrushSize = brushSizeMeters;
            for (int i = 0; i < splinePoints.Count; i++)
            {
                var pointData = splinePoints[i];
                var p = pointData.point;
                float hx = TerrainCoordinates.WorldToHeightmapXUnclamped(terrain, p.x);
                float hz = TerrainCoordinates.WorldToHeightmapZUnclamped(terrain, p.z);
                minXf = Mathf.Min(minXf, hx);
                maxXf = Mathf.Max(maxXf, hx);
                minZf = Mathf.Min(minZf, hz);
                maxZf = Mathf.Max(maxZf, hz);

                // Track maximum brush size for bounding box expansion
                float actualBrushSize = brushSizeMeters * pointData.brushSizeMultiplier;
                maxBrushSize = Mathf.Max(maxBrushSize, actualBrushSize);
            }
            
            // Convert to integers after expansion
            int minX = Mathf.FloorToInt(minXf);
            int minZ = Mathf.FloorToInt(minZf);
            int maxX = Mathf.CeilToInt(maxXf);
            int maxZ = Mathf.CeilToInt(maxZf);

            // Expand bounds by maximum brush radius (don't clamp - allow extension beyond terrain for seamless gradients)
            float radiusPx = TerrainCoordinates.MetersToHeightmapPixels(terrain, maxBrushSize);
            int brushRadiusPx = Mathf.CeilToInt(radiusPx);
            minX = minX - brushRadiusPx;
            maxX = maxX + brushRadiusPx;
            minZ = minZ - brushRadiusPx;
            maxZ = maxZ + brushRadiusPx;

            // Initialize cache arrays
            int width = maxX - minX + 1;
            int height = maxZ - minZ + 1;
            cache.cachedHeights = new float[height, width];
            cache.cachedAlpha = new float[height, width];
            cache.minX = minX;
            cache.minZ = minZ;
            cache.maxX = maxX;
            cache.maxZ = maxZ;

            // Prepare GPU data
            var splinePointsGPU = new SplinePointGPU[splinePoints.Count];
            for (int i = 0; i < splinePoints.Count; i++)
            {
                var pointData = splinePoints[i];
                splinePointsGPU[i] = new SplinePointGPU
                {
                    position = pointData.point,
                    brushSizeMultiplier = pointData.brushSizeMultiplier,
                    normalizedDistance = pointData.normalizedDistance
                };
            }

            var terrainParamsGPU = new TerrainParamsGPU
            {
                terrainPosition = terrain.transform.position,
                terrainSize = td.size,
                heightmapResolution = res,
                brushSizeMeters = brushSizeMeters,
                sampleStepMeters = sampleStepMeters,
                brushHardness = brushHardness,
                splinePointCount = splinePoints.Count,
                minX = minX,
                minZ = minZ,
                maxX = maxX,
                maxZ = maxZ
            };

            // Create compute buffers
            int splinePointSize = Marshal.SizeOf<SplinePointGPU>();
            int terrainParamsSize = Marshal.SizeOf<TerrainParamsGPU>();


            // Validate struct sizes
            int expectedTerrainParamsSize = 60; // Updated from 56
            if (terrainParamsSize != expectedTerrainParamsSize)
            {
                Debug.LogError($"[PathSplineStrategyGPU] TerrainParamsGPU size mismatch! Expected {expectedTerrainParamsSize}, got {terrainParamsSize}");
            }

            var splinePointsBuffer = new ComputeBuffer(splinePoints.Count, splinePointSize);
            var terrainParamsBuffer = new ComputeBuffer(1, terrainParamsSize);

            splinePointsBuffer.SetData(splinePointsGPU);
            terrainParamsBuffer.SetData(new[] { terrainParamsGPU });

            // Create output textures
            var outputHeights = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat);
            var outputAlpha = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat);
            outputHeights.enableRandomWrite = true;
            outputAlpha.enableRandomWrite = true;
            outputHeights.Create();
            outputAlpha.Create();

            // Set up compute shader
            int kernelIndex = pathRasterizationShader.FindKernel("RasterizePath");
            if (kernelIndex < 0)
            {
                Debug.LogError("[PathSplineStrategyGPU] Failed to find 'RasterizePath' kernel in compute shader!");
                // Cleanup and fallback to CPU
                splinePointsBuffer.Release();
                terrainParamsBuffer.Release();
                outputHeights.Release();
                outputAlpha.Release();
                return RasterizePathToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
            }

            pathRasterizationShader.SetBuffer(kernelIndex, "splinePoints", splinePointsBuffer);
            pathRasterizationShader.SetBuffer(kernelIndex, "terrainParams", terrainParamsBuffer);
            pathRasterizationShader.SetTexture(kernelIndex, "outputHeights", outputHeights);
            pathRasterizationShader.SetTexture(kernelIndex, "outputAlpha", outputAlpha);

            // Dispatch compute shader
            int threadGroupsX = Mathf.CeilToInt(width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(height / 8.0f);
            pathRasterizationShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);

            // Read back results
            ReadRenderTextureToArray(outputHeights, cache.cachedHeights, width, height);
            ReadRenderTextureToArray(outputAlpha, cache.cachedAlpha, width, height);


            // Cleanup
            splinePointsBuffer.Release();
            terrainParamsBuffer.Release();
            outputHeights.Release();
            outputAlpha.Release();

            // Store transform and parameter state
            var transform = container.transform;
            cache.lastPosition = transform.position;
            cache.lastRotation = transform.rotation;
            cache.lastScale = transform.localScale;
            cache.splineVersion = TerraSplinesTool.GetSplineVersion(container);
            cache.lastBrushSize = brushSizeMeters;
            cache.lastSampleStep = sampleStepMeters;
            cache.lastMode = mode;
            cache.lastBrushSizeCurveHash = brushSizeMultiplier.GetAnimationCurveHash();
            cache.lastBrushHardness = brushHardness;
            cache.isValid = true;

            return cache;
        }

        void ReadRenderTextureToArray(RenderTexture rt, float[,] array, int width, int height)
        {
            // Create a temporary texture to read from GPU
            var tempTexture = new Texture2D(width, height, TextureFormat.RFloat, false);
            RenderTexture.active = rt;
            tempTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tempTexture.Apply();
            RenderTexture.active = null;

            // Convert texture data to array
            var pixels = tempTexture.GetPixels();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    array[y, x] = pixels[y * width + x].r;
                }
            }

#if UNITY_EDITOR
            Object.DestroyImmediate(tempTexture);
#else
			Object.Destroy(tempTexture);
#endif
        }

        // CPU fallback implementation (reuse existing CPU logic)
        public PathHeightmapCache RasterizePathToCacheCPU(
            Terrain terrain,
            SplineContainer container,
            float brushHardness,
            float brushSizeMeters,
            float sampleStepMeters,
            SplineApplyMode mode,
            AnimationCurve brushSizeMultiplier
        )
        {
            // Use the existing CPU implementation from PathSplineStrategy
            var cpuStrategy = new PathSplineStrategy();
            var cache = new PathHeightmapCache();

            // Collect all spline points with their normalized distances and brush size multipliers
            List<(Vector3 point, float normalizedDistance, float brushSizeMultiplier)> splinePoints = new List<(Vector3, float, float)>();
            foreach (var spline in container.Splines)
            {
                float length = SplineUtility.CalculateLength(spline, container.transform.localToWorldMatrix);
                if (length <= 0.0001f) continue;
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(0.05f, sampleStepMeters)));
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    var world = container.transform.TransformPoint(SplineUtility.EvaluatePosition(spline, t));
                    float sizeMultiplier = brushSizeMultiplier != null ? brushSizeMultiplier.Evaluate(t) : 1f;
                    splinePoints.Add((world, t, sizeMultiplier));
                }
            }

            if (splinePoints.Count == 0)
            {
                cache.isValid = false;
                return cache;
            }

            var td = terrain.terrainData;
            int res = td.heightmapResolution;

            // Calculate bounding box around all spline points with brush radius expansion
            // Use unclamped conversion to allow splines outside terrain boundaries
            float minXf = float.MaxValue, minZf = float.MaxValue, maxXf = float.MinValue, maxZf = float.MinValue;
            float maxBrushSize = brushSizeMeters;
            for (int i = 0; i < splinePoints.Count; i++)
            {
                var pointData = splinePoints[i];
                var p = pointData.point;
                float hx = TerrainCoordinates.WorldToHeightmapXUnclamped(terrain, p.x);
                float hz = TerrainCoordinates.WorldToHeightmapZUnclamped(terrain, p.z);
                minXf = Mathf.Min(minXf, hx);
                maxXf = Mathf.Max(maxXf, hx);
                minZf = Mathf.Min(minZf, hz);
                maxZf = Mathf.Max(maxZf, hz);

                // Track maximum brush size for bounding box expansion
                float actualBrushSize = brushSizeMeters * pointData.brushSizeMultiplier;
                maxBrushSize = Mathf.Max(maxBrushSize, actualBrushSize);
            }
            
            // Convert to integers after expansion
            int minX = Mathf.FloorToInt(minXf);
            int minZ = Mathf.FloorToInt(minZf);
            int maxX = Mathf.CeilToInt(maxXf);
            int maxZ = Mathf.CeilToInt(maxZf);

            // Expand bounds by maximum brush radius (don't clamp - allow extension beyond terrain for seamless gradients)
            float radiusPx = TerrainCoordinates.MetersToHeightmapPixels(terrain, maxBrushSize);
            int brushRadiusPx = Mathf.CeilToInt(radiusPx);
            minX = minX - brushRadiusPx;
            maxX = maxX + brushRadiusPx;
            minZ = minZ - brushRadiusPx;
            maxZ = maxZ + brushRadiusPx;

            // Initialize cache arrays
            int width = maxX - minX + 1;
            int height = maxZ - minZ + 1;
            cache.cachedHeights = new float[height, width];
            cache.cachedAlpha = new float[height, width];
            cache.minX = minX;
            cache.minZ = minZ;
            cache.maxX = maxX;
            cache.maxZ = maxZ;

            // Process all pixels in expanded area and store to cache
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3 wp = TerrainCoordinates.HeightmapToWorld(terrain, x, z);
                    Vector2 p = new Vector2(wp.x, wp.z);

                    // Find nearest spline point
                    float nearestDistSq = float.MaxValue;
                    Vector3 nearestPoint = Vector3.zero;
                    float nearestBrushSize = brushSizeMeters;
                    for (int i = 0; i < splinePoints.Count; i++)
                    {
                        var pointData = splinePoints[i];
                        var sp = pointData.point;
                        float distSq = (p.x - sp.x) * (p.x - sp.x) + (p.y - sp.z) * (p.y - sp.z);
                        if (distSq < nearestDistSq)
                        {
                            nearestDistSq = distSq;
                            nearestPoint = sp;
                            nearestBrushSize = brushSizeMeters * pointData.brushSizeMultiplier;
                        }
                    }

                    float nearestDist = Mathf.Sqrt(nearestDistSq);
                    if (nearestDist > nearestBrushSize) continue; // Outside brush range

                    // Calculate target height from nearest spline point
                    float targetHeight = Mathf.Clamp01(TerrainCoordinates.WorldYToNormalizedHeight(terrain, nearestPoint.y));

                    // Calculate brush falloff based on distance
                    float normalizedDistance = nearestDist / nearestBrushSize;
                    float finalAlpha = BrushFalloffUtils.CalculateFalloff(normalizedDistance, brushHardness);

                    if (finalAlpha <= 0.0001f) continue;

                    // Store in cache arrays (convert to local coordinates)
                    int localX = x - minX;
                    int localZ = z - minZ;
                    cache.cachedHeights[localZ, localX] = targetHeight;
                    cache.cachedAlpha[localZ, localX] = finalAlpha;
                }
            }

            // Store transform and parameter state
            var transform = container.transform;
            cache.lastPosition = transform.position;
            cache.lastRotation = transform.rotation;
            cache.lastScale = transform.localScale;
            cache.splineVersion = TerraSplinesTool.GetSplineVersion(container);
            cache.lastBrushSize = brushSizeMeters;
            cache.lastSampleStep = sampleStepMeters;
            cache.lastMode = mode;
            cache.lastBrushSizeCurveHash = brushSizeMultiplier.GetAnimationCurveHash();
            cache.lastBrushHardness = brushHardness;
            cache.isValid = true;

            return cache;
        }

        /// <summary>
        /// Store individual cache (used by other strategies for group cache building)
        /// </summary>
        public void StorePathCache(Terrain terrain, SplineContainer container, PathHeightmapCache cache)
        {
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            pathHeightmapCaches[cacheKey] = cache;
        }

        public void RasterizeToPaint(
            Terrain terrain,
            SplineContainer container,
            float brushHardness,
            float brushSizeMeters,
            float paintStrength,
            float sampleStepMeters,
            int childPriority,
            int[,] writePriority,
            float[,,] alphamaps,
            int targetLayerIndex,
            SplineApplyMode mode,
            AnimationCurve brushSizeMultiplier
        )
        {
            // Create terrain-specific cache key
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            
            // Get or build heightmap cache (reuse existing cache for paint operations)
            PathHeightmapCache cache;
            if (!pathHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                cache.IsDirty(container, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                cache = RasterizePathToCacheGPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                pathHeightmapCaches[cacheKey] = cache;
            }

            if (!cache.isValid || cache.cachedHeights == null) return;

            // Paint using cached path data
            for (int z = cache.minZ; z <= cache.maxZ; z++)
            {
                for (int x = cache.minX; x <= cache.maxX; x++)
                {
                    // Convert to local cache coordinates
                    int localX = x - cache.minX;
                    int localZ = z - cache.minZ;

                    float cachedAlpha = cache.cachedAlpha[localZ, localX];
                    if (cachedAlpha <= 0.0001f) continue;

                    // Check priority
                    if (childPriority < writePriority[z, x]) continue;

                    // Apply paint strength
                    float paintAlpha = cachedAlpha * paintStrength;
                    if (paintAlpha <= 0.0001f) continue;

                    // Update write priority
                    writePriority[z, x] = childPriority;

                    // Smooth paint blending with gradual layer clearing
                    float currentTargetValue = alphamaps[z, x, targetLayerIndex];
                    float newTargetValue = Mathf.Lerp(currentTargetValue, 1f, paintAlpha);

                    // Calculate how much we need to reduce other layers to maintain smooth falloff
                    float reductionFactor = paintAlpha * paintAlpha; // Quadratic falloff for smoother edges

                    // Gradually reduce other layers based on paint strength
                    for (int l = 0; l < alphamaps.GetLength(2); l++)
                    {
                        if (l != targetLayerIndex)
                        {
                            alphamaps[z, x, l] *= (1f - reductionFactor);
                        }
                    }

                    // Set target layer
                    alphamaps[z, x, targetLayerIndex] = newTargetValue;

                    // Only normalize if we have significant other layer values
                    float otherLayersSum = 0f;
                    for (int l = 0; l < alphamaps.GetLength(2); l++)
                    {
                        if (l != targetLayerIndex)
                            otherLayersSum += alphamaps[z, x, l];
                    }

                    if (otherLayersSum > 0.01f) // Only normalize if other layers have meaningful values
                    {
                        TerraSplinesTool.NormalizeAlphamaps(alphamaps, x, z);
                    }
                }
            }
        }

        public void RasterizeToHeightAndPaint(
            Terrain terrain,
            SplineContainer container,
            float brushHardness,
            float brushSizeMeters,
            float heightStrength,
            float paintStrength,
            float sampleStepMeters,
            int childPriority,
            int[,] writePriority,
            float[,] heights,
            float[,,] alphamaps,
            int targetLayerIndex,
            SplineApplyMode mode,
            AnimationCurve brushSizeMultiplier
        )
        {
            // Get or build heightmap cache
            PathHeightmapCache cache;
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            if (!pathHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                cache.IsDirty(container, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                cache = RasterizePathToCacheGPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                pathHeightmapCaches[cacheKey] = cache;
            }

            if (!cache.isValid || cache.cachedHeights == null) return;

            // Apply both height and paint modifications
            for (int z = cache.minZ; z <= cache.maxZ; z++)
            {
                for (int x = cache.minX; x <= cache.maxX; x++)
                {
                    // Convert to local cache coordinates
                    int localX = x - cache.minX;
                    int localZ = z - cache.minZ;

                    float cachedAlpha = cache.cachedAlpha[localZ, localX];
                    if (cachedAlpha <= 0.0001f) continue;

                    // Check priority
                    if (childPriority < writePriority[z, x]) continue;

                    // Apply height modification
                    if (heights != null)
                    {
                        float cachedHeight = cache.cachedHeights[localZ, localX];
                        float heightAlpha = cachedAlpha * heightStrength;
                        heights[z, x] = Mathf.Lerp(heights[z, x], cachedHeight, heightAlpha);
                    }

                    // Apply paint modification
                    if (alphamaps != null)
                    {
                        float paintAlpha = cachedAlpha * paintStrength;

                        // Smooth paint blending with gradual layer clearing
                        float currentTargetValue = alphamaps[z, x, targetLayerIndex];
                        float newTargetValue = Mathf.Lerp(currentTargetValue, 1f, paintAlpha);

                        // Calculate how much we need to reduce other layers to maintain smooth falloff
                        float reductionFactor = paintAlpha * paintAlpha; // Quadratic falloff for smoother edges

                        // Gradually reduce other layers based on paint strength
                        for (int l = 0; l < alphamaps.GetLength(2); l++)
                        {
                            if (l != targetLayerIndex)
                            {
                                alphamaps[z, x, l] *= (1f - reductionFactor);
                            }
                        }

                        // Set target layer
                        alphamaps[z, x, targetLayerIndex] = newTargetValue;

                        // Only normalize if we have significant other layer values
                        float otherLayersSum = 0f;
                        for (int l = 0; l < alphamaps.GetLength(2); l++)
                        {
                            if (l != targetLayerIndex)
                                otherLayersSum += alphamaps[z, x, l];
                        }

                        if (otherLayersSum > 0.01f) // Only normalize if other layers have meaningful values
                        {
                            TerraSplinesTool.NormalizeAlphamaps(alphamaps, x, z);
                        }
                    }

                    // Update write priority
                    writePriority[z, x] = childPriority;
                }
            }
        }
    }
}