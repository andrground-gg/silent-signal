using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
#endif
using UnityEngine.Splines;

namespace GruelTerraSplines
{
    public static class TerraSplinesTool
    {
        #if UNITY_EDITOR
        static readonly Func<UnityEngine.Object, int> getDirtyCountFunc = CreateGetDirtyCountFunc();
        static Func<UnityEngine.Object, int> CreateGetDirtyCountFunc()
        {
            try
            {
                var method = typeof(EditorUtility).GetMethod(
                    "GetDirtyCount",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(UnityEngine.Object) },
                    null);

                if (method == null) return null;
                return (Func<UnityEngine.Object, int>)Delegate.CreateDelegate(typeof(Func<UnityEngine.Object, int>), method);
            }
            catch
            {
                return null;
            }
        }
        #endif

        #if UNITY_EDITOR
        static readonly Func<Type> getActiveToolTypeFunc = CreateGetToolManagerTypeGetter("activeToolType");
        static readonly Func<Type> getActiveContextTypeFunc = CreateGetToolManagerTypeGetter("activeContextType");
        static readonly Func<Type> getActiveToolContextTypeFunc = CreateGetToolManagerTypeGetter("activeToolContextType");

        static Func<Type> CreateGetToolManagerTypeGetter(string propertyName)
        {
            try
            {
                var toolManagerType = Type.GetType("UnityEditor.EditorTools.ToolManager, UnityEditor");
                if (toolManagerType == null) return null;

                var property = toolManagerType.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (property == null || property.PropertyType != typeof(Type)) return null;

                return () => (Type)property.GetValue(null);
            }
            catch
            {
                return null;
            }
        }

