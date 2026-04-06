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
    public class ShapeSplineStrategyGPU : ISplineStrategy
    {
        Dictionary<TerrainSplineCacheKey, ShapeHeightmapCache> shapeHeightmapCaches = new Dictionary<TerrainSplineCacheKey, ShapeHeightmapCache>();
        ComputeShader shapeRasterizationShader;
        bool computeShaderSupported = false;
        bool actuallyUsingGPU = false; // Track if we're actually using GPU or falling back to CPU

        // GPU data structures
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BorderPointGPU
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
            public int borderPointCount; // 4 bytes
            public int minX; // 4 bytes
            public int minZ; // 4 bytes
            public int maxX; // 4 bytes

            public int maxZ; // 4 bytes
            // Total: 60 bytes
        }

        public ShapeSplineStrategyGPU()
        {
            // Check compute shader support
            computeShaderSupported = SystemInfo.supportsComputeShaders;

            if (computeShaderSupported)
            {
                // Load compute shader
                shapeRasterizationShader = Resources.Load<ComputeShader>("Compute/ShapeRasterization");
                if (shapeRasterizationShader == null)
                {
#if UNITY_EDITOR
                    // Fallback: try to load from the Compute folder
                    shapeRasterizationShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Plugins/DKSplineTerrain/Compute/ShapeRasterization.compute");
#endif
                }

                if (shapeRasterizationShader == null)
                {
                    Debug.LogWarning("ShapeRasterization compute shader not found. Falling back to CPU implementation.");
                    computeShaderSupported = false;
                }
                else
                {
                    // Verify the kernel exists
                    int kernelIndex = shapeRasterizationShader.FindKernel("RasterizeShape");
                    if (kernelIndex < 0)
                    {
                        Debug.LogError("ShapeRasterization compute shader loaded but 'RasterizeShape' kernel not found!");
                        computeShaderSupported = false;
                    }
                }
            }
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
            // Get or build heightmap cache (always needed, even for holes)
            ShapeHeightmapCache cache;
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            if (!shapeHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                TerraSplinesTool.IsHeightmapCacheDirty(container, cache, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                if (computeShaderSupported && shapeRasterizationShader != null)
                {
                    try
                    {
                        cache = RasterizeShapeToCacheGPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                        actuallyUsingGPU = true;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[ShapeSplineStrategyGPU] GPU rasterization failed, falling back to CPU: {e.Message}");
                        computeShaderSupported = false; // Disable GPU for future attempts
                        actuallyUsingGPU = false;
                        cache = RasterizeShapeToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                    }
                }
                else
                {
                    // Fallback to CPU implementation
                    string reason = !computeShaderSupported ? "compute shaders not supported" : "shapeRasterizationShader is null";
                    actuallyUsingGPU = false;
                    cache = RasterizeShapeToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                }

                shapeHeightmapCaches[cacheKey] = cache;
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

                    // For outside polygon areas, blend with original terrain height
                    // (inside polygon areas already have the correct target height)
                    float originalHeight = heights[z, x];
                    float finalTargetHeight = Mathf.Lerp(originalHeight, targetHeight, cachedAlpha);

                    // Apply with priority
                    if (childPriority >= writePriority[z, x])
                    {
                        float src = heights[z, x];
                        float a = cachedAlpha * Mathf.Clamp01(strength);
                        heights[z, x] = Mathf.Lerp(src, finalTargetHeight, a);
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
            // Get or build heightmap cache (reuse existing cache for holes)
            ShapeHeightmapCache cache;
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            if (!shapeHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                TerraSplinesTool.IsHeightmapCacheDirty(container, cache, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                if (computeShaderSupported && shapeRasterizationShader != null)
                {
                    try
                    {
                        cache = RasterizeShapeToCacheGPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                        actuallyUsingGPU = true;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[ShapeSplineStrategyGPU] GPU rasterization failed, falling back to CPU: {e.Message}");
                        computeShaderSupported = false; // Disable GPU for future attempts
                        actuallyUsingGPU = false;
                        cache = RasterizeShapeToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                    }
                }
                else
                {
                    // Fallback to CPU implementation
                    actuallyUsingGPU = false;
                    cache = RasterizeShapeToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                }

                shapeHeightmapCaches[cacheKey] = cache;
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
            // Get or build heightmap cache (reuse existing cache for fill)
            ShapeHeightmapCache cache;
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            if (!shapeHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                TerraSplinesTool.IsHeightmapCacheDirty(container, cache, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                if (computeShaderSupported && shapeRasterizationShader != null)
                {
                    try
                    {
                        cache = RasterizeShapeToCacheGPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                        actuallyUsingGPU = true;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[ShapeSplineStrategyGPU] GPU rasterization failed, falling back to CPU: {e.Message}");
                        computeShaderSupported = false; // Disable GPU for future attempts
                        actuallyUsingGPU = false;
                        cache = RasterizeShapeToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                    }
                }
                else
                {
                    // Fallback to CPU implementation
                    actuallyUsingGPU = false;
                    cache = RasterizeShapeToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                }

                shapeHeightmapCaches[cacheKey] = cache;
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


        public IEnumerable<ISplineHeightmapCache> GetCachesForContainer(SplineContainer container)
        {
            foreach (var kvp in shapeHeightmapCaches)
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
            // Get or build cache
            ShapeHeightmapCache cache;
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            if (!shapeHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                TerraSplinesTool.IsHeightmapCacheDirty(container, cache, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                if (computeShaderSupported && shapeRasterizationShader != null)
                {
                    try
                    {
                        cache = RasterizeShapeToCacheGPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                        actuallyUsingGPU = true;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[ShapeSplineStrategyGPU] GPU rasterization failed, falling back to CPU: {e.Message}");
                        computeShaderSupported = false; // Disable GPU for future attempts
                        actuallyUsingGPU = false;
                        cache = RasterizeShapeToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                    }
                }
                else
                {
                    // Fallback to CPU implementation
                    actuallyUsingGPU = false;
                    cache = RasterizeShapeToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                }

                shapeHeightmapCaches[cacheKey] = cache;
            }

            return cache;
        }

        public void ClearCaches()
        {
            // Clean up preview textures before clearing
            foreach (var cache in shapeHeightmapCaches.Values)
            {
                if (cache.cachedPreviewTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(cache.cachedPreviewTexture);
                    cache.cachedPreviewTexture = null;
                }
            }
            shapeHeightmapCaches.Clear();
        }

        public int GetCacheCount()
        {
            return shapeHeightmapCaches.Count;
        }

        public float GetCacheMemoryMB()
        {
            float totalMB = 0f;
            foreach (var cache in shapeHeightmapCaches.Values)
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
            return computeShaderSupported && shapeRasterizationShader != null;
        }

        /// <summary>
        /// Get individual cache for profiling purposes
        /// </summary>
        public bool TryGetIndividualCache(Terrain terrain, SplineContainer container, out ISplineHeightmapCache cache)
        {
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            if (shapeHeightmapCaches.TryGetValue(cacheKey, out var shapeCache))
            {
                cache = shapeCache;
                return true;
            }

            cache = null;
            return false;
        }

        /// <summary>
        /// Store individual cache (used by other strategies for group cache building)
        /// </summary>
        public void StoreShapeCache(Terrain terrain, SplineContainer container, ShapeHeightmapCache cache)
        {
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            shapeHeightmapCaches[cacheKey] = cache;
        }


        public ShapeHeightmapCache RasterizeShapeToCacheGPU(
            Terrain terrain,
            SplineContainer container,
            float brushHardness,
            float brushSizeMeters,
            float sampleStepMeters,
            SplineApplyMode mode,
            AnimationCurve brushSizeMultiplier
        )
        {
            var cache = new ShapeHeightmapCache();

            // Collect polygon points from closed splines with their normalized distances and brush size multipliers
            List<(Vector3 point, float normalizedDistance, float brushSizeMultiplier)> borderPoints = new List<(Vector3, float, float)>();
            foreach (var spline in container.Splines)
            {
                if (!spline.Closed) continue;
                float length = SplineUtility.CalculateLength(spline, container.transform.localToWorldMatrix);
                int steps = Mathf.Max(16, Mathf.CeilToInt(length / Mathf.Max(0.1f, sampleStepMeters)));
                for (int i = 0; i < steps; i++)
                {
                    float t = (float)i / steps;
                    var world = container.transform.TransformPoint(SplineUtility.EvaluatePosition(spline, t));
                    float sizeMultiplier = brushSizeMultiplier != null ? brushSizeMultiplier.Evaluate(t) : 1f;
                    borderPoints.Add((world, t, sizeMultiplier));
                }
            }

            if (borderPoints.Count < 3)
            {
                cache.isValid = false;
                return cache;
            }

            var td = terrain.terrainData;
            int res = td.heightmapResolution;

            // Convert to 2D points and heights
            List<Vector2> borderXZ = new List<Vector2>(borderPoints.Count);
            List<float> borderH = new List<float>(borderPoints.Count);
            float maxBrushSize = brushSizeMeters;
            for (int i = 0; i < borderPoints.Count; i++)
            {
                var pointData = borderPoints[i];
                var p = pointData.point;
                borderXZ.Add(new Vector2(p.x, p.z));
                // For shape mode, use a fixed elevated height instead of actual spline heights
                // This creates a uniform elevated surface like the CPU version
                float elevatedHeight = 0.75f; // Fixed elevated height for shape mode
                borderH.Add(elevatedHeight);

                // Track maximum brush size for bounding box expansion
                float actualBrushSize = brushSizeMeters * pointData.brushSizeMultiplier;
                maxBrushSize = Mathf.Max(maxBrushSize, actualBrushSize);
            }

            // Calculate bounding box and expand by brush radius
            // Use unclamped conversion to allow splines outside terrain boundaries
            float minXf = float.MaxValue, minZf = float.MaxValue, maxXf = float.MinValue, maxZf = float.MinValue;
            for (int i = 0; i < borderPoints.Count; i++)
            {
                var pointData = borderPoints[i];
                var p = pointData.point;
                float hx = TerrainCoordinates.WorldToHeightmapXUnclamped(terrain, p.x);
                float hz = TerrainCoordinates.WorldToHeightmapZUnclamped(terrain, p.z);
                minXf = Mathf.Min(minXf, hx);
                maxXf = Mathf.Max(maxXf, hx);
                minZf = Mathf.Min(minZf, hz);
                maxZf = Mathf.Max(maxZf, hz);
            }
            
            // Convert to integers after expansion
            int minX = Mathf.FloorToInt(minXf);
            int minZ = Mathf.FloorToInt(minZf);
            int maxX = Mathf.CeilToInt(maxXf);
            int maxZ = Mathf.CeilToInt(maxZf);

            // Expand bounds by maximum brush radius to include tapering area (don't clamp - allow extension beyond terrain for seamless gradients)
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

            // Validate input data before sending to GPU

            // Check if we have valid border points
            if (borderPoints.Count < 3)
            {
                cache.isValid = false;
                return cache;
            }

            // Validate border point positions
            Vector3 minPos = Vector3.positiveInfinity;
            Vector3 maxPos = Vector3.negativeInfinity;
            for (int i = 0; i < borderPoints.Count; i++)
            {
                var pos = borderPoints[i].point;
                minPos = Vector3.Min(minPos, pos);
                maxPos = Vector3.Max(maxPos, pos);
            }

            // Prepare GPU data
            var borderPointsGPU = new BorderPointGPU[borderPoints.Count];
            for (int i = 0; i < borderPoints.Count; i++)
            {
                var pointData = borderPoints[i];
                borderPointsGPU[i] = new BorderPointGPU
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
                borderPointCount = borderPoints.Count,
                minX = minX,
                minZ = minZ,
                maxX = maxX,
                maxZ = maxZ
            };

            // Validate terrain parameters

            // Create compute buffers
            int borderPointSize = Marshal.SizeOf<BorderPointGPU>();
            int terrainParamsSize = Marshal.SizeOf<TerrainParamsGPU>();


            // Validate struct sizes
            int expectedTerrainParamsSize = 60; // Updated from 56
            if (terrainParamsSize != expectedTerrainParamsSize)
            {
            }

            var borderPointsBuffer = new ComputeBuffer(borderPoints.Count, borderPointSize);
            var terrainParamsBuffer = new ComputeBuffer(1, terrainParamsSize);

            borderPointsBuffer.SetData(borderPointsGPU);
            terrainParamsBuffer.SetData(new[] { terrainParamsGPU });

            // Create output textures
            var outputHeights = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat);
            var outputAlpha = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat);
            outputHeights.enableRandomWrite = true;
            outputAlpha.enableRandomWrite = true;
            outputHeights.Create();
            outputAlpha.Create();

            // Set up compute shader
            int kernelIndex = shapeRasterizationShader.FindKernel("RasterizeShape");
            if (kernelIndex < 0)
            {
                // Cleanup and fallback to CPU
                borderPointsBuffer.Release();
                terrainParamsBuffer.Release();
                outputHeights.Release();
                outputAlpha.Release();
                return RasterizeShapeToCacheCPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
            }

            shapeRasterizationShader.SetBuffer(kernelIndex, "borderPoints", borderPointsBuffer);
            shapeRasterizationShader.SetBuffer(kernelIndex, "terrainParams", terrainParamsBuffer);
            shapeRasterizationShader.SetTexture(kernelIndex, "outputHeights", outputHeights);
            shapeRasterizationShader.SetTexture(kernelIndex, "outputAlpha", outputAlpha);

            // Dispatch compute shader
            int threadGroupsX = Mathf.CeilToInt(width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(height / 8.0f);

            // Check if shader is valid before dispatch
            if (shapeRasterizationShader == null)
            {
                cache.isValid = false;
                return cache;
            }

            // Debug: Verify shader is loaded and has the correct kernel
            if (kernelIndex < 0)
            {
                cache.isValid = false;
                return cache;
            }

            try
            {
                shapeRasterizationShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);
            }
            catch (System.Exception e)
            {
                cache.isValid = false;
                return cache;
            }


            // Mark that we're actually using GPU
            actuallyUsingGPU = true;

            // Read back results

            try
            {
                ReadRenderTextureToArray(outputHeights, cache.cachedHeights, width, height);
            }
            catch (System.Exception e)
            {
            }

            try
            {
                ReadRenderTextureToArray(outputAlpha, cache.cachedAlpha, width, height);
            }
            catch (System.Exception e)
            {
            }

            // Validate GPU results
            float minHeight = float.MaxValue, maxHeight = float.MinValue;
            float minAlpha = float.MaxValue, maxAlpha = float.MinValue;
            int nonZeroAlphaCount = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float pixelHeight = cache.cachedHeights[y, x];
                    float pixelAlpha = cache.cachedAlpha[y, x];

                    minHeight = Mathf.Min(minHeight, pixelHeight);
                    maxHeight = Mathf.Max(maxHeight, pixelHeight);
                    minAlpha = Mathf.Min(minAlpha, pixelAlpha);
                    maxAlpha = Mathf.Max(maxAlpha, pixelAlpha);

                    if (pixelAlpha > 0.0001f) nonZeroAlphaCount++;
                }
            }


            // Cleanup
            borderPointsBuffer.Release();
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
        public ShapeHeightmapCache RasterizeShapeToCacheCPU(
            Terrain terrain,
            SplineContainer container,
            float brushHardness,
            float brushSizeMeters,
            float sampleStepMeters,
            SplineApplyMode mode,
            AnimationCurve brushSizeMultiplier
        )
        {
            var cache = new ShapeHeightmapCache();

            // Collect polygon points from closed splines with their normalized distances and brush size multipliers
            List<(Vector3 point, float normalizedDistance, float brushSizeMultiplier)> borderPoints = new List<(Vector3, float, float)>();
            foreach (var spline in container.Splines)
            {
                if (!spline.Closed) continue;
                float length = SplineUtility.CalculateLength(spline, container.transform.localToWorldMatrix);
                int steps = Mathf.Max(16, Mathf.CeilToInt(length / Mathf.Max(0.1f, sampleStepMeters)));
                for (int i = 0; i < steps; i++)
                {
                    float t = (float)i / steps;
                    var world = container.transform.TransformPoint(SplineUtility.EvaluatePosition(spline, t));
                    float sizeMultiplier = brushSizeMultiplier != null ? brushSizeMultiplier.Evaluate(t) : 1f;
                    borderPoints.Add((world, t, sizeMultiplier));
                }
            }

            if (borderPoints.Count < 3)
            {
                cache.isValid = false;
                return cache;
            }

            var td = terrain.terrainData;
            int res = td.heightmapResolution;

            // Convert to 2D points and heights
            List<Vector2> borderXZ = new List<Vector2>(borderPoints.Count);
            List<float> borderH = new List<float>(borderPoints.Count);
            float maxBrushSize = brushSizeMeters;
            for (int i = 0; i < borderPoints.Count; i++)
            {
                var pointData = borderPoints[i];
                var p = pointData.point;
                borderXZ.Add(new Vector2(p.x, p.z));
                // For shape mode, use a fixed elevated height instead of actual spline heights
                // This creates a uniform elevated surface like the CPU version
                float elevatedHeight = 0.75f; // Fixed elevated height for shape mode
                borderH.Add(elevatedHeight);

                // Track maximum brush size for bounding box expansion
                float actualBrushSize = brushSizeMeters * pointData.brushSizeMultiplier;
                maxBrushSize = Mathf.Max(maxBrushSize, actualBrushSize);
            }

            // Calculate bounding box and expand by brush radius
            // Use unclamped conversion to allow splines outside terrain boundaries
            float minXf = float.MaxValue, minZf = float.MaxValue, maxXf = float.MinValue, maxZf = float.MinValue;
            for (int i = 0; i < borderPoints.Count; i++)
            {
                var pointData = borderPoints[i];
                var p = pointData.point;
                float hx = TerrainCoordinates.WorldToHeightmapXUnclamped(terrain, p.x);
                float hz = TerrainCoordinates.WorldToHeightmapZUnclamped(terrain, p.z);
                minXf = Mathf.Min(minXf, hx);
                maxXf = Mathf.Max(maxXf, hx);
                minZf = Mathf.Min(minZf, hz);
                maxZf = Mathf.Max(maxZf, hz);
            }
            
            // Convert to integers after expansion
            int minX = Mathf.FloorToInt(minXf);
            int minZ = Mathf.FloorToInt(minZf);
            int maxX = Mathf.CeilToInt(maxXf);
            int maxZ = Mathf.CeilToInt(maxZf);

            // Expand bounds by maximum brush radius to include tapering area (don't clamp - allow extension beyond terrain for seamless gradients)
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

                    // Calculate signed distance to polygon (negative inside, positive outside)
                    float signedDist = PolygonUtils.SignedDistanceToPolygon(p, borderXZ);
                    float absDist = Mathf.Abs(signedDist);

                    // Find nearest border point to get its brush size multiplier
                    float nearestDistSq = float.MaxValue;
                    float nearestBrushSize = brushSizeMeters;
                    for (int i = 0; i < borderPoints.Count; i++)
                    {
                        var pointData = borderPoints[i];
                        var bp = pointData.point;
                        float distSq = (p.x - bp.x) * (p.x - bp.x) + (p.y - bp.z) * (p.y - bp.z);
                        if (distSq < nearestDistSq)
                        {
                            nearestDistSq = distSq;
                            nearestBrushSize = brushSizeMeters * pointData.brushSizeMultiplier;
                        }
                    }

                    // Only skip if outside AND beyond brush range
                    if (signedDist > 0 && absDist > nearestBrushSize) continue;

                    // Determine target height and brush strength based on position
                    float targetHeight;
                    float finalAlpha;

                    if (signedDist <= 0) // Inside polygon
                    {
                        // For convex surface: interpolate border heights and apply them with full strength
                        targetHeight = InterpolationUtils.InterpolateSmooth(p, borderXZ, borderH);
                        finalAlpha = 1f; // Full strength inside for convex surface
                    }
                    else // Outside polygon
                    {
                        // Taper from border height to original terrain height using brush texture
                        float borderHeight = InterpolationUtils.InterpolateIDW(p, borderXZ, borderH, 8);

                        // Calculate brush falloff for outside tapering using nearest brush size
                        float normalizedDistance = Mathf.Abs(signedDist) / nearestBrushSize;
                        finalAlpha = BrushFalloffUtils.CalculateFalloff(normalizedDistance, brushHardness);

                        if (finalAlpha <= 0.0001f) continue;

                        targetHeight = borderHeight; // Store target height, will blend with original later
                    }

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
            // Get or build heightmap cache (reuse existing cache for paint operations)
            ShapeHeightmapCache cache;
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
                if (!shapeHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                    cache.IsDirty(container, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                cache = RasterizeShapeToCacheGPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                shapeHeightmapCaches[cacheKey] = cache;
            }

            if (!cache.isValid || cache.cachedHeights == null) return;

            // Paint using cached shape data
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
            ShapeHeightmapCache cache;
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            if (!shapeHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                cache.IsDirty(container, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                cache = RasterizeShapeToCacheGPU(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                shapeHeightmapCaches[cacheKey] = cache;
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