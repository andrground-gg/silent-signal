using UnityEngine;

namespace GruelTerraSplines
{
    /// <summary>
    /// Interface for baseline update strategies that determine how the terrain baseline is maintained during spline updates.
    /// </summary>
    public interface IBaselineUpdateStrategy
    {
        /// <summary>
        /// Updates the baseline terrain data based on the current strategy.
        /// </summary>
        /// <param name="terrain">The target terrain being modified</param>
        /// <param name="baselineHeights">The baseline height data (will be updated)</param>
        /// <param name="workingHeights">The working height data representing baseline + splines</param>
        /// <param name="baselineHoles">The baseline hole data (will be updated)</param>
        /// <param name="workingHoles">The working hole data representing baseline + spline holes</param>
        /// <param name="baselineAlphamaps">The baseline alphamap data (will be updated)</param>
        /// <param name="workingAlphamaps">The working alphamap data representing baseline + spline paint</param>
        /// <param name="paintSplineAlphaMask">Mask indicating which pixels are affected by spline paint operations</param>
        /// <param name="holeSplineMask">Mask indicating which pixels are affected by spline hole/fill operations</param>
        /// <param name="suppressUpdate">Whether to suppress the baseline update (used during special operations)</param>
        void UpdateBaseline(
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
        );
    }
}