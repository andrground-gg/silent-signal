using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace GruelTerraSplines.Managers
{
    /// <summary>
    /// Manages preview texture generation, caching, and display.
    /// Handles preview updates, texture generation, and height range calculations.
    /// </summary>
    public class PreviewManager : IWindowManager
    {
        EditorWindow window;
        TerrainStateManager terrainStateManager;
        SplineDataManager splineDataManager;

        // Height range caching
        public (float min, float max) CachedBaselineRange { get; set; }
        public (float min, float max) CachedPreviewRange { get; set; }
        public bool HeightRangeNeedsUpdate { get; set; }

        // Preview texture state
        public bool PreviewTexturesNeedUpdate { get; set; }
        public bool IsPreviewAppliedToTerrain { get; set; }

        public PreviewManager(TerrainStateManager terrainStateManager, SplineDataManager splineDataManager)
        {
            this.terrainStateManager = terrainStateManager;
            this.splineDataManager = splineDataManager;
        }

        public void Initialize(EditorWindow window)
        {
            this.window = window;
        }

        public void OnDestroy()
        {
            // Cleanup preview textures from spline items
            if (splineDataManager != null)
            {
                var allSplines = splineDataManager.GetAllSplineItems();
                foreach (var item in allSplines)
                {
                    if (item.previewTexture != null)
                    {
                        UnityEngine.Object.DestroyImmediate(item.previewTexture);
                        item.previewTexture = null;
                    }
                }
            }

            PreviewTexturesNeedUpdate = false;
            IsPreviewAppliedToTerrain = false;
        }

        public (float min, float max) GetHeightmapRange(float[,] heights, Terrain terrain)
        {
            if (heights == null || terrain == null) return (0, 0);

            int res = heights.GetLength(0);
            float min = float.MaxValue;
            float max = float.MinValue;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float h = heights[z, x];
                    if (h < min) min = h;
                    if (h > max) max = h;
                }
            }

            // Convert normalized heights to meters
            float terrainHeight = terrain.terrainData.size.y;
            return (min * terrainHeight, max * terrainHeight);
        }

        /// <summary>
        /// Get combined height range from all terrains
        /// </summary>
        public (float min, float max) GetCombinedHeightmapRange(TerrainStateManager terrainStateManager)
        {
            if (terrainStateManager?.TerrainStates == null) return (0, 0);

            float combinedMin = float.MaxValue;
            float combinedMax = float.MinValue;

            foreach (var kvp in terrainStateManager.TerrainStates)
            {
                var terrain = kvp.Key;
                var state = kvp.Value;
                if (terrain == null || state?.BaselineHeights == null) continue;

                var range = GetHeightmapRange(state.BaselineHeights, terrain);
                if (range.min < combinedMin) combinedMin = range.min;
                if (range.max > combinedMax) combinedMax = range.max;
            }

            if (combinedMin == float.MaxValue) return (0, 0);
            return (combinedMin, combinedMax);
        }

        /// <summary>
        /// Get combined working height range from all terrains
        /// </summary>
        public (float min, float max) GetCombinedWorkingHeightmapRange(TerrainStateManager terrainStateManager)
        {
            if (terrainStateManager?.TerrainStates == null) return (0, 0);

            float combinedMin = float.MaxValue;
            float combinedMax = float.MinValue;

            foreach (var kvp in terrainStateManager.TerrainStates)
            {
                var terrain = kvp.Key;
                var state = kvp.Value;
                if (terrain == null || state?.WorkingHeights == null) continue;

                var range = GetHeightmapRange(state.WorkingHeights, terrain);
                if (range.min < combinedMin) combinedMin = range.min;
                if (range.max > combinedMax) combinedMax = range.max;
            }

            if (combinedMin == float.MaxValue) return (0, 0);
            return (combinedMin, combinedMax);
        }

        public void UpdatePreviewTextures()
        {
            if (terrainStateManager?.TargetTerrain == null) return;

            try
            {
                var allSplines = splineDataManager?.GetAllSplineItems();
                if (allSplines == null) return;

                foreach (var item in allSplines)
                {
                    if (item.container == null) continue;

                    // Determine if this spline is in Shape or Path mode
                    var mode = item.settings.overrideMode ? item.settings.mode : SplineApplyMode.Path;

                    if (mode == SplineApplyMode.Shape)
                    {
                        // Generate preview texture for Shape mode with height coloring
                        var old = item.previewTexture;
                        var updated = TerraSplinesTool.GetShapeHeightMaskTexture(item.container, 128, old);
                        if (updated == null && old != null) UnityEngine.Object.DestroyImmediate(old);
                        item.previewTexture = updated;
                    }
                    else if (mode == SplineApplyMode.Path)
                    {
                        // Generate preview texture for Path mode with height coloring
                        var old = item.previewTexture;
                        var updated = TerraSplinesTool.GetPathHeightMaskTexture(item.container, 128, old);
                        if (updated == null && old != null) UnityEngine.Object.DestroyImmediate(old);
                        item.previewTexture = updated;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to update preview textures: {e.Message}");
            }
        }

        public void UpdateHeightRangeDisplay(Label heightRangeLabel)
        {
            if (heightRangeLabel != null && terrainStateManager?.TargetTerrain != null && HeightRangeNeedsUpdate)
            {
                CachedBaselineRange = GetHeightmapRange(terrainStateManager.BaselineHeights, terrainStateManager.TargetTerrain);
                CachedPreviewRange = GetHeightmapRange(terrainStateManager.WorkingHeights, terrainStateManager.TargetTerrain);
                heightRangeLabel.text = $"Baseline: {CachedBaselineRange.min:F1}-{CachedBaselineRange.max:F1}m | Preview: {CachedPreviewRange.min:F1}-{CachedPreviewRange.max:F1}m";
                HeightRangeNeedsUpdate = false;
            }
            else if (heightRangeLabel != null && terrainStateManager?.TargetTerrain != null)
            {
                // Show cached values without recalculating
                heightRangeLabel.text = $"Baseline: {CachedBaselineRange.min:F1}-{CachedBaselineRange.max:F1}m | Preview: {CachedPreviewRange.min:F1}-{CachedPreviewRange.max:F1}m";
            }
        }
    }
}
