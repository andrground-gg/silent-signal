using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Splines;

namespace GruelTerraSplines
{
    public partial class TerraSplinesWindow
    {
        void OnEditorUpdate()
        {
            // Don't run updates while in play mode
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            // New update-on-change pipeline: do almost nothing per-frame unless something is actually dirty.
            if (updatesPaused)
            {
                if (!manualUpdateRequested) return;
                manualUpdateRequested = false;

                SyncSplineHierarchyForManualUpdate();

                baselineDirty = true;
                previewDirty = true;
                applyDirty = true;
                previewTexturesNeedUpdate = true;
                heightRangeDirty = true;
                heightRangeNeedsUpdate = true;

                RunPipelineIfDirty();
                return;
            }

            if (updatePendingWhilePaused)
            {
                updatePendingWhilePaused = false;
                RequestPipelineUpdate();
            }

            if (hierarchyRefreshPendingWhilePaused)
            {
                hierarchyRefreshPendingWhilePaused = false;
                RequestHierarchyRefresh();
            }

            // Throttled palette check (rare, but must keep UI palettes consistent).
            double now = EditorApplication.timeSinceStartup;
            if (now - lastTerrainPaletteCheckTime >= TERRAIN_PALETTE_CHECK_INTERVAL_SECONDS)
            {
                lastTerrainPaletteCheckTime = now;
                ValidateAndResetReferences();
                var terrains = GetAllTerrains();
                if (terrains.Count > 0)
                {
                    CheckTerrainLayerPaletteUpdates(terrains);
                }
            }

            CheckForSplinePropertyChanges();

            // External edits support: reuse the existing updateInterval cadence to periodically
            // reapply all splines so changes made via Unity's Terrain tools are incorporated.
            if (supportOutsideChanges && !updatesPaused)
            {
                // lastPipelineRunTime is updated when the pipeline actually runs.
                // If nothing is dirty, force one full pass at the configured interval.
                double intervalSeconds = Mathf.Max(0.02f, updateInterval);
                if ((now - lastPipelineRunTime) >= intervalSeconds)
                {
                    // Avoid scheduling work if there's nothing to apply.
                    if (HasActiveSplines())
                    {
                        MarkPipelineDirty(baseline: true, preview: true, apply: true, heightRange: true);
                    }
                }
            }

            // Live update while actively editing a selected spline (fallback when Undo callbacks don't fire per-drag).
            if (TerraSplinesTool.IsSplineEditModeActive())
            {
                var selectedGO = UnityEditor.Selection.activeGameObject;
                var selectedContainer = selectedGO != null ? selectedGO.GetComponent<SplineContainer>() : null;
                if (selectedContainer != null &&
                    splineGroup != null &&
                    selectedContainer.transform != null &&
                    selectedContainer.transform.IsChildOf(splineGroup) &&
                    selectedContainer.gameObject.activeInHierarchy)
                {
                    int currentEditHash = TerraSplinesTool.GetSplineVersion(selectedContainer);
                    if (currentEditHash != lastSelectedSplineEditHash)
                    {
                        if (now - lastSelectedSplineEditUpdateTime >= SPLINE_EDIT_MIN_UPDATE_INTERVAL_SECONDS)
                        {
                            lastSelectedSplineEditUpdateTime = now;
                            lastSelectedSplineEditHash = currentEditHash;
                            MarkPipelineDirty(preview: true, apply: true, heightRange: true);
                        }
                    }
                }
            }

            if (baselineDirty || previewDirty || applyDirty || previewTexturesNeedUpdate || heightRangeDirty || heightRangeNeedsUpdate)
            {
                RequestPipelineUpdate();
            }

            return;
#if false
        
        void CheckTerrainLayerPaletteUpdates(List<Terrain> terrains)
        {
            bool paletteChanged = false;

            // Detect changes to paint layers and refresh global palette
            int paintHash = ComputePaintLayerHash(terrains);
            if (paintHash != lastTerrainLayersHash)
            {
                lastTerrainLayersHash = paintHash;
                PopulateLayerPalette();
                paletteChanged = true;
            }
            
            // Detect changes to detail prototypes and refresh global palette
            int detailHash = ComputeDetailPrototypeHash(terrains);
            if (detailHash != lastDetailPrototypeHash)
            {
                lastDetailPrototypeHash = detailHash;
                PopulateDetailLayerPalette();
                paletteChanged = true;
            }

            // If terrain layer layout changed, refresh spline list so per-spline palettes update
            if (paletteChanged)
            {
                RefreshChildren();
            }
        }

        void SyncSplineHierarchyForManualUpdate()
        {
            hierarchyRefreshPendingWhilePaused = false;
            hierarchyRefreshScheduled = false;

            ValidateAndResetReferences();
            UpdateTargetsWarning();

            int currentStructureHash = ComputeSplineGroupStructureHash();
            if (currentStructureHash == lastSplineGroupStructureHash)
                return;

            lastSplineGroupStructureHash = currentStructureHash;
            RefreshChildren();
        }
        
        int ComputePaintLayerHash(List<Terrain> terrains)
        {
            unchecked
            {
                int hash = 17;
                foreach (var terrain in terrains)
                {
                    var layers = terrain?.terrainData?.terrainLayers;
                    if (layers == null)
                    {
                        hash = hash * 31 + 1;
                        continue;
                    }
                    
                    hash = hash * 31 + layers.Length;
                    for (int i = 0; i < layers.Length; i++)
                    {
                        var layer = layers[i];
                        hash = hash * 31 + (layer != null ? layer.GetInstanceID() : 0);
                        hash = hash * 31 + (layer != null && layer.name != null ? layer.name.GetHashCode() : 0);
                    }
                }
                
                return hash;
            }
        }
        
        int ComputeDetailPrototypeHash(List<Terrain> terrains)
        {
            unchecked
            {
                int hash = 23;
                foreach (var terrain in terrains)
                {
                    var detailPrototypes = terrain?.terrainData?.detailPrototypes;
                    if (detailPrototypes == null)
                    {
                        hash = hash * 31 + 1;
                        continue;
                    }
                    
                    hash = hash * 31 + detailPrototypes.Length;
                    for (int i = 0; i < detailPrototypes.Length; i++)
                    {
                        var proto = detailPrototypes[i];
                        hash = hash * 31 + (proto?.prototype != null ? proto.prototype.GetInstanceID() : 0);
                        hash = hash * 31 + (proto?.prototypeTexture != null ? proto.prototypeTexture.GetInstanceID() : 0);
                        hash = hash * 31 + (proto?.prototype != null && proto.prototype.name != null ? proto.prototype.name.GetHashCode() : 0);
                    }
                }
                
                return hash;
            }
        }
            if (updatesPaused)
            {
                if (!manualUpdateRequested)
                {
                    return;
                }

                // Manual single update pass regardless of interval
                var manualStopwatch = System.Diagnostics.Stopwatch.StartNew();
                Debug.Log($"{LogPrefix} Manual update execution started...");

                bool hasActiveSplinesManual = HasActiveSplines();
                if (!hasActiveSplinesManual)
                {
                    if (isPreviewAppliedToTerrain)
                    {
                        // Restore baseline for all terrains
                        foreach (var terrain in terrains)
                        {
                            var state = terrainStateManager?.TerrainStates?.GetValueOrDefault(terrain);
                            if (state != null && state.BaselineHeights != null)
                            {
                                TerraSplinesTool.ApplyPreviewToTerrain(terrain, state.BaselineHeights, state.BaselineHoles, state.BaselineAlphamaps);

                                if (state.BaselineDetailLayers != null)
                                {
                                    TerraSplinesTool.ApplyDetailLayersToTerrain(terrain, state.BaselineDetailLayers);
                                }
                            }
                        }
                        isPreviewAppliedToTerrain = false;
                    }

                    manualUpdateRequested = false;
                    lastUpdate = DateTime.Now;
                    UpdateUIState();
                    manualStopwatch.Stop();
                    Debug.Log($"{LogPrefix} Manual update completed in {manualStopwatch.ElapsedMilliseconds}ms ({manualStopwatch.Elapsed.TotalSeconds:F3}s) - No active splines");
                    return;
                }

                EnsureBaseline();
                // Update baseline for all terrains via TerrainStateManager
                var baselineSwManual = System.Diagnostics.Stopwatch.StartNew();
                terrainStateManager?.UpdateBaseline(suppressBaselineUpdate);
                baselineSwManual.Stop();
                updateBaselineTimeMs = baselineSwManual.Elapsed.TotalMilliseconds;
                
                // Sync first terrain's state to window fields for backward compatibility
                var firstTerrainManual = terrains[0];
                var firstStateManual = terrainStateManager?.TerrainStates?.GetValueOrDefault(firstTerrainManual);
                if (firstStateManual != null)
                {
                    baselineHeights = firstStateManual.BaselineHeights;
                    workingHeights = firstStateManual.WorkingHeights;
                    baselineHoles = firstStateManual.BaselineHoles;
                    workingHoles = firstStateManual.WorkingHoles;
                    baselineAlphamaps = firstStateManual.BaselineAlphamaps;
                    workingAlphamaps = firstStateManual.WorkingAlphamaps;
                    paintSplineAlphaMask = firstStateManual.PaintSplineAlphaMask;
                    holeSplineMask = firstStateManual.HoleSplineMask;
                    
                    // Update combined baseline texture
                    UpdateCombinedBaselineTexture();
                    heightRangeNeedsUpdate = true;
                }

                RebuildPreview(applyToTerrain: false);
                // Apply preview to all terrains
                var applySwManual = System.Diagnostics.Stopwatch.StartNew();
                foreach (var terrain in terrains)
                {
                    var state = terrainStateManager?.TerrainStates?.GetValueOrDefault(terrain);
                    if (state != null && state.WorkingHeights != null)
                    {
                        TerraSplinesTool.ApplyPreviewToTerrain(terrain, state.WorkingHeights, state.WorkingHoles, state.WorkingAlphamaps);
                        isPreviewAppliedToTerrain = true;
                    }
                }
                applySwManual.Stop();
                applyPreviewTimeMs = applySwManual.Elapsed.TotalMilliseconds;

                // Update preview textures after RebuildPreview() so cache is populated
                var previewTexturesSwManual = System.Diagnostics.Stopwatch.StartNew();
                if (previewTexturesNeedUpdate)
                {
                    UpdatePreviewTextures();
                    previewTexturesNeedUpdate = false;
                }
                previewTexturesSwManual.Stop();
                updatePreviewTexturesTimeMs = previewTexturesSwManual.Elapsed.TotalMilliseconds;

                // Update preview texture UI elements without full UI refresh
                UpdatePreviewTexturesUI();
                var heightRangeSwManual = System.Diagnostics.Stopwatch.StartNew();
                UpdateHeightRangeDisplay(forceRecalculate: true);
                heightRangeSwManual.Stop();
                updateHeightRangeTimeMs = heightRangeSwManual.Elapsed.TotalMilliseconds;

                manualUpdateRequested = false;
                lastUpdate = DateTime.Now;

                manualStopwatch.Stop();
                Debug.Log($"{LogPrefix} Manual update completed in {manualStopwatch.ElapsedMilliseconds}ms ({manualStopwatch.Elapsed.TotalSeconds:F3}s)");
                return;
            }

            // While actively editing spline knots/handles, update terrain preview quickly even if UpdateInterval is large.
            bool inSplineEditMode = TerraSplinesTool.IsSplineEditModeActive();
            bool forceSplineEditUpdate = false;
            if (inSplineEditMode)
            {
                var selectedGO = UnityEditor.Selection.activeGameObject;
                var selectedContainer = selectedGO != null ? selectedGO.GetComponent<SplineContainer>() : null;
                if (selectedContainer != null)
                {
                    int currentEditHash = TerraSplinesTool.GetSplineVersion(selectedContainer);
                    if (currentEditHash != lastSelectedSplineEditHash)
                    {
                        double now = EditorApplication.timeSinceStartup;
                        if (now - lastSelectedSplineEditUpdateTime >= SPLINE_EDIT_MIN_UPDATE_INTERVAL_SECONDS)
                        {
                            lastSelectedSplineEditUpdateTime = now;
                            lastSelectedSplineEditHash = currentEditHash;
                            forceSplineEditUpdate = true;
                        }
                    }
                    else if (selectedContainer.gameObject.activeInHierarchy && !supportOutsideChanges && !previewTexturesNeedUpdate && !manualUpdateRequested)
                    {
                        // Spline tool active but no edits: avoid background terrain updates while navigating the scene.
                        return;
                    }
                }
            }

            if (!forceSplineEditUpdate &&
                (DateTime.Now - lastUpdate).TotalSeconds < Mathf.Max(0.02f, updateInterval))
            {
                return;
            }
            lastUpdate = DateTime.Now;

            // Start timing automatic update interval
            var autoStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Terrains already retrieved at method start

            // Handle ObjectPicker results
#if UNITY_EDITOR
            if (UnityEditor.EditorGUIUtility.GetObjectPickerControlID() == 0)
            {
                var selectedObject = UnityEditor.EditorGUIUtility.GetObjectPickerObject();
                if (selectedObject is Texture2D selectedTexture)
                {
                    // Update the global brush
                    globalBrush = selectedTexture;
                    if (globalBrushPreviewImage != null)
                        globalBrushPreviewImage.image = selectedTexture;
                }
            }
#endif

            bool hasActiveSplines = HasActiveSplines();
            if (!hasActiveSplines)
            {
                if (isPreviewAppliedToTerrain)
                {
                    // Restore baseline for all terrains
                    foreach (var terrain in terrains)
                    {
                        var state = terrainStateManager?.TerrainStates?.GetValueOrDefault(terrain);
                        if (state != null && state.BaselineHeights != null)
                        {
                            TerraSplinesTool.ApplyPreviewToTerrain(terrain, state.BaselineHeights, state.BaselineHoles, state.BaselineAlphamaps);

                            if (state.BaselineDetailLayers != null)
                            {
                                TerraSplinesTool.ApplyDetailLayersToTerrain(terrain, state.BaselineDetailLayers);
                            }
                        }
                    }
                    isPreviewAppliedToTerrain = false;
                }

                UpdateHeightRangeDisplay(forceRecalculate: true);

                autoStopwatch.Stop();
                return;
            }

            EnsureBaseline();

            // Update baseline for all terrains via TerrainStateManager
            var baselineSw = System.Diagnostics.Stopwatch.StartNew();
            terrainStateManager?.UpdateBaseline(suppressBaselineUpdate);
            baselineSw.Stop();
            updateBaselineTimeMs = baselineSw.Elapsed.TotalMilliseconds;
            
            // Sync first terrain's state to window fields for backward compatibility
            var firstTerrain = terrains[0];
            var firstState = terrainStateManager?.TerrainStates?.GetValueOrDefault(firstTerrain);
            if (firstState != null)
            {
                baselineHeights = firstState.BaselineHeights;
                workingHeights = firstState.WorkingHeights;
                baselineHoles = firstState.BaselineHoles;
                workingHoles = firstState.WorkingHoles;
                baselineAlphamaps = firstState.BaselineAlphamaps;
                workingAlphamaps = firstState.WorkingAlphamaps;
                paintSplineAlphaMask = firstState.PaintSplineAlphaMask;
                holeSplineMask = firstState.HoleSplineMask;
                
                // Update combined baseline texture
                UpdateCombinedBaselineTexture();
                heightRangeNeedsUpdate = true;
            }

            // Rebuild preview (splines on top of updated baseline)
            RebuildPreview(applyToTerrain: false);

            // Apply preview to all terrains
            var applySw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var terrain in terrains)
            {
                var state = terrainStateManager?.TerrainStates?.GetValueOrDefault(terrain);
                if (state != null && state.WorkingHeights != null)
                {
                    TerraSplinesTool.ApplyPreviewToTerrain(terrain, state.WorkingHeights, state.WorkingHoles, state.WorkingAlphamaps);
                    isPreviewAppliedToTerrain = true;
                }
            }
            applySw.Stop();
            applyPreviewTimeMs = applySw.Elapsed.TotalMilliseconds;

