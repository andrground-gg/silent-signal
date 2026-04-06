using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace GruelTerraSplines.Managers
{
    /// <summary>
    /// Base interface for all window managers.
    /// Provides common lifecycle methods and access to the main window.
    /// </summary>
    public interface IWindowManager
    {
        /// <summary>
        /// Initialize the manager. Called when the window is created.
        /// </summary>
        void Initialize(EditorWindow window);

        /// <summary>
        /// Cleanup the manager. Called when the window is destroyed.
        /// </summary>
        void OnDestroy();
    }
}