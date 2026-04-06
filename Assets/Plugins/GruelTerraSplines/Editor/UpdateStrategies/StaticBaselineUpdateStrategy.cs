using UnityEngine;

namespace GruelTerraSplines
{
    /// <summary>
    /// Strategy that keeps the baseline static for optimal performance.
    /// When using this strategy, external terrain changes made outside the tool will not be detected.
    /// Use this for maximum performance when you know terrain won't be modified externally.
    /// </summary>
    public class StaticBaselineUpdateStrategy : IBaselineUpdateStrategy
    {
        /// <summary>
        /// No-op implementation. The baseline remains unchanged for better performance.
        /// External changes to terrain will not be detected when using this strategy.
        /// </summary>
        public void UpdateBaseline(
            Terrain terrain,
            ref float[,] baselineHeights,
            float[,] workingHeights,
            ref bool[,] baselineHoles,
            bool[,] workingHoles,
            ref float[,,] baselineAlphamaps,
            float[,,] workingAlphamaps,
            float[,] paintSplineAlphaMask,
            bool[,] holeSplineMask,
            bool suppressUpdate
        )
        {
            // Intentionally does nothing - baseline remains static for performance
        }
    }
}