            // Update preview textures after RebuildPreview() so cache is populated
            var previewTexturesSw = System.Diagnostics.Stopwatch.StartNew();
            if (previewTexturesNeedUpdate)
            {
                UpdatePreviewTextures();
                previewTexturesNeedUpdate = false;
            }
            previewTexturesSw.Stop();
            updatePreviewTexturesTimeMs = previewTexturesSw.Elapsed.TotalMilliseconds;

            var heightRangeSw = System.Diagnostics.Stopwatch.StartNew();
            UpdateHeightRangeDisplay();
            heightRangeSw.Stop();
            updateHeightRangeTimeMs = heightRangeSw.Elapsed.TotalMilliseconds;

            autoStopwatch.Stop();
#endif
        }

        void CheckTerrainLayerPaletteUpdates(List<Terrain> terrains)
        {
            bool paletteChanged = false;

            int paintHash = ComputePaintLayerHash(terrains);
            if (paintHash != lastTerrainLayersHash)
            {
                lastTerrainLayersHash = paintHash;
                PopulateLayerPalette();
                paletteChanged = true;
            }

            int detailHash = ComputeDetailPrototypeHash(terrains);
            if (detailHash != lastDetailPrototypeHash)
            {
                lastDetailPrototypeHash = detailHash;
                PopulateDetailLayerPalette();
                paletteChanged = true;
            }

            if (paletteChanged)
            {
                RequestHierarchyRefresh();
            }
        }

