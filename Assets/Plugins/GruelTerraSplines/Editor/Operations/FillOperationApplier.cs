using UnityEngine;

namespace GruelTerraSplines
{
    public class FillOperationApplier : OperationApplierBase
    {
        public override void Apply(
            Terrain terrain,
            ISplineHeightmapCache cache,
            int childPriority,
            int[,] writePriority,
            OperationContext context)
        {
            if (context.holes == null || !ValidateCache(cache)) return;

            ProcessCacheRegion(cache, childPriority, writePriority, terrain, context, (x, z, localX, localZ, cachedAlpha) =>
            {
                // Accumulate mask for fill operations
                if (context.holeSplineMask != null && cachedAlpha > 0.01f)
                {
                    context.holeSplineMask[z, x] = true;
                }

                float a = cachedAlpha * Mathf.Clamp01(context.strength);
                // Where alpha is high, fill hole (set to true)
                if (a > 0.01f)
                {
                    context.holes[z, x] = true;
                    writePriority[z, x] = childPriority;
                }
            });
        }
    }
}