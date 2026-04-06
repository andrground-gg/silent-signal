using UnityEngine;
using UnityEngine.Splines;

namespace GruelTerraSplines
{
	public class ShapeHeightmapCache : ISplineHeightmapCache
	{
		public float[,] cachedHeights;      // Pre-computed heights for this shape
		public float[,] cachedAlpha;        // Pre-computed blend weights
		public int minX, minZ, maxX, maxZ;  // Affected region bounds
		public Vector3 lastPosition;
		public Quaternion lastRotation;
		public Vector3 lastScale;
		public int splineVersion;
		public float lastBrushSize;
		public float lastSampleStep;
		public SplineApplyMode lastMode;
		public int lastBrushSizeCurveHash;
		public float lastBrushHardness;
		public bool isValid;
		
		// Cached preview texture (generated from cachedHeights and cachedAlpha)
		public Texture2D cachedPreviewTexture;
		public int cachedPreviewSize = -1; // Track preview size to regenerate if size changes

		float[,] ISplineHeightmapCache.cachedHeights => cachedHeights;
		float[,] ISplineHeightmapCache.cachedAlpha => cachedAlpha;
		int ISplineHeightmapCache.minX => minX;
		int ISplineHeightmapCache.minZ => minZ;
		int ISplineHeightmapCache.maxX => maxX;
		int ISplineHeightmapCache.maxZ => maxZ;
		bool ISplineHeightmapCache.isValid => isValid;
		Vector3 ISplineHeightmapCache.lastPosition => lastPosition;
		Quaternion ISplineHeightmapCache.lastRotation => lastRotation;
		Vector3 ISplineHeightmapCache.lastScale => lastScale;
		int ISplineHeightmapCache.splineVersion => splineVersion;
		float ISplineHeightmapCache.lastBrushSize => lastBrushSize;
		float ISplineHeightmapCache.lastSampleStep => lastSampleStep;
		SplineApplyMode ISplineHeightmapCache.lastMode => lastMode;
		int ISplineHeightmapCache.lastBrushSizeCurveHash => lastBrushSizeCurveHash;
		float ISplineHeightmapCache.lastBrushHardness => lastBrushHardness;
		Texture2D ISplineHeightmapCache.cachedPreviewTexture { get => cachedPreviewTexture; set => cachedPreviewTexture = value; }
		int ISplineHeightmapCache.cachedPreviewSize { get => cachedPreviewSize; set => cachedPreviewSize = value; }

		public bool IsDirty(SplineContainer container, float brushSize, float sampleStep, SplineApplyMode mode, AnimationCurve brushSizeMultiplier, float brushHardness)
		{
			return TerraSplinesTool.IsHeightmapCacheDirty(container, this, brushSize, sampleStep, mode, brushSizeMultiplier, brushHardness);
		}
	}
}
