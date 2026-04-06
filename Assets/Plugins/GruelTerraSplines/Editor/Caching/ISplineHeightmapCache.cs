using UnityEngine;
using UnityEngine.Splines;

namespace GruelTerraSplines
{
	public interface ISplineHeightmapCache
	{
		float[,] cachedHeights { get; }
		float[,] cachedAlpha { get; }
		int minX { get; }
		int minZ { get; }
		int maxX { get; }
		int maxZ { get; }
		bool isValid { get; }
		Vector3 lastPosition { get; }
		Quaternion lastRotation { get; }
		Vector3 lastScale { get; }
		int splineVersion { get; }
		float lastBrushSize { get; }
		float lastSampleStep { get; }
		SplineApplyMode lastMode { get; }
		int lastBrushSizeCurveHash { get; }
		float lastBrushHardness { get; }
		bool IsDirty(SplineContainer container, float brushSize, float sampleStep, SplineApplyMode mode, AnimationCurve brushSizeMultiplier, float brushHardness);
		
		// Preview texture caching (optional - implementations can provide these)
		Texture2D cachedPreviewTexture { get; set; }
		int cachedPreviewSize { get; set; }
	}
}