        int ComputePaintLayerHash(List<Terrain> terrains)
        {
            unchecked
            {
                int hash = 17;
                foreach (var terrain in terrains)
                {
                    var layers = terrain?.terrainData?.terrainLayers;
                    if (layers == null)
                    {
                        hash = hash * 31 + 1;
                        continue;
                    }

                    hash = hash * 31 + layers.Length;
                    for (int i = 0; i < layers.Length; i++)
                    {
                        var layer = layers[i];
                        hash = hash * 31 + (layer != null ? layer.GetInstanceID() : 0);
                        hash = hash * 31 + (layer != null && layer.name != null ? layer.name.GetHashCode() : 0);
                    }
                }

                return hash;
            }
        }

        int ComputeDetailPrototypeHash(List<Terrain> terrains)
        {
            unchecked
            {
                int hash = 23;
                foreach (var terrain in terrains)
                {
                    var detailPrototypes = terrain?.terrainData?.detailPrototypes;
                    if (detailPrototypes == null)
                    {
                        hash = hash * 31 + 1;
                        continue;
                    }

                    hash = hash * 31 + detailPrototypes.Length;
                    for (int i = 0; i < detailPrototypes.Length; i++)
                    {
                        var proto = detailPrototypes[i];
                        hash = hash * 31 + (proto?.prototype != null ? proto.prototype.GetInstanceID() : 0);
                        hash = hash * 31 + (proto?.prototypeTexture != null ? proto.prototypeTexture.GetInstanceID() : 0);
                        hash = hash * 31 + (proto?.prototype != null && proto.prototype.name != null ? proto.prototype.name.GetHashCode() : 0);
                    }
                }

                return hash;
            }
        }

