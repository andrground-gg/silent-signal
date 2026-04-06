using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

namespace GruelTerraSplines
{
    /// <summary>
    /// Test script to verify spline caching functionality
    /// This script can be attached to a GameObject in the scene for testing
    /// </summary>
    public class GroupCacheTest : MonoBehaviour
    {
        [Header("Test Configuration")]
        public Terrain targetTerrain;

        public Transform splinesRoot;

        [Header("Debug Info")]
        public int splineCacheCount;

        public float splineCacheMemoryMB;

        [Header("Performance Test")]
        public bool runPerformanceTest = false;

        public int testIterations = 100;

        void Start()
        {
            if (targetTerrain == null)
                targetTerrain = FindObjectOfType<Terrain>();

            if (splinesRoot == null)
                splinesRoot = transform;
        }

        void Update()
        {
            // Update cache statistics
            UpdateCacheStatistics();

            if (runPerformanceTest)
            {
                runPerformanceTest = false;
                RunPerformanceTest();
            }
        }

        void UpdateCacheStatistics()
        {
            splineCacheCount = TerraSplinesTool.GetSplineCacheCount();
            splineCacheMemoryMB = TerraSplinesTool.GetSplineCacheMemoryMB();
        }

        void RunPerformanceTest()
        {
            if (targetTerrain == null)
            {
                Debug.LogError("GroupCacheTest: No target terrain assigned!");
                return;
            }

            Debug.Log("=== Spline Cache Performance Test ===");

            // Collect all splines
            var allSplines = new List<(SplineContainer container, SplineStrokeSettings settings, int priority)>();
            CollectSplines(splinesRoot, allSplines);

            if (allSplines.Count == 0)
            {
                Debug.LogWarning("GroupCacheTest: No splines found for testing!");
                return;
            }

            Debug.Log($"Found {allSplines.Count} splines for testing");

            // Clear caches to start fresh
            TerraSplinesTool.ClearAllCaches();

            // Test parameters
            var globals = new TerraSplinesTool.GlobalSettings
            {
                mode = SplineApplyMode.Path,
                brushSizeMeters = 10f,
                strength = 1f,
                sampleStepMeters = 1f,
                brushHardness = 0.5f
            };

            var terrainData = targetTerrain.terrainData;
            int res = terrainData.heightmapResolution;
            var baseHeights = new float[res, res];
            var outHeights = new float[res, res];

            // Initialize base heights
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    baseHeights[z, x] = terrainData.GetHeight(x, z) / terrainData.heightmapScale.y;
                }
            }

            // Measure time for multiple iterations
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < testIterations; i++)
            {
                TerraSplinesTool.RasterizeSplines(targetTerrain, allSplines, globals, baseHeights, outHeights);
            }

            stopwatch.Stop();

            // Calculate statistics
            float totalTimeMs = stopwatch.ElapsedMilliseconds;
            float avgTimeMs = totalTimeMs / testIterations;
            float fps = 1000f / avgTimeMs;

            Debug.Log($"Performance Test Results:");
            Debug.Log($"- Iterations: {testIterations}");
            Debug.Log($"- Total Time: {totalTimeMs:F2} ms");
            Debug.Log($"- Average Time: {avgTimeMs:F2} ms per iteration");
            Debug.Log($"- Estimated FPS: {fps:F1}");
            Debug.Log($"- Spline Caches: {splineCacheCount}");
            Debug.Log($"- Spline Memory: {splineCacheMemoryMB:F2} MB");
        }

        void CollectSplines(Transform root, List<(SplineContainer container, SplineStrokeSettings settings, int priority)> splines)
        {
            // Get SplineContainer components
            var containers = root.GetComponentsInChildren<SplineContainer>();

            foreach (var container in containers)
            {
                if (container == null) continue;

                // Get or create SplineStrokeSettings
                var settings = container.GetComponent<TerrainSplineSettings>();
                if (settings == null)
                {
                    settings = container.gameObject.AddComponent<TerrainSplineSettings>();
                }

                // Calculate priority based on hierarchy depth
                int priority = CalculatePriority(container.transform, root);

                splines.Add((container, settings.settings, priority));
            }
        }

        int CalculatePriority(Transform splineTransform, Transform rootTransform)
        {
            int depth = 0;
            Transform current = splineTransform;

            while (current != null && current != rootTransform)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }

        [ContextMenu("Clear All Caches")]
        public void ClearAllCaches()
        {
            TerraSplinesTool.ClearAllCaches();
            UpdateCacheStatistics();
            Debug.Log("All caches cleared!");
        }

        [ContextMenu("Run Performance Test")]
        public void RunPerformanceTestMenu()
        {
            runPerformanceTest = true;
        }

        void OnGUI()
        {
            if (Application.isPlaying)
            {
                GUILayout.BeginArea(new Rect(10, 10, 300, 200));
                GUILayout.Label("Spline Cache Test", GUI.skin.box);
                GUILayout.Label($"Spline Caches: {splineCacheCount}");
                GUILayout.Label($"Spline Memory: {splineCacheMemoryMB:F2} MB");

                if (GUILayout.Button("Clear All Caches"))
                {
                    ClearAllCaches();
                }

                if (GUILayout.Button("Run Performance Test"))
                {
                    RunPerformanceTestMenu();
                }

                GUILayout.EndArea();
            }
        }
    }
}