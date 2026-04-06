using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace GruelTerraSplines
{
    public interface ISplineStrategy
    {
        /// <summary>
        /// Single rasterization method that creates and caches spline rasterization data.
        /// This replaces all the old RasterizeTo* methods which are now obsolete.
        /// </summary>
        ISplineHeightmapCache RasterizeToCache(
            Terrain terrain,
            SplineContainer container,
            float brushHardness,
            float brushSizeMeters,
            float sampleStepMeters,
            SplineApplyMode mode,
            AnimationCurve brushSizeMultiplier
        );

        /// <summary>
        /// Get all caches for a given container (used by default GetHeightMaskTexture implementation)
        /// </summary>
        IEnumerable<ISplineHeightmapCache> GetCachesForContainer(SplineContainer container);

        /// <summary>
        /// Generate preview texture from cached heightmap data.
        /// Default implementation that works for all strategies.
        /// </summary>
        Texture2D GetHeightMaskTexture(SplineContainer container, int previewSize = 128)
        {
            foreach (var cache in GetCachesForContainer(container))
            {
                if (cache == null || !cache.isValid || cache.cachedHeights == null) continue;

                // Check if we have a cached preview texture with the correct size
                if (cache.cachedPreviewTexture != null && cache.cachedPreviewSize == previewSize)
                {
                    return cache.cachedPreviewTexture;
                }

                // Generate preview texture and cache it
                var previewTex = TerraSplinesTool.GeneratePreviewTexture(cache, previewSize);
                if (previewTex != null)
                {
                    // Clean up old texture if size changed
                    if (cache.cachedPreviewTexture != null)
                    {
                        UnityEngine.Object.DestroyImmediate(cache.cachedPreviewTexture);
                    }
                    cache.cachedPreviewTexture = previewTex;
                    cache.cachedPreviewSize = previewSize;
                }
                return previewTex;
            }
            
            return null;
        }

        void ClearCaches();

        int GetCacheCount();

        float GetCacheMemoryMB();
    }
}