        void SyncSplineHierarchyForManualUpdate()
        {
            hierarchyRefreshPendingWhilePaused = false;
            hierarchyRefreshScheduled = false;

            ValidateAndResetReferences();
            UpdateTargetsWarning();

            int currentStructureHash = ComputeSplineGroupStructureHash();
            if (currentStructureHash == lastSplineGroupStructureHash)
                return;

            lastSplineGroupStructureHash = currentStructureHash;
            RefreshChildren();
        }

        void OnHierarchyChanged()
        {
            RequestHierarchyRefresh();
        }

        void RequestHierarchyRefresh()
        {
            lastHierarchyChangedTime = EditorApplication.timeSinceStartup;

            if (updatesPaused)
            {
                hierarchyRefreshPendingWhilePaused = true;
                return;
            }

            if (hierarchyRefreshScheduled) return;
            hierarchyRefreshScheduled = true;
            EditorApplication.delayCall += ProcessHierarchyRefreshDebounced;
        }

        void ProcessHierarchyRefreshDebounced()
        {
            if (this == null)
            {
                hierarchyRefreshScheduled = false;
                return;
            }

            if (IsInteractiveRefreshDeferred())
            {
                EditorApplication.delayCall += ProcessHierarchyRefreshDebounced;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - lastHierarchyChangedTime < HIERARCHY_REFRESH_DEBOUNCE_SECONDS)
            {
                EditorApplication.delayCall += ProcessHierarchyRefreshDebounced;
                return;
            }

            hierarchyRefreshScheduled = false;

            if (updatesPaused)
            {
                hierarchyRefreshPendingWhilePaused = true;
                return;
            }

            ValidateAndResetReferences();
            UpdateTargetsWarning();

            int currentStructureHash = ComputeSplineGroupStructureHash();
            if (currentStructureHash == lastSplineGroupStructureHash)
            {
                return;
            }

            lastSplineGroupStructureHash = currentStructureHash;
            RefreshChildren();
        }

