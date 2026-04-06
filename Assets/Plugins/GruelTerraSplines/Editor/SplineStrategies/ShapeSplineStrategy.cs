using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace GruelTerraSplines
{
    public class ShapeSplineStrategy : ISplineStrategy
    {
        Dictionary<TerrainSplineCacheKey, SplineRasterCache> shapeHeightmapCaches = new Dictionary<TerrainSplineCacheKey, SplineRasterCache>();

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

        public ISplineHeightmapCache RasterizeToCache(
            Terrain terrain,
            SplineContainer container,
            float brushHardness,
            float brushSizeMeters,
            float sampleStepMeters,
            SplineApplyMode mode,
            AnimationCurve brushSizeMultiplier)
        {
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            var cache = RasterizeShapeToCache(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
            shapeHeightmapCaches[cacheKey] = cache;
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
            SplineRasterCache cache;
            if (!shapeHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                TerraSplinesTool.IsHeightmapCacheDirty(container, cache, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                cache = RasterizeShapeToCache(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
                shapeHeightmapCaches[cacheKey] = cache;
            }

            if (!cache.isValid || cache.cachedHeights == null) return;

            // Fast blend from cache - this is the key optimization!
            for (int z = cache.minZ; z <= cache.maxZ; z++)
            {
                for (int x = cache.minX; x <= cache.maxX; x++)
                {
                    // Convert to local cache coordinates
                    int localX = x - cache.minX;
                    int localZ = z - cache.minZ;

                    float cachedAlpha = cache.cachedAlpha[localZ, localX];
                    if (cachedAlpha <= 0.0001f) continue;

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
            // Create terrain-specific cache key
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            
            // Get or build heightmap cache (reuse existing cache for holes)
            SplineRasterCache cache;
            if (!shapeHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                TerraSplinesTool.IsHeightmapCacheDirty(container, cache, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                cache = RasterizeShapeToCache(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
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
            // Create terrain-specific cache key
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            
            // Get or build heightmap cache (reuse existing cache for fill)
            SplineRasterCache cache;
            if (!shapeHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                TerraSplinesTool.IsHeightmapCacheDirty(container, cache, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                cache = RasterizeShapeToCache(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
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
        public void StoreShapeCache(Terrain terrain, SplineContainer container, SplineRasterCache cache)
        {
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            shapeHeightmapCaches[cacheKey] = cache;
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

        public SplineRasterCache RasterizeShapeToCache(
            Terrain terrain,
            SplineContainer container,
            float brushHardness,
            float brushSizeMeters,
            float sampleStepMeters,
            SplineApplyMode mode,
            AnimationCurve brushSizeMultiplier
        )
        {
            var cache = new SplineRasterCache();

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
                borderH.Add(Mathf.Clamp01(TerrainCoordinates.WorldYToNormalizedHeight(terrain, p.y)));

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
            // Create terrain-specific cache key
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            
            // Get or build heightmap cache (reuse existing cache for paint operations)
            SplineRasterCache cache;
            if (!shapeHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                cache.IsDirty(container, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                cache = RasterizeShapeToCache(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
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
            // Create terrain-specific cache key
            var cacheKey = new TerrainSplineCacheKey(terrain, container);
            
            // Get or build heightmap cache
            SplineRasterCache cache;
            if (!shapeHeightmapCaches.TryGetValue(cacheKey, out cache) ||
                cache.IsDirty(container, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier, brushHardness))
            {
                cache = RasterizeShapeToCache(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
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