using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Splines;

namespace GruelTerraSplines.Managers
{
    /// <summary>
    /// Manages editor update loop, timing, and validation.
    /// Coordinates updates across other managers and handles scene/hierarchy changes.
    /// </summary>
    public class EditorUpdateManager : IWindowManager
    {
        EditorWindow window;
        SplineDataManager splineDataManager;
        TerrainStateManager terrainStateManager;

        // Timing
        DateTime lastUpdate = DateTime.MinValue;
        public bool UpdatesPaused { get; set; }
        public float UpdateInterval { get; set; } = 0.1f;
        public bool ManualUpdateRequested { get; set; }

        // State tracking
        private bool lastHadActiveSplines = false;

        public EditorUpdateManager(SplineDataManager splineDataManager, TerrainStateManager terrainStateManager)
        {
            this.splineDataManager = splineDataManager;
            this.terrainStateManager = terrainStateManager;
        }

        public void Initialize(EditorWindow window)
        {
            this.window = window;
        }

        public void OnDestroy()
        {
            // Cleanup if needed
        }

        public void OnEditorUpdate(Action onPreviewRebuild)
        {
            // Don't run updates while in play mode
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            // Pause gating and one-shot manual update handling
            if (UpdatesPaused)
            {
                if (!ManualUpdateRequested)
                {
                    return;
                }

                // Manual single update pass regardless of interval
                ProcessManualUpdate(onPreviewRebuild);
                return;
            }

            // Check update interval
            if ((DateTime.Now - lastUpdate).TotalSeconds < Mathf.Max(0.02f, UpdateInterval)) return;
            lastUpdate = DateTime.Now;

            // Process automatic update
            ProcessAutomaticUpdate(onPreviewRebuild);
        }

        public void OnHierarchyChanged(Action refreshChildren)
        {
            refreshChildren?.Invoke();
        }

        public void OnSceneChanged()
        {
            // Reset state
            lastHadActiveSplines = false;

            // Clear spline items to force refresh
            if (splineDataManager != null)
            {
                splineDataManager.SplineItems.Clear();
            }
        }

        public bool HasActiveSplines()
        {
            var hasActive = splineDataManager?.HasActiveSplines() ?? false;

            // Only clean up caches when state changes from active to inactive
            if (lastHadActiveSplines && !hasActive)
            {
                TerraSplinesTool.ClearInactiveSplineCaches();
            }

            lastHadActiveSplines = hasActive;
            return hasActive;
        }

        private void ProcessManualUpdate(Action onPreviewRebuild)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Debug.Log($"[DK Terrain Splines] Manual update execution started...");

            if (terrainStateManager?.TargetTerrain == null)
            {
                ManualUpdateRequested = false;
                return;
            }

            bool hasActiveSplines = HasActiveSplines();
            if (!hasActiveSplines)
            {
                if (terrainStateManager?.BaselineHeights != null)
                {
                    TerraSplinesTool.ApplyPreviewToTerrain(
                        terrainStateManager.TargetTerrain,
                        terrainStateManager.BaselineHeights);
                }

                ManualUpdateRequested = false;
                stopwatch.Stop();
                Debug.Log($"[DK Terrain Splines] Manual update completed in {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F3}s) - No active splines");
                return;
            }

            terrainStateManager.EnsureBaseline();

            // Update baseline using strategy
            terrainStateManager.UpdateBaseline(suppressUpdate: false);

            onPreviewRebuild?.Invoke();

            ManualUpdateRequested = false;
            lastUpdate = DateTime.Now;

            stopwatch.Stop();
            Debug.Log($"[DK Terrain Splines] Manual update completed in {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F3}s)");
        }

        private void ProcessAutomaticUpdate(Action onPreviewRebuild)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            if (terrainStateManager?.TargetTerrain == null) return;

            bool hasActiveSplines = HasActiveSplines();
            if (!hasActiveSplines)
            {
                if (terrainStateManager?.BaselineHeights != null)
                {
                    TerraSplinesTool.ApplyPreviewToTerrain(
                        terrainStateManager.TargetTerrain,
                        terrainStateManager.BaselineHeights);
                }

                stopwatch.Stop();
                return;
            }

            terrainStateManager.EnsureBaseline();

            // Update baseline using strategy
            terrainStateManager.UpdateBaseline(suppressUpdate: false);

            // Rebuild preview
            onPreviewRebuild?.Invoke();

            stopwatch.Stop();
        }
    }
}