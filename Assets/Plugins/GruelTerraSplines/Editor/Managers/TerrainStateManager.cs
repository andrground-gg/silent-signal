using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace GruelTerraSplines.Managers
{
    /// <summary>
    /// Manages terrain state including baseline, working, and original data.
    /// Handles terrain data operations like ensuring baseline, updating baseline, and undo/redo.
    /// Now supports multiple terrains via Dictionary-based storage.
    /// </summary>
    public class TerrainStateManager : IWindowManager
    {
        private EditorWindow window;
        private IBaselineUpdateStrategy baselineUpdateStrategy;
        
        // Multi-terrain state storage
        private Dictionary<Terrain, TerrainState> terrainStates = new Dictionary<Terrain, TerrainState>();
        
        // Backward compatibility: Access first terrain's state (for single-terrain workflows)
        public Terrain TargetTerrain => terrainStates.Count > 0 ? terrainStates.Keys.First() : null;
        
        // Backward compatibility properties (access first terrain's state)
        public float[,] OriginalHeights => GetFirstTerrainState()?.OriginalHeights;
        public float[,] BaselineHeights => GetFirstTerrainState()?.BaselineHeights;
        public float[,] WorkingHeights => GetFirstTerrainState()?.WorkingHeights;
        public bool[,] OriginalHoles => GetFirstTerrainState()?.OriginalHoles;
        public bool[,] BaselineHoles => GetFirstTerrainState()?.BaselineHoles;
        public bool[,] WorkingHoles => GetFirstTerrainState()?.WorkingHoles;
        public float[,,] OriginalAlphamaps => GetFirstTerrainState()?.OriginalAlphamaps;
        public float[,,] BaselineAlphamaps => GetFirstTerrainState()?.BaselineAlphamaps;
        public float[,,] WorkingAlphamaps => GetFirstTerrainState()?.WorkingAlphamaps;
        public Texture2D BaselineTexture => GetFirstTerrainState()?.BaselineTexture;
        public Texture2D PreviewTexture => GetFirstTerrainState()?.PreviewTexture;
        public float[,] UndoBuffer => GetFirstTerrainState()?.UndoBuffer;
        public bool[,] UndoHolesBuffer => GetFirstTerrainState()?.UndoHolesBuffer;
        public float[,,] UndoAlphamapsBuffer => GetFirstTerrainState()?.UndoAlphamapsBuffer;
        public float[,] PaintSplineAlphaMask => GetFirstTerrainState()?.PaintSplineAlphaMask;
        public bool[,] HoleSplineMask => GetFirstTerrainState()?.HoleSplineMask;
        public bool CanUndo => terrainStates.Values.Any(s => s.CanUndo);
        public bool CanRedo => terrainStates.Values.Any(s => s.CanRedo);
        public bool HeightRangeNeedsUpdate { get; set; }
        
        // Multi-terrain access
        public Dictionary<Terrain, TerrainState> TerrainStates => terrainStates;
        public List<Terrain> TargetTerrains => terrainStates.Keys.ToList();
        
        private TerrainState GetFirstTerrainState()
        {
            return terrainStates.Count > 0 ? terrainStates.Values.First() : null;
        }
        
        public TerrainStateManager()
        {
            baselineUpdateStrategy = new StaticBaselineUpdateStrategy();
        }
        
        public void Initialize(EditorWindow window)
        {
            this.window = window;
        }
        
        public void OnDestroy()
        {
            // Cleanup all terrain states
            foreach (var state in terrainStates.Values)
            {
                if (state.BaselineTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(state.BaselineTexture);
                    state.BaselineTexture = null;
                }
                if (state.PreviewTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(state.PreviewTexture);
                    state.PreviewTexture = null;
                }
            }
            
            terrainStates.Clear();
        }
        
        public void SetTerrain(Terrain terrain)
        {
            // Backward compatibility: convert single terrain to list
            if (terrain != null)
            {
                SetTerrains(new List<Terrain> { terrain });
            }
            else
            {
                SetTerrains(new List<Terrain>());
            }
        }
        
        public void SetTerrains(List<Terrain> terrains)
        {
            // Remove terrains that are no longer in the list
            var toRemove = terrainStates.Keys.Where(t => !terrains.Contains(t)).ToList();
            foreach (var terrain in toRemove)
            {
                var state = terrainStates[terrain];
                if (state.BaselineTexture != null)
                    UnityEngine.Object.DestroyImmediate(state.BaselineTexture);
                if (state.PreviewTexture != null)
                    UnityEngine.Object.DestroyImmediate(state.PreviewTexture);
                terrainStates.Remove(terrain);
            }
            
            // Add or update terrains
            foreach (var terrain in terrains)
            {
                if (terrain == null) continue;
                
                if (!terrainStates.ContainsKey(terrain))
                {
                    terrainStates[terrain] = new TerrainState { Terrain = terrain };
                }
            }
            
            if (terrainStates.Count == 0)
            {
                HeightRangeNeedsUpdate = true;
                return;
            }
            
            // Ensure baseline for all terrains
            foreach (var terrain in terrains)
            {
                if (terrain != null)
                {
                    EnsureBaseline(terrain);
                }
            }
        }
        
        public void SetBaselineUpdateStrategy(IBaselineUpdateStrategy strategy)
        {
            baselineUpdateStrategy = strategy;
        }
        
        public void EnsureBaseline()
        {
            // Backward compatibility: ensure baseline for first terrain
            if (TargetTerrain != null)
            {
                EnsureBaseline(TargetTerrain);
            }
        }
        
        public void EnsureBaseline(Terrain terrain)
        {
            if (terrain == null) return;
            if (!terrainStates.ContainsKey(terrain))
            {
                terrainStates[terrain] = new TerrainState { Terrain = terrain };
            }
            
            var state = terrainStates[terrain];
            var td = terrain.terrainData;

            // Capture original heights on first initialization
            if (state.OriginalHeights == null || state.OriginalHeights.GetLength(0) != td.heightmapResolution)
            {
                state.OriginalHeights = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);
            }

            // Capture original holes on first initialization
            if (state.OriginalHoles == null || state.OriginalHoles.GetLength(0) != td.heightmapResolution)
            {
                var terrainHoles = TerraSplinesTool.GetTerrainHoles(terrain);
                state.OriginalHoles = TerraSplinesTool.ConvertTerrainHolesToHeightmapHoles(terrain, terrainHoles);
            }

            // Capture original alphamaps on first initialization
            if (td.alphamapLayers > 0 && (state.OriginalAlphamaps == null || state.OriginalAlphamaps.GetLength(0) != td.alphamapHeight || state.OriginalAlphamaps.GetLength(1) != td.alphamapWidth))
            {
                state.OriginalAlphamaps = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);
            }

            // Capture original detail layers on first initialization
            var detailPrototypes = td.detailPrototypes;
            int detailLayerCount = detailPrototypes != null ? detailPrototypes.Length : 0;
            if (detailLayerCount > 0 && td.detailWidth > 0 && td.detailHeight > 0)
            {
                bool needsDetailRefresh =
                    state.OriginalDetailLayers == null
                    || state.OriginalDetailLayers.Length != detailLayerCount
                    || state.OriginalDetailLayers[0] == null
                    || state.OriginalDetailLayers[0].GetLength(0) != td.detailHeight
                    || state.OriginalDetailLayers[0].GetLength(1) != td.detailWidth;

                if (needsDetailRefresh)
                {
                    state.OriginalDetailLayers = new int[detailLayerCount][,];
                    for (int layer = 0; layer < detailLayerCount; layer++)
                    {
                        state.OriginalDetailLayers[layer] = td.GetDetailLayer(0, 0, td.detailWidth, td.detailHeight, layer);
                    }
                }
            }

            // Only capture baseline if we don't have one yet
            if (state.BaselineHeights == null || state.BaselineHeights.GetLength(0) != td.heightmapResolution)
            {
                state.BaselineHeights = TerraSplinesTool.CopyHeights(state.OriginalHeights);
                state.WorkingHeights = TerraSplinesTool.CopyHeights(state.BaselineHeights);
                state.BaselineTexture = TerraSplinesTool.HeightsToTexture(terrain, state.BaselineHeights);
                HeightRangeNeedsUpdate = true;
            }

            // Only capture baseline alphamaps if we don't have one yet
            if (td.alphamapLayers > 0)
            {
                // Refresh original alphamaps if layer count changed
                if (state.OriginalAlphamaps == null 
                    || state.OriginalAlphamaps.GetLength(0) != td.alphamapHeight 
                    || state.OriginalAlphamaps.GetLength(1) != td.alphamapWidth
                    || state.OriginalAlphamaps.GetLength(2) != td.alphamapLayers)
                {
                    state.OriginalAlphamaps = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);
                }
                
                if (state.BaselineAlphamaps == null)
                {
                    state.BaselineAlphamaps = TerraSplinesTool.CopyAlphamaps(state.OriginalAlphamaps);
                    state.WorkingAlphamaps = TerraSplinesTool.CopyAlphamaps(state.BaselineAlphamaps);
                }
                else if (state.BaselineAlphamaps.GetLength(0) != td.alphamapHeight 
                    || state.BaselineAlphamaps.GetLength(1) != td.alphamapWidth
                    || state.BaselineAlphamaps.GetLength(2) != td.alphamapLayers)
                {
                    // Refresh from terrain if dimensions or layer count changed
                    state.BaselineAlphamaps = TerraSplinesTool.CopyAlphamaps(state.OriginalAlphamaps);
                    state.WorkingAlphamaps = TerraSplinesTool.CopyAlphamaps(state.BaselineAlphamaps);
                }
            }

            // Initialize holes arrays if needed
            if (state.BaselineHoles == null || state.BaselineHoles.GetLength(0) != td.heightmapResolution)
            {
                state.BaselineHoles = TerraSplinesTool.CopyHoles(state.OriginalHoles);
                state.WorkingHoles = TerraSplinesTool.CopyHoles(state.BaselineHoles);
            }

            // Initialize detail layers if needed
            if (detailLayerCount > 0 && td.detailWidth > 0 && td.detailHeight > 0)
            {
                bool needsBaselineDetails =
                    state.BaselineDetailLayers == null
                    || state.BaselineDetailLayers.Length != detailLayerCount
                    || state.BaselineDetailLayers[0] == null
                    || state.BaselineDetailLayers[0].GetLength(0) != td.detailHeight
                    || state.BaselineDetailLayers[0].GetLength(1) != td.detailWidth;

                if (needsBaselineDetails && state.OriginalDetailLayers != null)
                {
                    state.BaselineDetailLayers = TerraSplinesTool.CopyDetailLayers(state.OriginalDetailLayers);
                    state.WorkingDetailLayers = TerraSplinesTool.CopyDetailLayers(state.BaselineDetailLayers);
                }
                else if (state.WorkingDetailLayers == null && state.BaselineDetailLayers != null)
                {
                    state.WorkingDetailLayers = TerraSplinesTool.CopyDetailLayers(state.BaselineDetailLayers);
                }
            }

            // Initialize masks if needed
            if (state.PaintSplineAlphaMask == null || state.PaintSplineAlphaMask.GetLength(0) != td.heightmapResolution)
            {
                state.PaintSplineAlphaMask = new float[td.heightmapResolution, td.heightmapResolution];
            }
            if (state.HoleSplineMask == null || state.HoleSplineMask.GetLength(0) != td.heightmapResolution)
            {
                state.HoleSplineMask = new bool[td.heightmapResolution, td.heightmapResolution];
            }

            // Recreate baseline texture if it has the wrong format (R8 instead of RGBA32)
            if (state.BaselineTexture != null && state.BaselineTexture.format != TextureFormat.RGBA32)
            {
                UnityEngine.Object.DestroyImmediate(state.BaselineTexture);
                state.BaselineTexture = TerraSplinesTool.HeightsToTexture(terrain, state.BaselineHeights);
            }

            // Initialize preview texture if needed
            if (state.PreviewTexture == null || state.PreviewTexture.width != td.heightmapResolution)
            {
                state.PreviewTexture = new Texture2D(td.heightmapResolution, td.heightmapResolution, TextureFormat.RGBA32, false, true);
                state.PreviewTexture.wrapMode = TextureWrapMode.Clamp;
            }
        }
        
        public void RefreshBaselineFromTerrain()
        {
            // Refresh baseline for all terrains
            foreach (var kvp in terrainStates)
            {
                var terrain = kvp.Key;
                var state = kvp.Value;
                if (terrain == null) continue;

                var td = terrain.terrainData;
                if (td == null) continue;

                // Force refresh baseline heights from current terrain state
                state.BaselineHeights = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);
                state.BaselineTexture = TerraSplinesTool.HeightsToTexture(terrain, state.BaselineHeights);

                // Force refresh baseline holes from current terrain state
                var terrainHoles = TerraSplinesTool.GetTerrainHoles(terrain);
                state.BaselineHoles = TerraSplinesTool.ConvertTerrainHolesToHeightmapHoles(terrain, terrainHoles);

                // Force refresh baseline alphamaps from current terrain state
                if (td.alphamapLayers > 0)
                {
                    state.BaselineAlphamaps = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);
                }

                // Force refresh baseline detail layers from current terrain state
                var detailPrototypes = td.detailPrototypes;
                int detailLayerCount = detailPrototypes != null ? detailPrototypes.Length : 0;
                if (detailLayerCount > 0 && td.detailWidth > 0 && td.detailHeight > 0)
                {
                    state.BaselineDetailLayers = new int[detailLayerCount][,];
                    for (int layer = 0; layer < detailLayerCount; layer++)
                    {
                        state.BaselineDetailLayers[layer] = td.GetDetailLayer(0, 0, td.detailWidth, td.detailHeight, layer);
                    }
                }
                else
                {
                    state.BaselineDetailLayers = null;
                }

                // Update working arrays to match baseline
                state.WorkingHeights = TerraSplinesTool.CopyHeights(state.BaselineHeights);
                state.WorkingHoles = TerraSplinesTool.CopyHoles(state.BaselineHoles);
                if (state.BaselineAlphamaps != null)
                {
                    state.WorkingAlphamaps = TerraSplinesTool.CopyAlphamaps(state.BaselineAlphamaps);
                }

                if (state.BaselineDetailLayers != null)
                {
                    state.WorkingDetailLayers = TerraSplinesTool.CopyDetailLayers(state.BaselineDetailLayers);
                }
            }

            HeightRangeNeedsUpdate = true;
        }
        
        public void UpdateBaseline(bool suppressUpdate = false)
        {
            // Update baseline for all terrains
            foreach (var kvp in terrainStates)
            {
                var terrain = kvp.Key;
                var state = kvp.Value;
                if (terrain == null) continue;
                
                // Use local variables for ref parameters (cannot pass properties as ref)
                float[,] baselineHeights = state.BaselineHeights;
                bool[,] baselineHoles = state.BaselineHoles;
                float[,,] baselineAlphamaps = state.BaselineAlphamaps;
                
                baselineUpdateStrategy?.UpdateBaseline(
                    terrain,
                    ref baselineHeights,
                    state.WorkingHeights,
                    ref baselineHoles,
                    state.WorkingHoles,
                    ref baselineAlphamaps,
                    state.WorkingAlphamaps,
                    state.PaintSplineAlphaMask,
                    state.HoleSplineMask,
                    suppressUpdate);
                
                // Update state with modified values
                state.BaselineHeights = baselineHeights;
                state.BaselineHoles = baselineHoles;
                state.BaselineAlphamaps = baselineAlphamaps;
                    
                if (state.BaselineHeights != null && terrain != null)
                {
                    state.BaselineTexture = TerraSplinesTool.HeightsToTexture(terrain, state.BaselineHeights);
                    HeightRangeNeedsUpdate = true;
                }
            }
        }
        
        public void SaveUndoState()
        {
            // Save undo state for all terrains
            foreach (var state in terrainStates.Values)
            {
                if (state.BaselineHeights != null)
                {
                    state.UndoBuffer = TerraSplinesTool.CopyHeights(state.BaselineHeights);
                    state.CanUndo = true;
                    state.CanRedo = false;
                }
                
                if (state.BaselineHoles != null)
                {
                    state.UndoHolesBuffer = TerraSplinesTool.CopyHoles(state.BaselineHoles);
                }
                
                if (state.BaselineAlphamaps != null)
                {
                    state.UndoAlphamapsBuffer = TerraSplinesTool.CopyAlphamaps(state.BaselineAlphamaps);
                }

                if (state.BaselineDetailLayers != null)
                {
                    state.UndoDetailLayersBuffer = TerraSplinesTool.CopyDetailLayers(state.BaselineDetailLayers);
                }
            }
        }
        
        public void PerformUndo()
        {
            // Perform undo for all terrains
            foreach (var kvp in terrainStates)
            {
                var terrain = kvp.Key;
                var state = kvp.Value;
                if (terrain == null) continue;
                
                bool hasHeightUndo = state.UndoBuffer != null;
                bool hasAlphamapUndo = state.UndoAlphamapsBuffer != null;
                bool hasDetailUndo = state.UndoDetailLayersBuffer != null;
                
                if (hasHeightUndo || hasAlphamapUndo || hasDetailUndo)
                {
                    // Swap baseline and undo buffer for heights
                    if (hasHeightUndo)
                    {
                        (state.BaselineHeights, state.UndoBuffer) = (state.UndoBuffer, state.BaselineHeights);
                    }
                    
                    // Swap baseline and undo buffer for holes
                    if (state.UndoHolesBuffer != null && state.BaselineHoles != null)
                    {
                        (state.BaselineHoles, state.UndoHolesBuffer) = (state.UndoHolesBuffer, state.BaselineHoles);
                    }

                    // Swap baseline and undo buffer for alphamaps
                    if (state.UndoAlphamapsBuffer != null && state.BaselineAlphamaps != null)
                    {
                        (state.BaselineAlphamaps, state.UndoAlphamapsBuffer) = (state.UndoAlphamapsBuffer, state.BaselineAlphamaps);
                    }

                    if (state.UndoDetailLayersBuffer != null && state.BaselineDetailLayers != null)
                    {
                        (state.BaselineDetailLayers, state.UndoDetailLayersBuffer) = (state.UndoDetailLayersBuffer, state.BaselineDetailLayers);
                    }

                    // Toggle flags
                    state.CanUndo = false;
                    state.CanRedo = true;

                    // Update baseline texture
                    state.BaselineTexture = TerraSplinesTool.HeightsToTexture(terrain, state.BaselineHeights);
                }
            }
        }
        
        public void PerformRedo()
        {
            // Perform redo for all terrains
            foreach (var kvp in terrainStates)
            {
                var terrain = kvp.Key;
                var state = kvp.Value;
                if (terrain == null) continue;
                
                bool hasHeightRedo = state.UndoBuffer != null;
                bool hasAlphamapRedo = state.UndoAlphamapsBuffer != null;
                bool hasDetailRedo = state.UndoDetailLayersBuffer != null;
                
                if (hasHeightRedo || hasAlphamapRedo || hasDetailRedo)
                {
                    // Swap baseline and undo buffer for heights
                    if (hasHeightRedo)
                    {
                        (state.BaselineHeights, state.UndoBuffer) = (state.UndoBuffer, state.BaselineHeights);
                    }
                    
                    // Swap baseline and undo buffer for holes
                    if (state.UndoHolesBuffer != null && state.BaselineHoles != null)
                    {
                        (state.BaselineHoles, state.UndoHolesBuffer) = (state.UndoHolesBuffer, state.BaselineHoles);
                    }

                    // Swap baseline and undo buffer for alphamaps
                    if (state.UndoAlphamapsBuffer != null && state.BaselineAlphamaps != null)
                    {
                        (state.BaselineAlphamaps, state.UndoAlphamapsBuffer) = (state.UndoAlphamapsBuffer, state.BaselineAlphamaps);
                    }

                    if (state.UndoDetailLayersBuffer != null && state.BaselineDetailLayers != null)
                    {
                        (state.BaselineDetailLayers, state.UndoDetailLayersBuffer) = (state.UndoDetailLayersBuffer, state.BaselineDetailLayers);
                    }

                    // Toggle flags
                    state.CanUndo = true;
                    state.CanRedo = false;

                    // Update baseline texture
                    state.BaselineTexture = TerraSplinesTool.HeightsToTexture(terrain, state.BaselineHeights);
                }
            }
        }
    }
}