        static bool IsSplineEditModeLikelyActive()
        {
            try
            {
                var toolType = getActiveToolTypeFunc?.Invoke();
                if (toolType != null && toolType.FullName != null &&
                    toolType.FullName.IndexOf("Spline", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                var ctxType = getActiveContextTypeFunc?.Invoke() ?? getActiveToolContextTypeFunc?.Invoke();
                if (ctxType != null && ctxType.FullName != null &&
                    ctxType.FullName.IndexOf("Spline", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            catch
            {
                // Ignore and fall back to false
            }

            return false;
        }

        static bool IsContainerSelected(SplineContainer container)
        {
            if (container == null) return false;
            if (container.gameObject != null && UnityEditor.Selection.Contains(container.gameObject)) return true;
            return UnityEditor.Selection.Contains(container);
        }

        public static bool IsSplineEditModeActive()
        {
            return IsSplineEditModeLikelyActive();
        }
        #endif

        static readonly Dictionary<int, Color32[]> previewColorBuffers = new Dictionary<int, Color32[]>();
        static Color32[] GetPreviewColorBuffer(int previewSize)
        {
            int len = Mathf.Max(1, previewSize) * Mathf.Max(1, previewSize);
            if (!previewColorBuffers.TryGetValue(previewSize, out var buffer) || buffer == null || buffer.Length != len)
            {
                buffer = new Color32[len];
                previewColorBuffers[previewSize] = buffer;
            }
            return buffer;
        }

        // Compute shader support detection
        static bool computeShaderSupported = false;
        static bool computeShaderSupportChecked = false;
        static BackendType currentBackendType = BackendType.Unknown;

        /// <summary>
        /// Get or set the current backend type (CPU or GPU)
        /// </summary>
        public static BackendType CurrentBackendType
        {
            get => currentBackendType;
            set
            {
                if (currentBackendType != value)
                {
                    currentBackendType = value;
                    // Recreate strategies when backend type changes
                    RecreateStrategies();
                }
            }
        }

        // Strategy pattern for different spline modes
        // Easy swap: Change to PathSplineStrategy() and ShapeSplineStrategy() for CPU implementation
        // Change to PathSplineStrategyGPU() and ShapeSplineStrategyGPU() for GPU implementation
        public static Dictionary<SplineApplyMode, ISplineStrategy> strategies = new Dictionary<SplineApplyMode, ISplineStrategy>
        {
            { SplineApplyMode.Path, new PathSplineStrategyGPU() },
            { SplineApplyMode.Shape, new ShapeSplineStrategyGPU() }
        };

        // Operation applier factory for applying cached rasterization data to terrain
        enum OperationKind
        {
            Height,
            Paint,
            Hole,
            Fill,
            DetailAdd,
            DetailRemove
        }

        private static readonly Dictionary<OperationKind, IOperationApplier> operationAppliers = new Dictionary<OperationKind, IOperationApplier>
        {
            { OperationKind.Height, new HeightOperationApplier() },
            { OperationKind.Paint, new PaintOperationApplier() },
            { OperationKind.Hole, new HoleOperationApplier() },
            { OperationKind.Fill, new FillOperationApplier() },
            { OperationKind.DetailAdd, new DetailOperationApplier() },
            { OperationKind.DetailRemove, new DetailOperationApplier() },
        };

        // Initialize backend type
        static TerraSplinesTool()
        {
            currentBackendType = BackendType.GPU; // GPU for both Path and Shape
        }

        // Force strategy recreation for testing
        public static void ForceRecreateStrategies()
        {
            // Clear all caches first
            foreach (var strategy in strategies.Values)
            {
                strategy.ClearCaches();
            }

            // Clear and recreate strategies
            strategies.Clear();
            strategies[SplineApplyMode.Path] = new PathSplineStrategyGPU();
            strategies[SplineApplyMode.Shape] = new ShapeSplineStrategyGPU();
        }

        // Keep GlobalSettings struct here for backward compatibility with DKTerrainSplinesWindow
        public struct GlobalSettings
        {
            public SplineApplyMode mode;
            public float brushSizeMeters;
            public float strength;
            public float sampleStepMeters;

            // New brush falloff parameters
            public float brushHardness;

            // Brush noise settings
            public BrushNoiseSettings brushNoise;

            // Terrain operation toggles
            public bool operationHeight;
            public bool operationPaint;
            public bool operationHole;
            public bool operationFill;
            public bool operationAddDetail;
            public bool operationRemoveDetail;

            // Paint settings
            public int globalSelectedLayerIndex;
            public float globalPaintStrength;
            public List<PaintNoiseLayerSettings> globalPaintNoiseLayers;

            // Detail settings
            public int globalSelectedDetailLayerIndex;
            public int[] globalSelectedDetailLayerIndices;
            public float globalDetailStrength;
            public DetailOperationMode globalDetailMode;
            public int globalDetailTargetDensity;
            public float globalDetailSlopeLimitDegrees;
            public float globalDetailFalloffPower;
            public int globalDetailSpreadRadius;
            public float globalDetailRemoveThreshold;
            public List<DetailNoiseLayerSettings> globalDetailNoiseLayers;

        }

        struct OperationFlags
        {
            public bool height;
            public bool paint;
            public bool hole;
            public bool fill;
            public bool addDetail;
            public bool removeDetail;

            public bool Any => height || paint || hole || fill || addDetail || removeDetail;
        }

        static int[] BuildSelectedDetailLayerIndices(SplineStrokeSettings splineSettings, GlobalSettings globals, bool useOverrideDetail, int layerCount)
        {
            if (layerCount <= 0) return System.Array.Empty<int>();

            System.Collections.Generic.IEnumerable<int> raw;
            if (useOverrideDetail)
            {
                if (splineSettings != null && splineSettings.selectedDetailLayerIndices != null && splineSettings.selectedDetailLayerIndices.Count > 0)
                    raw = splineSettings.selectedDetailLayerIndices;
                else
                    raw = new[] { splineSettings != null ? splineSettings.selectedDetailLayerIndex : 0 };
            }
            else
            {
                if (globals.globalSelectedDetailLayerIndices != null && globals.globalSelectedDetailLayerIndices.Length > 0)
                    raw = globals.globalSelectedDetailLayerIndices;
                else
                    raw = new[] { globals.globalSelectedDetailLayerIndex };
            }

            var indices = new System.Collections.Generic.List<int>();
            foreach (int idx in raw)
            {
                int clamped = Mathf.Clamp(idx, 0, layerCount - 1);
                if (!indices.Contains(clamped))
                    indices.Add(clamped);
            }

            if (indices.Count == 0)
                indices.Add(0);

            indices.Sort();
            return indices.ToArray();
        }

        static DetailNoiseLayerSettings[] BuildSelectedDetailNoiseLayers(
            SplineStrokeSettings splineSettings,
            GlobalSettings globals,
            bool useOverrideDetail,
            int[] selectedDetailLayerIndices)
        {
            if (selectedDetailLayerIndices == null || selectedDetailLayerIndices.Length == 0)
                return System.Array.Empty<DetailNoiseLayerSettings>();

            var source = useOverrideDetail ? splineSettings?.detailNoiseLayers : globals.globalDetailNoiseLayers;
            if (source == null || source.Count == 0)
                return System.Array.Empty<DetailNoiseLayerSettings>();

            var selectedSet = new System.Collections.Generic.HashSet<int>(selectedDetailLayerIndices);
            var results = new System.Collections.Generic.List<DetailNoiseLayerSettings>();
            for (int i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (entry == null) continue;
                if (selectedSet.Contains(entry.detailLayerIndex))
                    results.Add(entry);
            }

            return results.Count > 0 ? results.ToArray() : System.Array.Empty<DetailNoiseLayerSettings>();
        }

        static PaintNoiseLayerSettings[] BuildSelectedPaintNoiseLayers(
            SplineStrokeSettings splineSettings,
            GlobalSettings globals,
            bool useOverridePaint,
            int selectedPaintLayerIndex)
        {
            var source = useOverridePaint ? splineSettings?.paintNoiseLayers : globals.globalPaintNoiseLayers;
            if (source == null || source.Count == 0)
                return System.Array.Empty<PaintNoiseLayerSettings>();

            for (int i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (entry == null) continue;
                if (entry.paintLayerIndex == selectedPaintLayerIndex)
                    return new[] { entry };
            }

            return System.Array.Empty<PaintNoiseLayerSettings>();
        }

        static BrushNoiseSettings ResolveBrushNoiseSettings(
            SplineStrokeSettings splineSettings,
            GlobalSettings globals,
            bool useOverrideBrush)
        {
            var source = useOverrideBrush ? splineSettings?.brushNoise : globals.brushNoise;
            if (source == null)
                return null;

            return new BrushNoiseSettings
            {
                noiseTexture = source.noiseTexture,
                noiseStrength = source.noiseStrength,
                noiseEdge = source.noiseEdge,
                noiseWorldSizeMeters = source.noiseWorldSizeMeters,
                noiseOffset = source.noiseOffset,
                noiseInvert = source.noiseInvert
            };
        }

        static OperationFlags ResolveOperations(SplineStrokeSettings settings, GlobalSettings globals)
        {
            bool useOverride = settings.overrideMode;
            var ops = new OperationFlags
            {
                height = useOverride ? settings.operationHeight : globals.operationHeight,
                paint = useOverride ? settings.operationPaint : globals.operationPaint,
                hole = useOverride ? settings.operationHole : globals.operationHole,
                fill = useOverride ? settings.operationFill : globals.operationFill,
                addDetail = useOverride ? settings.operationAddDetail : globals.operationAddDetail,
                removeDetail = useOverride ? settings.operationRemoveDetail : globals.operationRemoveDetail
            };

            // Enforce mutual exclusivity: hole vs fill, add detail vs remove detail.
            ops.fill = ops.fill && !ops.hole;
            ops.removeDetail = ops.removeDetail && !ops.addDetail;

            return ops;
        }

        static List<OperationKind> BuildOperationList(OperationFlags ops)
        {
            var list = new List<OperationKind>(6);
            if (ops.height) list.Add(OperationKind.Height);
            if (ops.paint) list.Add(OperationKind.Paint);
            if (ops.hole) list.Add(OperationKind.Hole);
            else if (ops.fill) list.Add(OperationKind.Fill);

            if (ops.addDetail) list.Add(OperationKind.DetailAdd);
            else if (ops.removeDetail) list.Add(OperationKind.DetailRemove);

            return list;
        }

        /// <summary>
        /// Check if compute shaders are supported on this platform
        /// </summary>
        public static bool IsComputeShaderSupported()
        {
            if (!computeShaderSupportChecked)
            {
                computeShaderSupported = SystemInfo.supportsComputeShaders;
                computeShaderSupportChecked = true;

                if (!computeShaderSupported)
                {
                    Debug.LogWarning("Compute shaders not supported on this platform. Terrain spline tool will use CPU fallback.");
                }
            }

            return computeShaderSupported;
        }

        /// <summary>
        /// Get the current backend being used (CPU or GPU)
        /// </summary>
        public static string GetCurrentBackend()
        {
            // Check if we're using GPU strategies
            bool usingGPU = strategies[SplineApplyMode.Path] is PathSplineStrategyGPU;

            if (usingGPU && IsComputeShaderSupported())
            {
                // Check if GPU strategies are actually using GPU or falling back to CPU
                bool pathActuallyGPU = strategies[SplineApplyMode.Path] is PathSplineStrategyGPU pathGPU && pathGPU.IsActuallyUsingGPU();
                bool shapeActuallyGPU = strategies[SplineApplyMode.Shape] is ShapeSplineStrategyGPU shapeGPU && shapeGPU.IsActuallyUsingGPU();

                if (pathActuallyGPU && shapeActuallyGPU)
                {
                    return "GPU (Compute Shader)";
                }
                else
                {
                    return "GPU Strategy (CPU Fallback)";
                }
            }
            else
            {
                return "CPU (Fallback)";
            }
        }

        /// <summary>
        /// Force fallback to CPU implementation (useful for debugging or unsupported platforms)
        /// </summary>
        public static void ForceCPUFallback()
        {
            CurrentBackendType = BackendType.CPU;
            Debug.Log("Forced CPU fallback for terrain spline strategies.");

#if UNITY_EDITOR
            // Force repaint of all terrain spline windows
            var windows = Resources.FindObjectsOfTypeAll<TerraSplinesWindow>();
            foreach (var window in windows)
            {
                window.Repaint();
            }
#endif
        }

        /// <summary>
        /// Switch back to GPU implementation if supported
        /// </summary>
        public static void SwitchToGPU()
        {
            if (IsComputeShaderSupported())
            {
                CurrentBackendType = BackendType.GPU;
                Debug.Log("Switched to GPU implementation for terrain spline strategies.");

#if UNITY_EDITOR
                // Force repaint of all terrain spline windows
                var windows = Resources.FindObjectsOfTypeAll<TerraSplinesWindow>();
                foreach (var window in windows)
                {
                    window.Repaint();
                }
#endif
            }
            else
            {
                Debug.LogWarning("Cannot switch to GPU: compute shaders not supported on this platform.");
            }
        }

        public static bool IsHeightmapCacheDirty(SplineContainer container, ISplineHeightmapCache cache, float brushSize, float sampleStep, SplineApplyMode mode, AnimationCurve brushSizeMultiplier, float brushHardness)
        {
            if (cache == null || !cache.isValid) return true;

            // Check mode changes
            if (mode != cache.lastMode)
            {
                return true;
            }

            // Check transform changes
            var transform = container.transform;
            if (transform.position != cache.lastPosition ||
                transform.rotation != cache.lastRotation ||
                transform.localScale != cache.lastScale)
            {
                return true;
            }

            // Check parameter changes
            if (Mathf.Abs(brushSize - cache.lastBrushSize) > 0.001f ||
                Mathf.Abs(sampleStep - cache.lastSampleStep) > 0.001f)
            {
                return true;
            }

            // Check brush falloff parameter changes
            if (Mathf.Abs(brushHardness - cache.lastBrushHardness) > 0.001f)
            {
                return true;
            }

            // Check brush size curve changes
            int currentCurveHash = brushSizeMultiplier.GetAnimationCurveHash();
            if (currentCurveHash != cache.lastBrushSizeCurveHash)
            {
                return true;
            }

            // Check spline modifications
            int currentVersion = GetSplineVersion(container);
            if (currentVersion != cache.splineVersion)
            {
                return true;
            }

            return false;
        }

        public static int GetSplineVersion(SplineContainer container)
        {
            if (container == null) return 0;

            #if UNITY_EDITOR
            if (IsSplineEditModeLikelyActive() && IsContainerSelected(container))
            {
                unchecked
                {
                    int version = ComputeSplineGeometryHash(container);
                    if (container.transform != null)
                    {
                        version = (version * 486187739) ^ container.transform.localToWorldMatrix.GetHashCode();
                    }
                    return version;
                }
            }

            if (getDirtyCountFunc != null)
            {
                unchecked
                {
                    int version = getDirtyCountFunc(container);
                    if (container.transform != null)
                    {
                        version = (version * 486187739) ^ container.transform.localToWorldMatrix.GetHashCode();
                    }
                    return version;
                }
            }
            #endif

            unchecked
            {
                int version = ComputeSplineGeometryHash(container);

                // Include transform in version calculation to detect position/rotation/scale changes
                if (container.transform != null)
                {
                    version = (version * 486187739) ^ container.transform.localToWorldMatrix.GetHashCode();
                }

                return version;
            }
        }

        static int ComputeSplineGeometryHash(SplineContainer container)
        {
            if (container == null) return 0;

            unchecked
            {
                int version = 0;

                foreach (var spline in container.Splines)
                {
                    version += spline.Count; // knot count
                    for (int i = 0; i < spline.Count; i++)
                    {
                        var knot = spline[i];
                        version += knot.Position.GetHashCode();
                        version += knot.TangentIn.GetHashCode();
                        version += knot.TangentOut.GetHashCode();
                    }
                    version += spline.Closed.GetHashCode();
                }

                return version;
            }
        }

        public static void ClearShapeHeightmapCache()
        {
            foreach (var strategy in strategies.Values)
            {
                strategy.ClearCaches();
            }
        }

        public static void ClearPathHeightmapCache()
        {
            foreach (var strategy in strategies.Values)
            {
                strategy.ClearCaches();
            }
        }

        public static void ClearAllCaches()
        {
            foreach (var strategy in strategies.Values)
            {
                strategy.ClearCaches();
            }
        }

        /// <summary>
        /// Copy alphamap data for non-destructive editing
        /// </summary>
        public static float[,,] CopyAlphamaps(float[,,] source)
        {
            if (source == null) return null;

            int height = source.GetLength(0);
            int width = source.GetLength(1);
            int layers = source.GetLength(2);

            float[,,] copy = new float[height, width, layers];
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int l = 0; l < layers; l++)
                    {
                        copy[z, x, l] = source[z, x, l];
                    }
                }
            }

            return copy;
        }

        public static int[,] CopyDetailLayer(int[,] source)
        {
            if (source == null) return null;
            int height = source.GetLength(0);
            int width = source.GetLength(1);
            var copy = new int[height, width];
            Buffer.BlockCopy(source, 0, copy, 0, sizeof(int) * width * height);
            return copy;
        }

        public static int[][,] CopyDetailLayers(int[][,] source)
        {
            if (source == null) return null;

            var copy = new int[source.Length][,];
            for (int i = 0; i < source.Length; i++)
            {
                copy[i] = CopyDetailLayer(source[i]);
            }

            return copy;
        }

        public static void CopyAlphamapsTo(float[,,] src, float[,,] dst)
        {
            if (src == null || dst == null) return;
            int height = src.GetLength(0);
            int width = src.GetLength(1);
            int layers = src.GetLength(2);
            if (dst.GetLength(0) != height || dst.GetLength(1) != width || dst.GetLength(2) != layers) return;

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int l = 0; l < layers; l++)
                    {
                        dst[z, x, l] = src[z, x, l];
                    }
                }
            }
        }

        /// <summary>
        /// Apply alphamap data to terrain
        /// </summary>
        public static void ApplyAlphamapsToTerrain(Terrain terrain, float[,,] alphamaps)
        {
            if (terrain == null || alphamaps == null) return;

            var td = terrain.terrainData;
            
            // Validate dimensions match terrain expectations
            int expectedHeight = td.alphamapHeight;
            int expectedWidth = td.alphamapWidth;
            int expectedLayers = td.alphamapLayers;
            
            int actualHeight = alphamaps.GetLength(0);
            int actualWidth = alphamaps.GetLength(1);
            int actualLayers = alphamaps.GetLength(2);
            
            if (actualHeight != expectedHeight || actualWidth != expectedWidth || actualLayers != expectedLayers)
            {
                Debug.LogWarning($"Cannot apply alphamaps to terrain '{terrain.name}': Dimension mismatch. " +
                    $"Expected: {expectedHeight}x{expectedWidth}x{expectedLayers}, " +
                    $"Got: {actualHeight}x{actualWidth}x{actualLayers}");
                return;
            }

            terrain.terrainData.SetAlphamaps(0, 0, alphamaps);
        }

        public static void ApplyDetailLayersToTerrain(Terrain terrain, int[][,] detailLayers)
        {
            if (terrain == null || detailLayers == null) return;

            var td = terrain.terrainData;
            if (td == null) return;

            var prototypes = td.detailPrototypes;
            if (prototypes == null || prototypes.Length == 0) return;

            int expectedLayerCount = prototypes.Length;
            int expectedWidth = td.detailWidth;
            int expectedHeight = td.detailHeight;
            if (expectedWidth <= 0 || expectedHeight <= 0) return;

            int layerCount = Mathf.Min(expectedLayerCount, detailLayers.Length);
            for (int layer = 0; layer < layerCount; layer++)
            {
                var layerData = detailLayers[layer];
                if (layerData == null) continue;

                if (layerData.GetLength(0) != expectedHeight || layerData.GetLength(1) != expectedWidth)
                {
                    Debug.LogWarning($"Cannot apply detail layer {layer} to terrain '{terrain.name}': Dimension mismatch. " +
                        $"Expected: {expectedHeight}x{expectedWidth}, Got: {layerData.GetLength(0)}x{layerData.GetLength(1)}");
                    continue;
                }

                td.SetDetailLayer(0, 0, layer, layerData);
            }

            terrain.Flush();
        }

        /// <summary>
        /// Normalize alphamap weights at a specific pixel to ensure they sum to 1.0
        /// </summary>
        public static void NormalizeAlphamaps(float[,,] alphamaps, int x, int z)
        {
            if (alphamaps == null) return;

            int layers = alphamaps.GetLength(2);
            float sum = 0f;

            // Calculate sum of all layer weights at this pixel
            for (int l = 0; l < layers; l++)
            {
                sum += alphamaps[z, x, l];
            }


            // Normalize if sum is greater than 0
            if (sum > 0f)
            {
                for (int l = 0; l < layers; l++)
                {
                    alphamaps[z, x, l] /= sum;
                }
            }
        }

        /// <summary>
        /// Recreate all strategies to ensure fresh state after scene changes
        /// </summary>
        public static void RecreateStrategies()
        {
            // Clear existing strategies
            foreach (var strategy in strategies.Values)
            {
                strategy.ClearCaches();
            }

            // Recreate strategies based on current backend type
            if (currentBackendType == BackendType.GPU)
            {
                strategies[SplineApplyMode.Path] = new PathSplineStrategyGPU();
                strategies[SplineApplyMode.Shape] = new ShapeSplineStrategyGPU();
            }
            else
            {
                strategies[SplineApplyMode.Path] = new PathSplineStrategy();
                strategies[SplineApplyMode.Shape] = new ShapeSplineStrategy();
            }

            Debug.Log($"Terrain spline strategies recreated for fresh state. Path strategy: {strategies[SplineApplyMode.Path].GetType().Name}, Shape strategy: {strategies[SplineApplyMode.Shape].GetType().Name}");
        }

        public static void ClearInactiveSplineCaches()
        {
            // This method is kept for backward compatibility
            // The actual cache cleanup is now handled by individual strategies
            foreach (var strategy in strategies.Values)
            {
                strategy.ClearCaches();
            }
        }

        public static int GetShapeHeightmapCacheCount()
        {
            int totalCount = 0;
            foreach (var strategy in strategies.Values)
            {
                totalCount += strategy.GetCacheCount();
            }

            return totalCount;
        }

        public static float GetShapeHeightmapCacheMemoryMB()
        {
            float totalMB = 0f;
            foreach (var strategy in strategies.Values)
            {
                totalMB += strategy.GetCacheMemoryMB();
            }

            return totalMB;
        }

        public static int GetSplineCacheCount()
        {
            int totalCount = 0;
            foreach (var strategy in strategies.Values)
            {
                totalCount += strategy.GetCacheCount();
            }

            return totalCount;
        }

        public static float GetSplineCacheMemoryMB()
        {
            float totalMB = 0f;
            foreach (var strategy in strategies.Values)
            {
                totalMB += strategy.GetCacheMemoryMB();
            }

            return totalMB;
        }


        public static Texture2D GetShapeHeightMaskTexture(SplineContainer container, int previewSize = 128)
        {
            return strategies[SplineApplyMode.Shape].GetHeightMaskTexture(container, previewSize);
        }

        public static Texture2D GetPathHeightMaskTexture(SplineContainer container, int previewSize = 128)
        {
            return strategies[SplineApplyMode.Path].GetHeightMaskTexture(container, previewSize);
        }

        public static Texture2D GetShapeHeightMaskTexture(SplineContainer container, int previewSize, Texture2D reuseTexture)
        {
            return GetHeightMaskTextureInternal(SplineApplyMode.Shape, container, previewSize, reuseTexture);
        }

        public static Texture2D GetPathHeightMaskTexture(SplineContainer container, int previewSize, Texture2D reuseTexture)
        {
            return GetHeightMaskTextureInternal(SplineApplyMode.Path, container, previewSize, reuseTexture);
        }

        static Texture2D GetHeightMaskTextureInternal(SplineApplyMode mode, SplineContainer container, int previewSize, Texture2D reuseTexture)
        {
            if (container == null) return null;
            if (!strategies.TryGetValue(mode, out var strategy) || strategy == null) return null;

            foreach (var cache in strategy.GetCachesForContainer(container))
            {
                if (cache == null || !cache.isValid || cache.cachedHeights == null || cache.cachedAlpha == null) continue;

                var baseTexture = reuseTexture != null ? reuseTexture : cache.cachedPreviewTexture;
                var updatedTexture = GeneratePreviewTexture(cache, previewSize, baseTexture);
                if (updatedTexture == null) return null;

                if (cache.cachedPreviewTexture != null &&
                    cache.cachedPreviewTexture != updatedTexture &&
                    cache.cachedPreviewTexture != reuseTexture)
                {
                    UnityEngine.Object.DestroyImmediate(cache.cachedPreviewTexture);
                }

                cache.cachedPreviewTexture = updatedTexture;
                cache.cachedPreviewSize = previewSize;
                return updatedTexture;
            }

            return null;
        }

        /// <summary>
        /// Generate preview texture from cache arrays. Caches the result in the cache object.
        /// </summary>
        public static Texture2D GeneratePreviewTexture(ISplineHeightmapCache cache, int previewSize = 128, Texture2D reuseTexture = null)
        {
            if (cache == null || !cache.isValid || cache.cachedHeights == null || cache.cachedAlpha == null)
                return null;

            int cacheWidth = cache.cachedHeights.GetLength(1);
            int cacheHeight = cache.cachedHeights.GetLength(0);

            if (cacheWidth <= 0 || cacheHeight <= 0) return null;

            // Create or reuse preview texture
            var previewTex = reuseTexture;
            if (previewTex == null ||
                previewTex.width != previewSize ||
                previewTex.height != previewSize ||
                previewTex.format != TextureFormat.RGBA32)
            {
                if (previewTex != null)
                {
                    UnityEngine.Object.DestroyImmediate(previewTex);
                }

                previewTex = new Texture2D(previewSize, previewSize, TextureFormat.RGBA32, false, true);
                previewTex.wrapMode = TextureWrapMode.Clamp;
            }

            var colors = GetPreviewColorBuffer(previewSize);

            // Sample from cache height data and apply color gradient
            for (int y = 0; y < previewSize; y++)
            {
                for (int x = 0; x < previewSize; x++)
                {
                    // Map preview coordinates to cache coordinates
                    float u = (float)x / (previewSize - 1);
                    float v = (float)y / (previewSize - 1);

                    int cacheX = Mathf.RoundToInt(u * (cacheWidth - 1));
                    int cacheY = Mathf.RoundToInt(v * (cacheHeight - 1));

                    cacheX = Mathf.Clamp(cacheX, 0, cacheWidth - 1);
                    cacheY = Mathf.Clamp(cacheY, 0, cacheHeight - 1);

                    float height = cache.cachedHeights[cacheY, cacheX];
                    float alpha = cache.cachedAlpha[cacheY, cacheX];

                    // Apply grayscale based on height
                    Color32 color = HeightToColor(height, alpha);
                    colors[y * previewSize + x] = color;
                }
            }

            previewTex.SetPixels32(colors);
            previewTex.Apply(false, false);

            return previewTex;
        }

        public static Color32 HeightToColor(float height, float alpha)
        {
            // Non-rasterized areas (alpha=0) should be black
            if (alpha < 0.01f)
            {
                return new Color32(0, 0, 0, 255);
            }

            // Rasterized areas: grayscale based on height
            byte grayscale = (byte)Mathf.Clamp(Mathf.RoundToInt(height * 255f), 0, 255);
            return new Color32(grayscale, grayscale, grayscale, 255);
        }

        /// <summary>
        /// Get current terrain holes array
        /// </summary>
        public static bool[,] GetTerrainHoles(Terrain terrain)
        {
            var td = terrain.terrainData;
            // Terrain holes have different dimensions than heightmap
            // Use the actual holes resolution
            int holesRes = td.holesResolution;
            return td.GetHoles(0, 0, holesRes, holesRes);
        }

        /// <summary>
        /// Apply holes to terrain with undo support
        /// </summary>
        public static void ApplyHolesToTerrain(Terrain terrain, bool[,] holes)
        {
            var td = terrain.terrainData;

#if UNITY_EDITOR
            Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Apply Terrain Holes");
#endif

            td.SetHolesDelayLOD(0, 0, holes);
        }

        /// <summary>
        /// Copy holes array
        /// </summary>
        public static bool[,] CopyHoles(bool[,] source)
        {
            if (source == null) return null;

            int height = source.GetLength(0);
            int width = source.GetLength(1);
            var result = new bool[height, width];

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    result[z, x] = source[z, x];
                }
            }

            return result;
        }

        public static void CopyHolesTo(bool[,] src, bool[,] dst)
        {
            if (src == null || dst == null) return;
            int height = src.GetLength(0);
            int width = src.GetLength(1);
            if (dst.GetLength(0) != height || dst.GetLength(1) != width) return;

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    dst[z, x] = src[z, x];
                }
            }
        }

        /// <summary>
        /// Convert heightmap holes array to terrain holes array (downsample)
        /// </summary>
        public static bool[,] ConvertHeightmapHolesToTerrainHoles(Terrain terrain, bool[,] heightmapHoles)
        {
            int heightmapRes = terrain.terrainData.heightmapResolution;
            int holesRes = terrain.terrainData.holesResolution;

            var terrainHoles = new bool[holesRes, holesRes];

            for (int z = 0; z < holesRes; z++)
            {
                for (int x = 0; x < holesRes; x++)
                {
                    // Map holes coordinate to heightmap coordinate
                    int hx = Mathf.RoundToInt((float)x * heightmapRes / holesRes);
                    int hz = Mathf.RoundToInt((float)z * heightmapRes / holesRes);

                    // Clamp and sample
                    hx = Mathf.Clamp(hx, 0, heightmapRes - 1);
                    hz = Mathf.Clamp(hz, 0, heightmapRes - 1);

                    terrainHoles[z, x] = heightmapHoles[hz, hx];
                }
            }

            return terrainHoles;
        }

        /// <summary>
        /// Convert terrain holes array to heightmap holes array (upsample)
        /// </summary>
        public static bool[,] ConvertTerrainHolesToHeightmapHoles(Terrain terrain, bool[,] terrainHoles)
        {
            int heightmapRes = terrain.terrainData.heightmapResolution;
            int holesRes = terrain.terrainData.holesResolution;

            var heightmapHoles = new bool[heightmapRes, heightmapRes];

            for (int z = 0; z < heightmapRes; z++)
            {
                for (int x = 0; x < heightmapRes; x++)
                {
                    // Map heightmap coordinate to holes coordinate
                    int hx = Mathf.RoundToInt((float)x * holesRes / heightmapRes);
                    int hz = Mathf.RoundToInt((float)z * holesRes / heightmapRes);

                    // Clamp and sample
                    hx = Mathf.Clamp(hx, 0, holesRes - 1);
                    hz = Mathf.Clamp(hz, 0, holesRes - 1);

                    heightmapHoles[z, x] = terrainHoles[hz, hx];
                }
            }

            return heightmapHoles;
        }


        public static Texture2D HeightsToTexture(Terrain terrain, float[,] heights)
        {
            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            var colors = new Color32[res * res];
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(heights[z, x] * 255f), 0, 255);
                    colors[z * res + x] = new Color32(v, v, v, 255);
                }
            }

            tex.SetPixels32(colors);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>
        /// Combine multiple terrain heightmaps into a single 256x256 texture, arranged by world-space positions
        /// </summary>
        public static Texture2D HeightsToCombinedTexture(List<(Terrain terrain, float[,] heights)> terrainData, int outputSize = 256)
        {
            if (terrainData == null || terrainData.Count == 0)
            {
                // Return empty texture if no terrains
                var emptyTex = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, false, true);
                emptyTex.wrapMode = TextureWrapMode.Clamp;
                var emptyColors = new Color32[outputSize * outputSize];
                for (int i = 0; i < emptyColors.Length; i++)
                {
                    emptyColors[i] = new Color32(0, 0, 0, 255);
                }
                emptyTex.SetPixels32(emptyColors);
                emptyTex.Apply(false, false);
                return emptyTex;
            }

            if (terrainData.Count == 1)
            {
                // Single terrain: just scale it to output size
                var (terrain, heights) = terrainData[0];
                return HeightsToCombinedTextureSingle(terrain, heights, outputSize);
            }

            // Calculate world bounds
            float minWorldX = float.MaxValue, maxWorldX = float.MinValue;
            float minWorldZ = float.MaxValue, maxWorldZ = float.MinValue;

            foreach (var (terrain, _) in terrainData)
            {
                if (terrain == null) continue;
                var td = terrain.terrainData;
                var pos = terrain.transform.position;
                float terrainMinX = pos.x;
                float terrainMaxX = pos.x + td.size.x;
                float terrainMinZ = pos.z;
                float terrainMaxZ = pos.z + td.size.z;

                minWorldX = Mathf.Min(minWorldX, terrainMinX);
                maxWorldX = Mathf.Max(maxWorldX, terrainMaxX);
                minWorldZ = Mathf.Min(minWorldZ, terrainMinZ);
                maxWorldZ = Mathf.Max(maxWorldZ, terrainMaxZ);
            }

            float worldWidth = maxWorldX - minWorldX;
            float worldDepth = maxWorldZ - minWorldZ;

            if (worldWidth <= 0 || worldDepth <= 0)
            {
                // Fallback: use single terrain method
                var (terrain, heights) = terrainData[0];
                return HeightsToCombinedTextureSingle(terrain, heights, outputSize);
            }

            // Create output texture
            var combinedTex = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, false, true);
            combinedTex.wrapMode = TextureWrapMode.Clamp;
            var combinedColors = new Color32[outputSize * outputSize];

            // Initialize to black
            for (int i = 0; i < combinedColors.Length; i++)
            {
                combinedColors[i] = new Color32(0, 0, 0, 255);
            }

            // Calculate uniform tile size for all terrains
            // Use grid layout to determine tile size, but arrange by world-space position
            int terrainCount = terrainData.Count;
            int cols = Mathf.CeilToInt(Mathf.Sqrt(terrainCount));
            int rows = Mathf.CeilToInt((float)terrainCount / cols);
            int tileSize = outputSize / Mathf.Max(cols, rows);

            // Process each terrain
            for (int idx = 0; idx < terrainData.Count; idx++)
            {
                var (terrain, heights) = terrainData[idx];
                if (terrain == null || heights == null) continue;

                var td = terrain.terrainData;
                var pos = terrain.transform.position;
                
                // Calculate normalized position in world bounds (center of terrain)
                float terrainCenterX = pos.x + td.size.x * 0.5f;
                float terrainCenterZ = pos.z + td.size.z * 0.5f;
                float normX = (terrainCenterX - minWorldX) / worldWidth;
                float normZ = (terrainCenterZ - minWorldZ) / worldDepth;

                // Map to output texture coordinates (preserving world-space arrangement)
                // Center the tile at the normalized position
                int outputStartX = Mathf.RoundToInt(normX * outputSize) - tileSize / 2;
                int outputStartZ = Mathf.RoundToInt(normZ * outputSize) - tileSize / 2;
                
                // Clamp to ensure we don't go out of bounds
                outputStartX = Mathf.Clamp(outputStartX, 0, outputSize - tileSize);
                outputStartZ = Mathf.Clamp(outputStartZ, 0, outputSize - tileSize);

                // Scale terrain heightmap to fit tile (uniform scaling)
                int terrainRes = heights.GetLength(0);
                for (int outZ = 0; outZ < tileSize; outZ++)
                {
                    for (int outX = 0; outX < tileSize; outX++)
                    {
                        // Sample from terrain heightmap
                        float terrainX = (float)outX / (tileSize - 1);
                        float terrainZ = (float)outZ / (tileSize - 1);
                        
                        int srcX = Mathf.Clamp(Mathf.RoundToInt(terrainX * (terrainRes - 1)), 0, terrainRes - 1);
                        int srcZ = Mathf.Clamp(Mathf.RoundToInt(terrainZ * (terrainRes - 1)), 0, terrainRes - 1);
                        
                        float height = heights[srcZ, srcX];
                        byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(height * 255f), 0, 255);
                        
                        // Write to combined texture
                        int combinedX = outputStartX + outX;
                        int combinedZ = outputStartZ + outZ;
                        
                        if (combinedX >= 0 && combinedX < outputSize && combinedZ >= 0 && combinedZ < outputSize)
                        {
                            combinedColors[combinedZ * outputSize + combinedX] = new Color32(v, v, v, 255);
                        }
                    }
                }
            }

            combinedTex.SetPixels32(combinedColors);
            combinedTex.Apply(false, false);
            return combinedTex;
        }

        /// <summary>
        /// Helper method to scale a single terrain heightmap to output size
        /// </summary>
        static Texture2D HeightsToCombinedTextureSingle(Terrain terrain, float[,] heights, int outputSize)
        {
            if (heights == null) return null;
            
            int terrainRes = heights.GetLength(0);
            var tex = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            var colors = new Color32[outputSize * outputSize];
            
            for (int z = 0; z < outputSize; z++)
            {
                for (int x = 0; x < outputSize; x++)
                {
                    // Sample from terrain heightmap
                    float terrainX = (float)x / (outputSize - 1);
                    float terrainZ = (float)z / (outputSize - 1);
                    
                    int srcX = Mathf.Clamp(Mathf.RoundToInt(terrainX * (terrainRes - 1)), 0, terrainRes - 1);
                    int srcZ = Mathf.Clamp(Mathf.RoundToInt(terrainZ * (terrainRes - 1)), 0, terrainRes - 1);
                    
                    float height = heights[srcZ, srcX];
                    byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(height * 255f), 0, 255);
                    colors[z * outputSize + x] = new Color32(v, v, v, 255);
                }
            }
            
            tex.SetPixels32(colors);
            tex.Apply(false, false);
            return tex;
        }

        public static float[,] CopyHeights(float[,] src)
        {
            int h = src.GetLength(0);
            int w = src.GetLength(1);
            var dst = new float[h, w];
            Buffer.BlockCopy(src, 0, dst, 0, sizeof(float) * w * h);
            return dst;
        }

        public static void CopyHeightsTo(float[,] src, float[,] dst)
        {
            if (src == null || dst == null) return;
            int h = src.GetLength(0);
            int w = src.GetLength(1);
            if (dst.GetLength(0) != h || dst.GetLength(1) != w) return;
            Buffer.BlockCopy(src, 0, dst, 0, sizeof(float) * w * h);
        }

        public static int WorldToHeightmapX(Terrain terrain, float worldX)
        {
            var td = terrain.terrainData;
            float rel = (worldX - terrain.transform.position.x) / td.size.x;
            return Mathf.Clamp(Mathf.RoundToInt(rel * (td.heightmapResolution - 1)), 0, td.heightmapResolution - 1);
        }

        public static int WorldToHeightmapZ(Terrain terrain, float worldZ)
        {
            var td = terrain.terrainData;
            float rel = (worldZ - terrain.transform.position.z) / td.size.z;
            return Mathf.Clamp(Mathf.RoundToInt(rel * (td.heightmapResolution - 1)), 0, td.heightmapResolution - 1);
        }

        public static float WorldYToNormalizedHeight(Terrain terrain, float worldY)
        {
            var td = terrain.terrainData;
            return Mathf.InverseLerp(terrain.transform.position.y, terrain.transform.position.y + td.size.y, worldY);
        }

        public static Vector2Int WorldToHeightmap(Terrain terrain, Vector3 worldPos)
        {
            return new Vector2Int(
                WorldToHeightmapX(terrain, worldPos.x),
                WorldToHeightmapZ(terrain, worldPos.z)
            );
        }

        public static void ApplyPreviewToTerrain(Terrain terrain, float[,] previewHeights)
        {
            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            terrain.terrainData.SetHeightsDelayLOD(0, 0, previewHeights);
#if UNITY_EDITOR
            terrain.terrainData.SyncHeightmap();
#endif
        }

        public static void ApplyPreviewToTerrain(Terrain terrain, float[,] previewHeights, bool[,] previewHoles)
        {
            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            terrain.terrainData.SetHeightsDelayLOD(0, 0, previewHeights);
            if (previewHoles != null)
            {
                var terrainHoles = ConvertHeightmapHolesToTerrainHoles(terrain, previewHoles);
                terrain.terrainData.SetHolesDelayLOD(0, 0, terrainHoles);
            }
#if UNITY_EDITOR
            terrain.terrainData.SyncHeightmap();
#endif
        }

        public static void ApplyPreviewToTerrain(Terrain terrain, float[,] previewHeights, bool[,] previewHoles, float[,,] previewAlphamaps)
        {
            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            terrain.terrainData.SetHeightsDelayLOD(0, 0, previewHeights);
            if (previewHoles != null)
            {
                var terrainHoles = ConvertHeightmapHolesToTerrainHoles(terrain, previewHoles);
                terrain.terrainData.SetHolesDelayLOD(0, 0, terrainHoles);
            }

            if (previewAlphamaps != null)
            {
                terrain.terrainData.SetAlphamaps(0, 0, previewAlphamaps);
            }
#if UNITY_EDITOR
            terrain.terrainData.SyncHeightmap();
#endif
        }

        public static void ApplyFinalToTerrainWithUndo(Terrain terrain, float[,] finalHeights, string undoName)
        {
            var td = terrain.terrainData;
#if UNITY_EDITOR
            Undo.RegisterCompleteObjectUndo(td, undoName);
#endif
            td.SetHeightsDelayLOD(0, 0, finalHeights);
#if UNITY_EDITOR
            td.SyncHeightmap();
#endif
        }

        public static void ApplyOffsetToHeights(Terrain terrain, float[,] baseHeights, float[,] outHeights, float offsetMeters)
        {
            var td = terrain.terrainData;
            int res = td.heightmapResolution;

            // Convert meters to normalized height
            float normalizedOffset = offsetMeters / td.size.y;

            // Apply offset to all heights
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float newHeight = baseHeights[z, x] + normalizedOffset;
                    outHeights[z, x] = Mathf.Clamp01(newHeight);
                }
            }
        }

        public static void ApplyLevelToHeights(Terrain terrain, float[,] baseHeights, float[,] outHeights, float offsetMeters)
        {
            var td = terrain.terrainData;
            int res = td.heightmapResolution;

            // Find minimum height in baseline
            float minHeight = float.MaxValue;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    if (baseHeights[z, x] < minHeight)
                        minHeight = baseHeights[z, x];
                }
            }

            // Convert offset meters to normalized height
            float normalizedOffset = offsetMeters / td.size.y;
            float targetHeight = Mathf.Clamp01(minHeight + normalizedOffset);

            // Set all heights to target height
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    outHeights[z, x] = targetHeight;
                }
            }
        }

        public static void FillAllHoles(Terrain terrain, bool[,] holes)
        {
            int res = holes.GetLength(0);
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    holes[z, x] = true; // true = solid (no hole)
                }
            }
        }

        /// <summary>
        /// Get all Terrain components from a GameObject and its children
        /// </summary>
        public static List<Terrain> GetAllTerrainsInGroup(GameObject group)
        {
            var terrains = new List<Terrain>();
            if (group == null) return terrains;
            
            terrains.AddRange(group.GetComponentsInChildren<Terrain>());
            return terrains;
        }

        /// <summary>
        /// Get combined world-space bounds of all terrains
        /// </summary>
        public static Bounds GetCombinedTerrainBounds(List<Terrain> terrains)
        {
            if (terrains == null || terrains.Count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            bool first = true;
            Bounds combined = default(Bounds);

            foreach (var terrain in terrains)
            {
                if (terrain == null) continue;
                
                var terrainBounds = TerrainCoordinates.GetTerrainBounds(terrain);
                if (first)
                {
                    combined = terrainBounds;
                    first = false;
                }
                else
                {
                    combined.Encapsulate(terrainBounds);
                }
            }

            return combined;
        }

        public static void RasterizeSplines(
            Terrain terrain,
            IReadOnlyList<(SplineContainer container, SplineStrokeSettings settings, int priority)> orderedSplines,
            GlobalSettings globals,
            float[,] baseHeights,
            float[,] outHeights,
            Transform splineGroup = null
        )
        {
            RasterizeSplines(terrain, orderedSplines, globals, baseHeights, outHeights, null, null, splineGroup);
        }

        public static void RasterizeSplines(
            Terrain terrain,
            IReadOnlyList<(SplineContainer container, SplineStrokeSettings settings, int priority)> orderedSplines,
            GlobalSettings globals,
            float[,] baseHeights,
            float[,] outHeights,
            bool[,] baseHoles,
            bool[,] outHoles,
            Transform splineGroup = null
        )
        {
            RasterizeSplines(terrain, orderedSplines, globals, baseHeights, outHeights, baseHoles, outHoles, null, null, splineGroup, true, true, true, null, null, null, null);
        }

        public static void RasterizeSplines(
            Terrain terrain,
            IReadOnlyList<(SplineContainer container, SplineStrokeSettings settings, int priority)> orderedSplines,
            GlobalSettings globals,
            float[,] baseHeights,
            float[,] outHeights,
            bool[,] baseHoles,
            bool[,] outHoles,
            float[,,] baseAlphamaps,
            float[,,] outAlphamaps,
            Transform splineGroup = null,
            bool enableHoleOperations = true,
            bool enablePaintOperations = true,
            bool enableDetailOperations = true,
            float[,] paintSplineAlphaMask = null, // Optional mask output for tracking paint operations
            bool[,] holeSplineMask = null, // Optional mask output for tracking hole operations
            int[][,] baseDetailLayers = null, // Optional base detail layers for grass operations
            int[][,] outDetailLayers = null // Optional output detail layers for grass operations
        )
        {
            int res = terrain.terrainData.heightmapResolution;
            int holesRes = terrain.terrainData.holesResolution;

            // Initialize masks if provided (only when features are enabled)
            if (paintSplineAlphaMask != null)
            {
                for (int z = 0; z < res; z++)
                {
                    for (int x = 0; x < res; x++) paintSplineAlphaMask[z, x] = 0f;
                }
            }

            if (holeSplineMask != null)
            {
                for (int z = 0; z < res; z++)
                {
                    for (int x = 0; x < res; x++) holeSplineMask[z, x] = false;
                }
            }

            // Copy heightmap data using Array.Copy for better performance
            for (int z = 0; z < res; z++)
            {
                System.Array.Copy(baseHeights, z * res, outHeights, z * res, res);
            }

            // track which priority wrote the pixel last; -1 means none
            int[,] writePriority = new int[res, res];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                writePriority[z, x] = -1;

            // Copy holes array using Array.Copy
            if (outHoles != null && baseHoles != null)
            {
                for (int z = 0; z < res; z++)
                {
                    System.Array.Copy(baseHoles, z * res, outHoles, z * res, res);
                }
            }

            // Copy alphamaps using Array.Copy
            if (outAlphamaps != null && baseAlphamaps != null)
            {
                int alphamapHeight = outAlphamaps.GetLength(0);
                int alphamapWidth = outAlphamaps.GetLength(1);
                int layers = outAlphamaps.GetLength(2);

                // Copy 3D array by copying each 2D slice
                for (int z = 0; z < alphamapHeight; z++)
                {
                    for (int l = 0; l < layers; l++)
                    {
                        // Calculate source and destination indices for this slice
                        int srcIndex = z * (alphamapWidth * layers) + l * alphamapWidth;
                        int dstIndex = z * (alphamapWidth * layers) + l * alphamapWidth;
                        System.Array.Copy(baseAlphamaps, srcIndex, outAlphamaps, dstIndex, alphamapWidth);
                    }
                }
            }

            // Copy detail layers for detail operations (when supplied)
            if (enableDetailOperations && outDetailLayers != null && baseDetailLayers != null)
            {
                int layerCount = Mathf.Min(outDetailLayers.Length, baseDetailLayers.Length);
                for (int layer = 0; layer < layerCount; layer++)
                {
                    var baseLayer = baseDetailLayers[layer];
                    if (baseLayer == null) continue;

                    int dh = baseLayer.GetLength(0);
                    int dw = baseLayer.GetLength(1);

                    var outLayer = outDetailLayers[layer];
                    if (outLayer == null || outLayer.GetLength(0) != dh || outLayer.GetLength(1) != dw)
                    {
                        outLayer = new int[dh, dw];
                        outDetailLayers[layer] = outLayer;
                    }

                    Buffer.BlockCopy(baseLayer, 0, outLayer, 0, sizeof(int) * dh * dw);
                }
            }

            // Process all splines individually
            foreach (var entry in orderedSplines)
            {
                if (entry.container == null || !entry.settings.enabled)
                {
                    continue;
                }

                var mode = entry.settings.overrideMode ? entry.settings.mode : globals.mode;
                float brushHardness = entry.settings.overrideBrush ? entry.settings.hardness : globals.brushHardness;
                float brushSize = entry.settings.overrideBrush ? entry.settings.sizeMeters : globals.brushSizeMeters;
                float strength = entry.settings.overrideBrush ? entry.settings.strength : globals.strength;
                float step = entry.settings.overrideBrush ? entry.settings.sampleStep : globals.sampleStepMeters;
                var brushSizeMultiplier = (entry.settings.overrideBrush && entry.settings.overrideSizeMultiplier) ? entry.settings.sizeMultiplier : AnimationCurve.Constant(0f, 1f, 1f);

                var operations = ResolveOperations(entry.settings, globals);

                // Skip operations blocked by feature toggles
                if (!enableHoleOperations)
                {
                    operations.hole = false;
                    operations.fill = false;
                }

                if (!enablePaintOperations)
                {
                    operations.paint = false;
                }

                if (!enableDetailOperations)
                {
                    operations.addDetail = false;
                    operations.removeDetail = false;
                }

                if (!operations.Any)
                {
                    continue;
                }

                // Use strategy pattern to delegate to appropriate strategy
                if (strategies.TryGetValue(mode, out var strategy))
                {
                    // Rasterize once to cache
                    ISplineHeightmapCache cache = strategy.RasterizeToCache(
                        terrain, entry.container, brushHardness, brushSize, step, mode, brushSizeMultiplier
                    );

                    var appliers = BuildOperationList(operations);
                    if (appliers.Count == 0)
                    {
                        continue;
                    }

                    // Create HashSet for batch normalization tracking (only when paint is active)
                    var pixelsNeedingNormalization = operations.paint
                        ? new System.Collections.Generic.HashSet<(int x, int z)>()
                        : null;

                    bool usesDetail = operations.addDetail || operations.removeDetail;
                    bool useOverrideDetail = entry.settings.overrideDetail;
                    int[] selectedDetailLayerIndices = null;
                    if (usesDetail && outDetailLayers != null && outDetailLayers.Length > 0)
                    {
                        if (operations.removeDetail)
                        {
                            int layerCount = outDetailLayers.Length;
                            selectedDetailLayerIndices = new int[layerCount];
                            for (int i = 0; i < layerCount; i++)
                            {
                                selectedDetailLayerIndices[i] = i;
                            }
                        }
                        else
                        {
                            selectedDetailLayerIndices = BuildSelectedDetailLayerIndices(entry.settings, globals, useOverrideDetail, outDetailLayers.Length);
                        }
                    }

                    var selectedDetailNoiseLayers = usesDetail
                        ? BuildSelectedDetailNoiseLayers(entry.settings, globals, useOverrideDetail, selectedDetailLayerIndices)
                        : System.Array.Empty<DetailNoiseLayerSettings>();

                    bool usesPaint = operations.paint;
                    bool useOverridePaint = entry.settings.overridePaint;
                    int selectedPaintLayerIndex = usesPaint
                        ? (useOverridePaint ? entry.settings.selectedLayerIndex : globals.globalSelectedLayerIndex)
                        : 0;
                    var selectedPaintNoiseLayers = usesPaint
                        ? BuildSelectedPaintNoiseLayers(entry.settings, globals, useOverridePaint, selectedPaintLayerIndex)
                        : System.Array.Empty<PaintNoiseLayerSettings>();

                    var context = new OperationContext
                    {
                        strength = strength,
                        brushNoise = ResolveBrushNoiseSettings(entry.settings, globals, entry.settings.overrideBrush),
                        heights = operations.height ? outHeights : null,
                        holes = (operations.hole || operations.fill) ? outHoles : null,
                        alphamaps = operations.paint ? outAlphamaps : null,
                        targetLayerIndex = operations.paint ? selectedPaintLayerIndex : 0,
                        paintStrength = operations.paint
                            ? (useOverridePaint ? entry.settings.paintStrength : globals.globalPaintStrength)
                            : 0f,
                        paintNoiseLayers = selectedPaintNoiseLayers,
                        paintSplineAlphaMask = paintSplineAlphaMask,
                        holeSplineMask = holeSplineMask,
                        pixelsNeedingNormalization = pixelsNeedingNormalization,
                        detailLayerIndex = usesDetail
                            ? (selectedDetailLayerIndices != null && selectedDetailLayerIndices.Length > 0
                                ? selectedDetailLayerIndices[0]
                                : (useOverrideDetail ? entry.settings.selectedDetailLayerIndex : globals.globalSelectedDetailLayerIndex))
                            : 0,
                        detailLayerIndices = selectedDetailLayerIndices,
                        detailStrength = usesDetail
                            ? (useOverrideDetail ? entry.settings.detailStrength : globals.globalDetailStrength)
                            : 0f,
                        detailData = null,
                        detailLayer = null,
                        detailLayers = null,
                        detailMode = operations.removeDetail ? DetailOperationMode.Remove : DetailOperationMode.Add,
                        detailTargetDensity = usesDetail
                            ? (useOverrideDetail ? entry.settings.detailTargetDensity : globals.globalDetailTargetDensity)
                            : 0,
                        detailSlopeLimitDegrees = usesDetail
                            ? (useOverrideDetail ? entry.settings.detailSlopeLimitDegrees : globals.globalDetailSlopeLimitDegrees)
                            : 90f,
                        detailFalloffPower = usesDetail
                            ? (useOverrideDetail ? entry.settings.detailFalloffPower : globals.globalDetailFalloffPower)
                            : 1f,
                        detailSpreadRadius = usesDetail
                            ? (useOverrideDetail ? entry.settings.detailSpreadRadius : globals.globalDetailSpreadRadius)
                            : 0,
                        detailRemoveThreshold = usesDetail
                            ? (useOverrideDetail ? entry.settings.detailRemoveThreshold : globals.globalDetailRemoveThreshold)
                            : 0.6f,
                        detailNoiseLayers = selectedDetailNoiseLayers
                    };

                    if (usesDetail && outDetailLayers != null && outDetailLayers.Length > 0 && selectedDetailLayerIndices != null && selectedDetailLayerIndices.Length > 0)
                    {
                        context.detailLayers = new int[selectedDetailLayerIndices.Length][,];
                        for (int i = 0; i < selectedDetailLayerIndices.Length; i++)
                        {
                            int idx = Mathf.Clamp(selectedDetailLayerIndices[i], 0, outDetailLayers.Length - 1);
                            context.detailLayers[i] = outDetailLayers[idx];
                        }
                        context.detailLayer = context.detailLayers[0];
                    }

                    // Validate layer index for paint operations
                    bool canApplyPaint = context.alphamaps != null && context.targetLayerIndex >= 0 && context.targetLayerIndex < context.alphamaps.GetLength(2);

                    // If paint can't be applied, still allow the rest of the operations, but prevent appliers from touching invalid alphamaps.
                    if (!canApplyPaint && context.alphamaps != null && operations.paint)
                    {
                        context.alphamaps = null;
                    }

                    bool shouldApply =
                        (operations.paint ? canApplyPaint : false)
                        || context.heights != null
                        || context.holes != null
                        || context.detailLayer != null
                        || (context.detailLayers != null && context.detailLayers.Length > 0);

                    if (!shouldApply) continue;

                    foreach (var kind in appliers)
                    {
                        if (!operationAppliers.TryGetValue(kind, out var applier))
                        {
                            continue;
                        }

                        if (kind == OperationKind.Paint && !canApplyPaint)
                        {
                            continue;
                        }

                        if ((kind == OperationKind.Hole || kind == OperationKind.Fill) && context.holes == null)
                        {
                            continue;
                        }

                        if ((kind == OperationKind.DetailAdd || kind == OperationKind.DetailRemove)
                            && context.detailLayer == null
                            && (context.detailLayers == null || context.detailLayers.Length == 0))
                        {
                            continue;
                        }

                        if (kind == OperationKind.DetailAdd)
                        {
                            context.detailMode = DetailOperationMode.Add;
                        }
                        else if (kind == OperationKind.DetailRemove)
                        {
                            context.detailMode = DetailOperationMode.Remove;
                        }

                        applier.Apply(terrain, cache, entry.priority, writePriority, context);
                    }

                    // Batch normalize all pixels that were modified (only if paint was actually applied)
                    if (operations.paint && canApplyPaint && pixelsNeedingNormalization != null && pixelsNeedingNormalization.Count > 0)
                    {
                        foreach (var (x, z) in pixelsNeedingNormalization)
                        {
                            NormalizeAlphamaps(outAlphamaps, x, z);
                        }

                        pixelsNeedingNormalization.Clear();
                    }
                }
            }
        }

        /// <summary>
        /// Rasterize splines to alphamaps for paint operations
        /// </summary>
        public static void RasterizeSplinesToAlphamaps(
            Terrain terrain,
            IReadOnlyList<(SplineContainer container, SplineStrokeSettings settings, int priority)> orderedSplines,
            GlobalSettings globals,
            float[,,] baseAlphamaps,
            float[,,] outAlphamaps,
            Transform splineGroup = null
        )
        {
            if (terrain == null || baseAlphamaps == null || outAlphamaps == null) return;


            var td = terrain.terrainData;
            int alphamapHeight = td.alphamapHeight;
            int alphamapWidth = td.alphamapWidth;
            int layers = baseAlphamaps.GetLength(2);

            // Safety check - if no layers, nothing to do
            if (layers <= 0) return;

            // Start from base
            for (int z = 0; z < alphamapHeight; z++)
            {
                for (int x = 0; x < alphamapWidth; x++)
                {
                    for (int l = 0; l < layers; l++)
                    {
                        outAlphamaps[z, x, l] = baseAlphamaps[z, x, l];
                    }
                }
            }

            // Track which priority wrote the pixel last; -1 means none
            int[,] writePriority = new int[alphamapHeight, alphamapWidth];
            for (int z = 0; z < alphamapHeight; z++)
            {
                for (int x = 0; x < alphamapWidth; x++)
                {
                    writePriority[z, x] = -1;
                }
            }

            // Process all splines individually
            foreach (var entry in orderedSplines)
            {
                if (entry.container == null || !entry.settings.enabled)
                {
                    continue;
                }

                var mode = entry.settings.overrideMode ? entry.settings.mode : globals.mode;
                float brushHardness = entry.settings.overrideBrush ? entry.settings.hardness : globals.brushHardness;
                float brushSize = entry.settings.overrideBrush ? entry.settings.sizeMeters : globals.brushSizeMeters;
                float step = entry.settings.overrideBrush ? entry.settings.sampleStep : globals.sampleStepMeters;
                var brushSizeMultiplier = (entry.settings.overrideBrush && entry.settings.overrideSizeMultiplier) ? entry.settings.sizeMultiplier : AnimationCurve.Constant(0f, 1f, 1f);

                // Determine operation toggles for this spline
                var operations = ResolveOperations(entry.settings, globals);
                if (!operations.paint) continue;

                // Get paint settings
                int layerIndex = entry.settings.overridePaint ? entry.settings.selectedLayerIndex : globals.globalSelectedLayerIndex;
                float paintStrength = entry.settings.overridePaint ? entry.settings.paintStrength : globals.globalPaintStrength;

                // Validate layer index
                if (layerIndex < 0 || layerIndex >= layers) continue;

                // Use strategy pattern to delegate to appropriate strategy
                if (strategies.TryGetValue(mode, out var strategy))
                {
                    // Rasterize once to cache
                    ISplineHeightmapCache cache = strategy.RasterizeToCache(
                        terrain, entry.container, brushHardness, brushSize, step, mode, brushSizeMultiplier
                    );

                    // Get the paint applier
                    if (operationAppliers.TryGetValue(OperationKind.Paint, out var applier))
                    {
                        // Prepare operation context for paint operations
                        var context = new OperationContext
                        {
                            strength = paintStrength,
                            brushNoise = ResolveBrushNoiseSettings(entry.settings, globals, entry.settings.overrideBrush),
                            heights = null,
                            holes = null,
                            alphamaps = outAlphamaps,
                            targetLayerIndex = layerIndex,
                            paintStrength = paintStrength
                        };

                        // Apply the operation using the applier
                        applier.Apply(terrain, cache, entry.priority, writePriority, context);
                    }
                }
            }
        }

        public static void StampBrush(
            Terrain terrain,
            Texture2D brush,
            float brushSizeMeters,
            float strength,
            int childPriority,
            int[,] writePriority,
            float[,] heights,
            float worldX,
            float worldZ,
            float targetHeight
        )
        {
            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            float radiusPx = TerrainCoordinates.MetersToHeightmapPixels(terrain, brushSizeMeters);
            if (radiusPx <= 0.5f) return;
            int r = Mathf.CeilToInt(radiusPx);
            int cx = TerrainCoordinates.WorldToHeightmapX(terrain, worldX);
            int cz = TerrainCoordinates.WorldToHeightmapZ(terrain, worldZ);

            for (int dz = -r; dz <= r; dz++)
            {
                int z = cz + dz;
                if (z < 0 || z >= res) continue;
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = cx + dx;
                    if (x < 0 || x >= res) continue;
                    float distSq = dx * dx + dz * dz;
                    if (distSq > radiusPx * radiusPx) continue;

                    float u = (dx / (radiusPx * 2f)) + 0.5f;
                    float v = (dz / (radiusPx * 2f)) + 0.5f;
                    float brushA = brush.GetPixelBilinear(u, v).a;
                    if (brushA <= 0.0001f) continue;

                    if (childPriority >= writePriority[z, x])
                    {
                        float src = heights[z, x];
                        float a = brushA * Mathf.Clamp01(strength);
                        heights[z, x] = Mathf.Lerp(src, targetHeight, a);
                        writePriority[z, x] = childPriority;
                    }
                }
            }
        }

        /// <summary>
        /// Apply a single spline to the baseline heights, permanently committing its contribution.
        /// This rasterizes only the specified spline onto a copy of the baseline and returns the result.
        /// </summary>
        public static void ApplySingleSplineToBaseline(
            Terrain terrain,
            SplineContainer container,
            SplineStrokeSettings settings,
            GlobalSettings globals,
            float[,] baselineHeights,
            float[,] outputHeights,
            bool[,] baselineHoles = null,
            bool[,] outputHoles = null
        )
        {
            ApplySingleSplineToBaseline(terrain, container, settings, globals, baselineHeights, outputHeights, baselineHoles, outputHoles, null, null);
        }

        public static void ApplySingleSplineToBaseline(
            Terrain terrain,
            SplineContainer container,
            SplineStrokeSettings settings,
            GlobalSettings globals,
            float[,] baselineHeights,
            float[,] outputHeights,
            bool[,] baselineHoles,
            bool[,] outputHoles,
            float[,,] baselineAlphamaps,
            float[,,] outputAlphamaps,
            int[][,] baselineDetailLayers = null,
            int[][,] outputDetailLayers = null
        )
        {
            if (container == null || !settings.enabled)
            {
                // If spline is disabled, just copy baseline to output
                CopyHeights(baselineHeights, outputHeights);
                if (baselineHoles != null && outputHoles != null)
                {
                    CopyHoles(baselineHoles, outputHoles);
                }

                if (baselineAlphamaps != null && outputAlphamaps != null)
                {
                    CopyAlphamaps(baselineAlphamaps, outputAlphamaps);
                }

                if (baselineDetailLayers != null && outputDetailLayers != null)
                {
                    int layerCount = Mathf.Min(baselineDetailLayers.Length, outputDetailLayers.Length);
                    for (int layer = 0; layer < layerCount; layer++)
                    {
                        var src = baselineDetailLayers[layer];
                        if (src == null) continue;

                        int dh = src.GetLength(0);
                        int dw = src.GetLength(1);

                        var dst = outputDetailLayers[layer];
                        if (dst == null || dst.GetLength(0) != dh || dst.GetLength(1) != dw)
                        {
                            dst = new int[dh, dw];
                            outputDetailLayers[layer] = dst;
                        }

                        Buffer.BlockCopy(src, 0, dst, 0, sizeof(int) * dh * dw);
                    }
                }

                return;
            }

            int res = terrain.terrainData.heightmapResolution;

            // Start with a copy of the baseline
            CopyHeights(baselineHeights, outputHeights);
            if (baselineHoles != null && outputHoles != null)
            {
                CopyHoles(baselineHoles, outputHoles);
            }

            // Initialize alphamaps if provided
            if (baselineAlphamaps != null && outputAlphamaps != null)
            {
                CopyAlphamaps(baselineAlphamaps, outputAlphamaps);
            }

            // Initialize detail layers if provided
            if (baselineDetailLayers != null && outputDetailLayers != null)
            {
                int layerCount = Mathf.Min(baselineDetailLayers.Length, outputDetailLayers.Length);
                for (int layer = 0; layer < layerCount; layer++)
                {
                    var src = baselineDetailLayers[layer];
                    if (src == null) continue;

                    int dh = src.GetLength(0);
                    int dw = src.GetLength(1);

                    var dst = outputDetailLayers[layer];
                    if (dst == null || dst.GetLength(0) != dh || dst.GetLength(1) != dw)
                    {
                        dst = new int[dh, dw];
                        outputDetailLayers[layer] = dst;
                    }

                    Buffer.BlockCopy(src, 0, dst, 0, sizeof(int) * dh * dw);
                }
            }

            // Initialize write priority array (single spline gets priority 0)
            int[,] writePriority = new int[res, res];
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    writePriority[z, x] = -1;
                }
            }

            // Determine settings for this spline
            var mode = settings.overrideMode ? settings.mode : globals.mode;
            float brushHardness = settings.overrideBrush ? settings.hardness : globals.brushHardness;
            float brushSize = settings.overrideBrush ? settings.sizeMeters : globals.brushSizeMeters;
            float strength = settings.overrideBrush ? settings.strength : globals.strength;
            float step = settings.overrideBrush ? settings.sampleStep : globals.sampleStepMeters;
            var brushSizeMultiplier = (settings.overrideBrush && settings.overrideSizeMultiplier) ? settings.sizeMultiplier : AnimationCurve.Constant(0f, 1f, 1f);

            // Determine operation toggles for this spline (use same override as mode)
            var operations = ResolveOperations(settings, globals);

            // Use strategy pattern to rasterize this single spline
            if (strategies.TryGetValue(mode, out var strategy))
            {
                // Rasterize once to cache
                ISplineHeightmapCache cache = strategy.RasterizeToCache(
                    terrain, container, brushHardness, brushSize, step, mode, brushSizeMultiplier
                );

                var appliers = BuildOperationList(operations);
                if (appliers.Count == 0)
                {
                    return;
                }

                bool usesDetail = operations.addDetail || operations.removeDetail;
                bool useOverrideDetail = settings.overrideDetail;
                int[] selectedDetailLayerIndices = null;
                if (usesDetail && outputDetailLayers != null && outputDetailLayers.Length > 0)
                {
                    selectedDetailLayerIndices = BuildSelectedDetailLayerIndices(settings, globals, useOverrideDetail, outputDetailLayers.Length);
                }
                var selectedDetailNoiseLayers = usesDetail
                    ? BuildSelectedDetailNoiseLayers(settings, globals, useOverrideDetail, selectedDetailLayerIndices)
                    : System.Array.Empty<DetailNoiseLayerSettings>();

                bool usesPaint = operations.paint;
                bool useOverridePaint = settings.overridePaint;
                int selectedPaintLayerIndex = usesPaint
                    ? (useOverridePaint ? settings.selectedLayerIndex : globals.globalSelectedLayerIndex)
                    : 0;
                var selectedPaintNoiseLayers = usesPaint
                    ? BuildSelectedPaintNoiseLayers(settings, globals, useOverridePaint, selectedPaintLayerIndex)
                    : System.Array.Empty<PaintNoiseLayerSettings>();

                var context = new OperationContext
                {
                    strength = strength,
                    brushNoise = ResolveBrushNoiseSettings(settings, globals, settings.overrideBrush),
                    heights = operations.height ? outputHeights : null,
                    holes = (operations.hole || operations.fill) ? outputHoles : null,
                    alphamaps = operations.paint ? outputAlphamaps : null,
                    targetLayerIndex = operations.paint ? selectedPaintLayerIndex : 0,
                    paintStrength = operations.paint
                        ? (useOverridePaint ? settings.paintStrength : globals.globalPaintStrength)
                        : 0f,
                    paintNoiseLayers = selectedPaintNoiseLayers,
                    detailLayerIndex = usesDetail
                        ? (selectedDetailLayerIndices != null && selectedDetailLayerIndices.Length > 0
                            ? selectedDetailLayerIndices[0]
                            : (useOverrideDetail ? settings.selectedDetailLayerIndex : globals.globalSelectedDetailLayerIndex))
                        : 0,
                    detailLayerIndices = selectedDetailLayerIndices,
                    detailStrength = usesDetail
                        ? (useOverrideDetail ? settings.detailStrength : globals.globalDetailStrength)
                        : 0f,
                    detailData = null,
                    detailLayer = null,
                    detailLayers = null,
                    detailMode = operations.removeDetail ? DetailOperationMode.Remove : DetailOperationMode.Add,
                    detailTargetDensity = usesDetail
                        ? (useOverrideDetail ? settings.detailTargetDensity : globals.globalDetailTargetDensity)
                        : 0,
                    detailSlopeLimitDegrees = usesDetail
                        ? (useOverrideDetail ? settings.detailSlopeLimitDegrees : globals.globalDetailSlopeLimitDegrees)
                        : 90f,
                    detailFalloffPower = usesDetail
                        ? (useOverrideDetail ? settings.detailFalloffPower : globals.globalDetailFalloffPower)
                        : 1f,
                    detailSpreadRadius = usesDetail
                        ? (useOverrideDetail ? settings.detailSpreadRadius : globals.globalDetailSpreadRadius)
                        : 0,
                    detailRemoveThreshold = usesDetail
                        ? (useOverrideDetail ? settings.detailRemoveThreshold : globals.globalDetailRemoveThreshold)
                        : 0.6f,
                    detailNoiseLayers = selectedDetailNoiseLayers
                };

                if (usesDetail && outputDetailLayers != null && outputDetailLayers.Length > 0 && selectedDetailLayerIndices != null && selectedDetailLayerIndices.Length > 0)
                {
                    context.detailLayers = new int[selectedDetailLayerIndices.Length][,];
                    for (int i = 0; i < selectedDetailLayerIndices.Length; i++)
                    {
                        int idx = Mathf.Clamp(selectedDetailLayerIndices[i], 0, outputDetailLayers.Length - 1);
                        context.detailLayers[i] = outputDetailLayers[idx];
                    }
                    context.detailLayer = context.detailLayers[0];
                }

                // Validate layer index for paint operations
                bool canApplyPaint = context.alphamaps != null && context.targetLayerIndex >= 0 && context.targetLayerIndex < context.alphamaps.GetLength(2);

                // If paint can't be applied, still allow the rest of the operations, but prevent appliers from touching invalid alphamaps.
                if (!canApplyPaint && context.alphamaps != null && operations.paint)
                {
                    context.alphamaps = null;
                }

                bool shouldApply =
                    (operations.paint ? canApplyPaint : false)
                    || context.heights != null
                    || context.holes != null
                    || context.detailLayer != null
                    || (context.detailLayers != null && context.detailLayers.Length > 0);

                if (!shouldApply) return;

                foreach (var kind in appliers)
                {
                    if (!operationAppliers.TryGetValue(kind, out var applier))
                    {
                        continue;
                    }

                    if (kind == OperationKind.Paint && !canApplyPaint)
                    {
                        continue;
                    }

                    if ((kind == OperationKind.Hole || kind == OperationKind.Fill) && context.holes == null)
                    {
                        continue;
                    }

                    if ((kind == OperationKind.DetailAdd || kind == OperationKind.DetailRemove)
                        && context.detailLayer == null
                        && (context.detailLayers == null || context.detailLayers.Length == 0))
                    {
                        continue;
                    }

                    if (kind == OperationKind.DetailAdd)
                    {
                        context.detailMode = DetailOperationMode.Add;
                    }
                    else if (kind == OperationKind.DetailRemove)
                    {
                        context.detailMode = DetailOperationMode.Remove;
                    }

                    applier.Apply(terrain, cache, 0, writePriority, context);
                }
            }
        }

        /// <summary>
        /// Helper method to copy heights from source to destination
        /// </summary>
        static void CopyHeights(float[,] src, float[,] dst)
        {
            int h = src.GetLength(0);
            int w = src.GetLength(1);
            Buffer.BlockCopy(src, 0, dst, 0, sizeof(float) * w * h);
        }

        static void CopyHoles(bool[,] src, bool[,] dst)
        {
            int h = src.GetLength(0);
            int w = src.GetLength(1);
            Buffer.BlockCopy(src, 0, dst, 0, sizeof(bool) * w * h);
        }

        static void CopyAlphamaps(float[,,] src, float[,,] dst)
        {
            int h = src.GetLength(0);
            int w = src.GetLength(1);
            int l = src.GetLength(2);
            Buffer.BlockCopy(src, 0, dst, 0, sizeof(float) * w * h * l);
        }
    }
}