        int ComputeSplineGroupStructureHash()
        {
            unchecked
            {
                int hash = 17;

                if (splineGroup == null || splineGroup.gameObject == null)
                {
                    return hash;
                }

                hash = hash * 31 + splineGroup.GetInstanceID();
                hash = hash * 31 + splineGroup.childCount;

                for (int i = 0; i < splineGroup.childCount; i++)
                {
                    var child = splineGroup.GetChild(i);
                    if (child == null) continue;

                    hash = hash * 31 + child.GetInstanceID();
                    hash = hash * 31 + child.GetSiblingIndex();
                    hash = hash * 31 + child.childCount;
                    hash = hash * 31 + (child.GetComponent<SplineContainer>() != null ? 1 : 0);
                    hash = hash * 31 + (child.GetComponentInChildren<SplineContainer>(true) != null ? 1 : 0);
                }

                return hash;
            }
        }

        void OnSceneChanged(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
        {
            // Recreate all strategies to ensure fresh state after scene changes
            TerraSplinesTool.RecreateStrategies();

            // Reset window state
            lastHadActiveSplines = false;
            baselineHeights = null;
            needsFreshBaseline = true;
            heightRangeNeedsUpdate = true;

            // Clear spline items to force refresh
            splineItems.Clear();
            RebuildListView();
            UpdateSplineCountLabel();

            // Validate and reset references if they became invalid
            ValidateAndResetReferences();

            // Update warning indicator after scene change
            UpdateTargetsWarning();
        }

        void ValidateAndResetReferences()
        {
            bool referencesChanged = false;

            // Check if terrain group reference is still valid
            if (targetTerrainGroup != null && targetTerrainGroup.gameObject == null)
            {
                Debug.Log($"ValidateAndResetReferences - Clearing invalid terrain group");
                targetTerrainGroup = null;
                if (terrainField != null)
                {
                    terrainField.value = null;
                    terrainField.MarkDirtyRepaint();
                }

                referencesChanged = true;
            }
            
            // Legacy: Check if legacy terrain reference is still valid
            if (targetTerrain != null && targetTerrain.gameObject == null)
            {
                Debug.Log($"ValidateAndResetReferences - Clearing invalid legacy terrain: {targetTerrain.name}");
                targetTerrain = null;
                referencesChanged = true;
            }

            // Check if spline group reference is still valid
            if (splineGroup != null && splineGroup.gameObject == null)
            {
                splineGroup = null;
                if (splineGroupField != null)
                {
                    splineGroupField.value = null;
                    splineGroupField.MarkDirtyRepaint();
                }

                referencesChanged = true;
            }

            // Reset UI state when references become invalid
            var terrains = GetAllTerrains();
            if (terrains.Count == 0)
            {
                baselineHeights = null;
                workingHeights = null;
                originalHeights = null;
                needsFreshBaseline = true;
                heightRangeNeedsUpdate = true;
                ClearUndoRedoStacks();
            }

            // If references changed, force UI refresh
            if (referencesChanged)
            {
                RefreshUIAfterReferenceChange();
            }

            // Update warning indicator based on current reference state
            UpdateTargetsWarning();
        }

        void RefreshUIAfterReferenceChange()
        {
            // Force refresh of all UI elements
            if (splinesListView != null)
            {
                splinesListView.RefreshItems();
                splinesListView.MarkDirtyRepaint();
            }

            // Force refresh of ObjectField elements
            if (terrainField != null)
            {
                terrainField.MarkDirtyRepaint();
            }

            if (splineGroupField != null)
            {
                splineGroupField.MarkDirtyRepaint();
            }

            // Update UI state (but don't call ValidateAndResetReferences here to avoid clearing just-assigned references)
            UpdateUIStateWithoutValidation();

            // Force repaint of the entire window
            Repaint();

            // Schedule another refresh after a short delay to ensure UI updates
            EditorApplication.delayCall += () =>
            {
                Repaint();
                if (splinesListView != null)
                {
                    splinesListView.RefreshItems();
                }
            };
        }

        void AutoDetectTerrainAndSplineGroup()
        {
            // Auto-detect terrain group if not set
            if (targetTerrainGroup == null)
            {
                // First try to migrate from legacy targetTerrain
                if (targetTerrain != null)
                {
                    if (targetTerrain.transform.parent != null)
                    {
                        targetTerrainGroup = targetTerrain.transform.parent.gameObject;
                    }
                    else
                    {
                        var groupGO = new GameObject("Terrain Group");
                        targetTerrain.transform.SetParent(groupGO.transform);
                        targetTerrainGroup = groupGO;
                    }
                    targetTerrain = null;
                }
                else
                {
                    // Find first Terrain and use its parent or create a group
                    var terrain = FindObjectOfType<Terrain>();
                    if (terrain != null)
                    {
                        if (terrain.transform.parent != null)
                        {
                            targetTerrainGroup = terrain.transform.parent.gameObject;
                        }
                        else
                        {
                            var groupGO = new GameObject("Terrain Group");
                            terrain.transform.SetParent(groupGO.transform);
                            targetTerrainGroup = groupGO;
                        }
                    }
                }
            }

            // Auto-detect spline group if not set
            if (splineGroup == null)
            {
                // Find first SplineContainer in hierarchy
                SplineContainer[] splineContainers = FindObjectsOfType<SplineContainer>();
                if (splineContainers.Length > 0)
                {
                    // Get the parent of the first SplineContainer
                    Transform parent = splineContainers[0].transform.root;
                    if (parent != null)
                    {
                        splineGroup = parent;
                        // Refresh children to populate the splineItems list
                        // Always refresh when auto-detecting spline group (initial setup)
                        RefreshChildren();
                    }
                }
            }
        }

        public Transform GetOrCreateSplineGroup()
        {
            if (splineGroup != null && splineGroup.gameObject != null)
                return splineGroup;

            var groupObject = new GameObject("Spline Group");
            Undo.RegisterCreatedObjectUndo(groupObject, "Create Spline Group");
            splineGroup = groupObject.transform;

            if (splineGroupField != null)
            {
                splineGroupField.value = splineGroup;
                splineGroupField.MarkDirtyRepaint();
            }

            RefreshChildren();
            return splineGroup;
        }

        public Transform GetSplineGroup()
        {
            if (splineGroup == null || splineGroup.gameObject == null)
                return null;

            return splineGroup;
        }

        public void SetSplineGroupExternal(Transform group, bool refreshList = true)
        {
            splineGroup = group != null && group.gameObject != null ? group : null;

            if (splineGroupField != null)
            {
                splineGroupField.value = splineGroup;
                splineGroupField.MarkDirtyRepaint();
            }

            if (refreshList)
                RefreshChildren();
        }

        void RestoreBaselineIfAny()
        {
            // Restore baseline for all terrains
            var terrains = GetAllTerrains();
            foreach (var terrain in terrains)
            {
                if (terrain == null) continue;
                var state = terrainStateManager?.TerrainStates?.GetValueOrDefault(terrain);
                if (state != null && state.BaselineHeights != null)
                {
                    TerraSplinesTool.ApplyPreviewToTerrain(terrain, state.BaselineHeights, state.BaselineHoles, state.BaselineAlphamaps);

                    if (state.BaselineDetailLayers != null)
                    {
                        TerraSplinesTool.ApplyDetailLayersToTerrain(terrain, state.BaselineDetailLayers);
                    }
                }
            }
        }

        string GetHierarchyPath(Transform splineTransform, Transform rootTransform)
        {
            if (splineTransform == null || rootTransform == null) return "Unknown";

            var pathParts = new List<string>();
            Transform current = splineTransform;

            // Walk up the hierarchy until we reach the root spline group
            while (current != null && current != rootTransform)
            {
                pathParts.Add(current.name);
                current = current.parent;
            }

            // Reverse the list to get the correct order (root to leaf)
            pathParts.Reverse();

            // Join with "/" separator
            return string.Join("/", pathParts);
        }

        (float min, float max) GetHeightmapRange(float[,] heights, Terrain terrain)
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

        void CheckForSplinePropertyChanges()
        {
            if (updatesPaused) return;

            // Throttle: only check every PROPERTY_CHECK_INTERVAL seconds
            if ((DateTime.Now - lastPropertyCheckTime).TotalSeconds < PROPERTY_CHECK_INTERVAL)
            {
                return;
            }

            lastPropertyCheckTime = DateTime.Now;

            // Check warning indicator periodically (even if splineGroup is null)
            if (splinesListView != null)
            {
                UpdateTargetsWarning();
            }

            // Only check spline properties if ListView and splineGroup exist
            if (splinesListView == null || splineGroup == null) return;

            bool needsRefresh = false;

            // Get all spline items
            var allSplines = GetAllSplineItems();

            foreach (var splineItem in allSplines)
            {
                if (splineItem.container == null) continue;

                // Track only structural property changes (knot count / closed state), not continuous knot drags.
                int currentVersion = GetSplinePropertyVersion(splineItem.container);

                // Check if we've seen this version before
                if (lastSplineVersions.TryGetValue(splineItem.container, out int lastVersion))
                {
                    if (currentVersion != lastVersion)
                    {
                        // Version changed - spline property was modified
                        needsRefresh = true;
                        lastSplineVersions[splineItem.container] = currentVersion;
                        // Clear preview texture cache key so preview will regenerate for structural changes
                        previewTextureCacheKeys.Remove(splineItem.container);
                    }
                }
                else
                {
                    // First time seeing this spline - record its version
                    lastSplineVersions[splineItem.container] = currentVersion;
                }
            }

            // Remove entries for splines that no longer exist
            var containersToRemove = new List<SplineContainer>();
            foreach (var kvp in lastSplineVersions)
            {
                if (kvp.Key == null || !allSplines.Any(s => s.container == kvp.Key))
                {
                    containersToRemove.Add(kvp.Key);
                }
            }

            foreach (var container in containersToRemove)
            {
                lastSplineVersions.Remove(container);
                // Also clean up preview texture cache and settings tracking
                previewTextureCacheKeys.Remove(container);
                lastSplineSettings.Remove(container);
            }

            // Refresh ListView if needed
            if (needsRefresh && splinesListView != null)
            {
                splinesListView.RefreshItems();
            }
        }

        int GetSplinePropertyVersion(SplineContainer container)
        {
            if (container == null) return 0;

            unchecked
            {
                int version = 17;
                foreach (var spline in container.Splines)
                {
                    version = version * 31 + spline.Count;
                    version = version * 31 + (spline.Closed ? 1 : 0);
                }
                return version;
            }
        }
    }
}
