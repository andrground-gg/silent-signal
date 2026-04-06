using UnityEngine;

namespace GruelTerraSplines
{
    public class HeightOperationApplier : OperationApplierBase
    {
        public override void Apply(
            Terrain terrain,
            ISplineHeightmapCache cache,
            int childPriority,
            int[,] writePriority,
            OperationContext context)
        {
            if (context.heights == null || !ValidateCache(cache) || cache.cachedHeights == null) return;

            ProcessCacheRegion(cache, childPriority, writePriority, terrain, context, (x, z, localX, localZ, cachedAlpha) =>
            {
                float targetHeight = cache.cachedHeights[localZ, localX];
                float src = context.heights[z, x];
                float a = cachedAlpha * Mathf.Clamp01(context.strength);
                context.heights[z, x] = Mathf.Lerp(src, targetHeight, a);
                writePriority[z, x] = childPriority;
            });
        }
    }
}