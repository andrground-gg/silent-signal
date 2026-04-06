using UnityEngine;

namespace GruelTerraSplines.Managers
{
    /// <summary>
    /// Holds state data for a single terrain
    /// </summary>
    public class TerrainState
    {
        public Terrain Terrain { get; set; }
        
        // Data arrays
        public float[,] OriginalHeights { get; set; }
        public float[,] BaselineHeights { get; set; }
        public float[,] WorkingHeights { get; set; }
        public bool[,] OriginalHoles { get; set; }
        public bool[,] BaselineHoles { get; set; }
        public bool[,] WorkingHoles { get; set; }
        public float[,,] OriginalAlphamaps { get; set; }
        public float[,,] BaselineAlphamaps { get; set; }
        public float[,,] WorkingAlphamaps { get; set; }
        public int[][,] OriginalDetailLayers { get; set; }
        public int[][,] BaselineDetailLayers { get; set; }
        public int[][,] WorkingDetailLayers { get; set; }
        
        // Preview textures
        public Texture2D BaselineTexture { get; set; }
        public Texture2D PreviewTexture { get; set; }
        
        // Undo buffers
        public float[,] UndoBuffer { get; set; }
        public bool[,] UndoHolesBuffer { get; set; }
        public float[,,] UndoAlphamapsBuffer { get; set; }
        public int[][,] UndoDetailLayersBuffer { get; set; }
        
        // Masks
        public float[,] PaintSplineAlphaMask { get; set; }
        public bool[,] HoleSplineMask { get; set; }
        
        // State flags
        public bool CanUndo { get; set; }
        public bool CanRedo { get; set; }
        public bool HeightRangeNeedsUpdate { get; set; }
    }
}

