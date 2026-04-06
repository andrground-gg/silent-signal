using UnityEngine;

namespace GruelTerraSplines
{
    /// <summary>
    /// Strategy that dynamically updates the baseline by subtracting current spline contributions from the terrain.
    /// This allows the tool to detect and preserve manual terrain edits made outside the tool.
    /// </summary>
    public class DynamicBaselineUpdateStrategy : IBaselineUpdateStrategy
    {
        /// <summary>
        /// Updates the baseline by subtracting the current spline contribution from the current terrain state.
        /// This preserves manual edits while filtering out splines.
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
            if (terrain == null || suppressUpdate) return;
            if (baselineHeights == null || workingHeights == null) return;

            var td = terrain.terrainData;

            // Get current terrain state (may include manual edits)
            var currentHeights = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);

            // Check dimensions match
            if (baselineHeights.GetLength(0) != currentHeights.GetLength(0) ||
                baselineHeights.GetLength(1) != currentHeights.GetLength(1) ||
                workingHeights.GetLength(0) != currentHeights.GetLength(0) ||
                workingHeights.GetLength(1) != currentHeights.GetLength(1))
            {
                return; // Dimension mismatch, skip update
            }

            int resX = currentHeights.GetLength(0);
            int resZ = currentHeights.GetLength(1);

            // Create new baseline by subtracting spline contribution from current terrain
            // Formula: newBaseline = currentTerrain - (workingHeights - baselineHeights)
            // Simplified: newBaseline = currentTerrain - workingHeights + baselineHeights
            var newBaseline = new float[resX, resZ];

            for (int z = 0; z < resZ; z++)
            {
                for (int x = 0; x < resX; x++)
                {
                    // Calculate spline contribution at this point
                    float splineContribution = workingHeights[z, x] - baselineHeights[z, x];

                    // Subtract spline contribution from current terrain to get baseline + manual edits
                    newBaseline[z, x] = currentHeights[z, x] - splineContribution;
                }
            }

            // Update baseline with the result
            baselineHeights = newBaseline;

            // Sync alphamaps - subtract spline contribution everywhere (like heights)
            if (baselineAlphamaps != null && workingAlphamaps != null && td.alphamapLayers > 0)
            {
                var currentAlphamaps = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);

                // Check dimensions match
                if (baselineAlphamaps.GetLength(0) == currentAlphamaps.GetLength(0) &&
                    baselineAlphamaps.GetLength(1) == currentAlphamaps.GetLength(1))
                {
                    int alphaHeight = currentAlphamaps.GetLength(0);
                    int alphaWidth = currentAlphamaps.GetLength(1);
                    int layers = currentAlphamaps.GetLength(2);

                    for (int z = 0; z < alphaHeight; z++)
                    {
                        for (int x = 0; x < alphaWidth; x++)
                        {
                            // Subtract spline contribution from alphamaps (like we do for heights)
                            // Formula: newBaseline = currentTerrain - (working - baseline)
                            for (int l = 0; l < layers; l++)
                            {
                                float splineContribution = workingAlphamaps[z, x, l] - baselineAlphamaps[z, x, l];
                                baselineAlphamaps[z, x, l] = currentAlphamaps[z, x, l] - splineContribution;
                            }
                        }
                    }
                }
            }

            // Sync holes - subtract spline contribution everywhere (like heights and paint)
            if (baselineHoles != null && workingHoles != null)
            {
                var currentTerrainHoles = TerraSplinesTool.GetTerrainHoles(terrain);
                var currentHoles = TerraSplinesTool.ConvertTerrainHolesToHeightmapHoles(terrain, currentTerrainHoles);

                // Check dimensions match
                if (baselineHoles.GetLength(0) == currentHoles.GetLength(0) &&
                    baselineHoles.GetLength(1) == currentHoles.GetLength(1) &&
                    workingHoles.GetLength(0) == currentHoles.GetLength(0) &&
                    workingHoles.GetLength(1) == currentHoles.GetLength(1))
                {
                    int holeResX = currentHoles.GetLength(0);
                    int holeResZ = currentHoles.GetLength(1);

                    // Create new baseline array to avoid overwriting values we need for comparison
                    var newBaselineHoles = new bool[holeResX, holeResZ];
                    
                    for (int z = 0; z < holeResZ; z++)
                    {
                        for (int x = 0; x < holeResX; x++)
                        {
                            // Calculate spline hole contribution: detect when working and baseline differ
                            // For holes: false = hole exists, true = solid ground
                            // If working != baseline, spline modified this pixel
                            bool splineContribution = (workingHoles[z, x] != baselineHoles[z, x]);

                            // Subtract spline contribution from current terrain to restore baseline
                            // If spline contributed: reverse the change by using original baseline value
                            // If no contribution: preserve current terrain value (external changes)
                            if (splineContribution)
                            {
                                // Reverse spline change: restore to original baseline value
                                newBaselineHoles[z, x] = baselineHoles[z, x];
                            }
                            else
                            {
                                // No spline change: preserve current terrain state (external edits)
                                newBaselineHoles[z, x] = currentHoles[z, x];
                            }
                        }
                    }
                    
                    // Update baseline with the result
                    baselineHoles = newBaselineHoles;
                }
            }
        }
    }
}