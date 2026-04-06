using UnityEngine;

namespace GruelTerraSplines
{
    public interface IOperationApplier
    {
        void Apply(
            Terrain terrain,
            ISplineHeightmapCache cache,
            int childPriority,
            int[,] writePriority,
            OperationContext context
        );
    }
}