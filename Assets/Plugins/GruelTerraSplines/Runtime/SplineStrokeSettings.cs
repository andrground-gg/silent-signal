using System;
using System.Collections.Generic;
using UnityEngine;

namespace GruelTerraSplines
{
    public enum SplineApplyMode
    {
        Path = 0,
        Shape = 1
    }

    [Serializable]
    public class BrushNoiseSettings
    {
        [Tooltip("Optional noise texture used to modulate brush alpha for this spline.")]
        public Texture2D noiseTexture = null;

        [Range(0f, 1f)]
        [Tooltip("0 = no noise influence, 1 = fully multiply by noise.")]
        public float noiseStrength = 1f;

        [Range(-1f, 1f)]
        [Tooltip("Bias noise toward center or edges. 0 = noise everywhere, 1 = noise only near edges, -1 = noise only near center/interior.")]
        public float noiseEdge = 0f;

        [Min(0.001f)]
        [Tooltip("World meters per noise tile. Larger values = larger blobs; smaller values = more frequent variation.")]
        public float noiseWorldSizeMeters = 128f;

        [Tooltip("UV offset applied after world-to-UV mapping.")]
        public Vector2 noiseOffset = Vector2.zero;

        [Tooltip("Invert sampled noise (1-noise).")]
        public bool noiseInvert = false;

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (noiseTexture != null ? noiseTexture.GetInstanceID() : 0);
                hash = hash * 31 + noiseStrength.GetHashCode();
                hash = hash * 31 + noiseEdge.GetHashCode();
                hash = hash * 31 + noiseWorldSizeMeters.GetHashCode();
                hash = hash * 31 + noiseOffset.GetHashCode();
                hash = hash * 31 + noiseInvert.GetHashCode();
                return hash;
            }
        }
    }

    public enum DetailOperationMode
    {
        Add = 0,
        Remove = 1,
    }

    public enum BackendType
    {
        Unknown = 0,
        CPU = 1,
        GPU = 2
    }

    [Serializable]
    public class PaintNoiseLayerSettings
    {
        [Tooltip("Terrain paint layer index this noise settings entry applies to.")]
        public int paintLayerIndex = 0;

        [Tooltip("Optional noise texture used to modulate paint alpha for this layer.")]
        public Texture2D noiseTexture = null;

        [Range(0f, 1f)]
        [Tooltip("0 = no noise influence, 1 = fully multiply by noise.")]
        public float noiseStrength = 1f;

        [Range(-1f, 1f)]
        [Tooltip("Bias noise toward center or edges. 0 = noise everywhere, 1 = noise only near edges, -1 = noise only near center/interior.")]
        public float noiseEdge = 0f;

        [Min(0.001f)]
        [Tooltip("World meters per noise tile. Larger values = larger blobs; smaller values = more frequent variation.")]
        public float noiseWorldSizeMeters = 128f;

        [Tooltip("UV offset applied after world-to-UV mapping.")]
        public Vector2 noiseOffset = Vector2.zero;

        [Tooltip("Invert sampled noise (1-noise).")]
        public bool noiseInvert = false;

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + paintLayerIndex.GetHashCode();
                hash = hash * 31 + (noiseTexture != null ? noiseTexture.GetInstanceID() : 0);
                hash = hash * 31 + noiseStrength.GetHashCode();
                hash = hash * 31 + noiseEdge.GetHashCode();
                hash = hash * 31 + noiseWorldSizeMeters.GetHashCode();
                hash = hash * 31 + noiseOffset.GetHashCode();
                hash = hash * 31 + noiseInvert.GetHashCode();
                return hash;
            }
        }
    }

    [Serializable]
    public class DetailNoiseLayerSettings
    {
        [Tooltip("Terrain detail layer index this noise settings entry applies to.")]
        public int detailLayerIndex = 0;

        [Tooltip("Optional noise texture used to modulate density for this detail layer.")]
        public Texture2D noiseTexture = null;

        [Range(0f, 1f)]
        [Tooltip("0 = no noise influence, 1 = fully multiply by noise.")]
        public float noiseStrength = 1f;

        [Min(0.001f)]
        [Tooltip("World meters per noise tile. Larger values = larger blobs; smaller values = more frequent variation.")]
        public float noiseWorldSizeMeters = 128f;

        [Tooltip("UV offset applied after world-to-UV mapping.")]
        public Vector2 noiseOffset = Vector2.zero;

        [Range(0f, 1f)]
        [Tooltip("Noise values below this threshold are treated as 0 (hard cutoff), higher values are remapped to 0..1.")]
        public float noiseThreshold = 0.5f;

        [Tooltip("Invert sampled noise (1-noise).")]
        public bool noiseInvert = false;

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + detailLayerIndex.GetHashCode();
                hash = hash * 31 + (noiseTexture != null ? noiseTexture.GetInstanceID() : 0);
                hash = hash * 31 + noiseStrength.GetHashCode();
                hash = hash * 31 + noiseWorldSizeMeters.GetHashCode();
                hash = hash * 31 + noiseOffset.GetHashCode();
                hash = hash * 31 + noiseThreshold.GetHashCode();
                hash = hash * 31 + noiseInvert.GetHashCode();
                return hash;
            }
        }
    }

    [Serializable]
    public class SplineStrokeSettings
    {
        public bool enabled = true;

        [Header("Mode")]
        public bool overrideMode = false;

        [Space]
        public SplineApplyMode mode = SplineApplyMode.Path;

        [Header("Operations")]
        public bool operationHeight = true;
        public bool operationPaint = false;
        public bool operationHole = false;
        public bool operationFill = false;
        public bool operationAddDetail = false;
        public bool operationRemoveDetail = false;

        [Header("Brush")]
        public bool overrideBrush = false;

        [Space]
        public bool overrideSizeMultiplier = false;

        public AnimationCurve sizeMultiplier = new AnimationCurve(
            new Keyframe(0f, 0.5f), // Start at 50% size
            new Keyframe(0.5f, 1.0f), // Middle at 100% size  
            new Keyframe(1f, 0.5f) // End at 50% size
        );

        [Range(0.1f, 200f)]
        public float sizeMeters = 5f;

        [Range(0f, 1f)]
        public float hardness = 0.5f; // 0-1, controls falloff curve exponent (0=soft, 0.5=linear, 1=hard)

        [Range(0f, 1f)]
        public float strength = 1f; // 0..1 blend strength toward target

        [Range(.05f, 30f)]
        public float sampleStep = 1f;

        [Header("Brush Noise")]
        public BrushNoiseSettings brushNoise = new BrushNoiseSettings();

        [Header("Paint")]
        public bool overridePaint = false;

        [Space]
        public int selectedLayerIndex = 0; // Index into terrain's terrainLayers array

        [Range(0f, 1f)]
        public float paintStrength = 1f; // 0-1 blend strength for painting

        [Tooltip("Optional per-paint-layer noise textures used to modulate paint alpha. Entries are keyed by paint layer index.")]
        public List<PaintNoiseLayerSettings> paintNoiseLayers = new List<PaintNoiseLayerSettings>();

        [Header("Detail")]
        public bool overrideDetail = false;

        [Space]
        public int selectedDetailLayerIndex = 0; // Legacy single selection (index into terrain's detail prototypes array)

        public List<int> selectedDetailLayerIndices = new List<int>(); // Multi-selection (indices into terrain's detail prototypes array)

        [Range(0f, 1f)]
        public float detailStrength = 1f; // 0-1 blend strength for details

        public DetailOperationMode detailMode = DetailOperationMode.Add;

        [Range(10, 2048)]
        public int detailTargetDensity = 128;

        [Range(0f, 90f)]
        public float detailSlopeLimitDegrees = 90f;

        [Range(0.1f, 8f)]
        public float detailFalloffPower = 0.89f;

        [Range(0, 4)]
        public int detailSpreadRadius = 0;

        [Range(0f, 1f)]
        public float detailRemoveThreshold = 0.5f;

        [Tooltip("Optional per-detail-layer noise textures used to modulate detail density. Entries are keyed by detail layer index.")]
        public List<DetailNoiseLayerSettings> detailNoiseLayers = new List<DetailNoiseLayerSettings>();

        public override int GetHashCode()
        {
            unchecked // Allow overflow, just wrap around
            {
                int hash = 17;
                hash = hash * 31 + enabled.GetHashCode();
                hash = hash * 31 + overrideMode.GetHashCode();
                hash = hash * 31 + mode.GetHashCode();
                hash = hash * 31 + operationHeight.GetHashCode();
                hash = hash * 31 + operationPaint.GetHashCode();
                hash = hash * 31 + operationHole.GetHashCode();
                hash = hash * 31 + operationFill.GetHashCode();
                hash = hash * 31 + operationAddDetail.GetHashCode();
                hash = hash * 31 + operationRemoveDetail.GetHashCode();
                hash = hash * 31 + overrideBrush.GetHashCode();
                hash = hash * 31 + overrideSizeMultiplier.GetHashCode();
                hash = hash * 31 + sizeMultiplier.GetAnimationCurveHash();
                hash = hash * 31 + sizeMeters.GetHashCode();
                hash = hash * 31 + hardness.GetHashCode();
                hash = hash * 31 + strength.GetHashCode();
                hash = hash * 31 + sampleStep.GetHashCode();
                if (brushNoise != null)
                {
                    hash = hash * 31 + brushNoise.GetHashCode();
                }
                hash = hash * 31 + overridePaint.GetHashCode();
                hash = hash * 31 + selectedLayerIndex.GetHashCode();
                hash = hash * 31 + paintStrength.GetHashCode();
                if (paintNoiseLayers != null && paintNoiseLayers.Count > 0)
                {
                    for (int i = 0; i < paintNoiseLayers.Count; i++)
                    {
                        hash = hash * 31 + (paintNoiseLayers[i] != null ? paintNoiseLayers[i].GetHashCode() : 0);
                    }
                }
                hash = hash * 31 + overrideDetail.GetHashCode();
                hash = hash * 31 + selectedDetailLayerIndex.GetHashCode();
                if (selectedDetailLayerIndices != null && selectedDetailLayerIndices.Count > 0)
                {
                    for (int i = 0; i < selectedDetailLayerIndices.Count; i++)
                    {
                        hash = hash * 31 + selectedDetailLayerIndices[i].GetHashCode();
                    }
                }
                hash = hash * 31 + detailStrength.GetHashCode();
                hash = hash * 31 + detailMode.GetHashCode();
                hash = hash * 31 + detailTargetDensity.GetHashCode();
                hash = hash * 31 + detailSlopeLimitDegrees.GetHashCode();
                hash = hash * 31 + detailFalloffPower.GetHashCode();
                hash = hash * 31 + detailSpreadRadius.GetHashCode();
                hash = hash * 31 + detailRemoveThreshold.GetHashCode();
                if (detailNoiseLayers != null && detailNoiseLayers.Count > 0)
                {
                    for (int i = 0; i < detailNoiseLayers.Count; i++)
                    {
                        hash = hash * 31 + (detailNoiseLayers[i] != null ? detailNoiseLayers[i].GetHashCode() : 0);
                    }
                }
                return hash;
            }
        }
    }
}
