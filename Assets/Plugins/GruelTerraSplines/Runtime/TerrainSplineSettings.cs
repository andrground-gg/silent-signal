using UnityEngine;

namespace GruelTerraSplines
{
    /// <summary>
    /// MonoBehaviour component that stores per-spline terrain settings.
    /// Automatically added to SplineContainer GameObjects to persist settings
    /// when splines are toggled on/off or reordered in the hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    public class TerrainSplineSettings : MonoBehaviour
    {
        [SerializeField]
        public SplineStrokeSettings settings = new SplineStrokeSettings();

        /// <summary>
        /// Resets settings to default values. Called by Unity's Reset context menu.
        /// </summary>
        void Reset()
        {
            settings = new SplineStrokeSettings();
        }

        /// <summary>
        /// Ensures the component has valid default settings.
        /// Called when the component is first created or when settings are null.
        /// </summary>
        void Awake()
        {
            if (settings == null)
            {
                settings = new SplineStrokeSettings();
            }
        }
    }
}