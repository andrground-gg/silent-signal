using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace GruelTerraSplines
{
    public class PathSplineStrategy : ISplineStrategy
    {
        Dictionary<TerrainSplineCacheKey, SplineRasterCache> pathHeightmapCaches = new Dictionary<TerrainSplineCacheKey, SplineRasterCache>();

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
            var cache = RasterizePathToCache(terrain, container, brushHardness, brushSizeMeters, sampleStepMeters, mode, brushSizeMultiplier);
            pathHeightmapCaches[cacheKey] = cache;
            return cache;
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

        public SplineRasterCache RasterizePathToCache(
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
    }
}