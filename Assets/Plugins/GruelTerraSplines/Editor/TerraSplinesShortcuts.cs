using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace GruelTerraSplines
{
    /// <summary>
    /// Unity shortcuts for the Gruel TerraSplines Tool.
    /// This class provides keyboard shortcuts that work globally when the tool window is open.
    /// </summary>
    public static class TerraSplinesShortcuts
    {
        /// <summary>
        /// Update Preview shortcut - Alt+U
        /// Triggers a preview update for the terrain spline tool when the window is open.
        /// </summary>
        [Shortcut("Gruel TerraSplines/Update Preview", KeyCode.U, ShortcutModifiers.Alt)]
        public static void UpdatePreview()
        {
            // Find the open DK Terrain Splines window
            var window = Resources.FindObjectsOfTypeAll<TerraSplinesWindow>().FirstOrDefault();

            if (window != null)
            {
                // Trigger the preview update
                window.TriggerPreviewUpdate();
            }
            else
            {
                Debug.LogWarning("Gruel TerraSplines window is not open. Cannot update preview.");
            }
        }

        /// <summary>
        /// Toggle Pause/Resume Updates shortcut - Alt+Y
        /// Toggles the pause/resume state for automatic terrain updates when the window is open.
        /// </summary>
        [Shortcut("Gruel TerraSplines/Toggle Pause/Resume Updates", KeyCode.Y, ShortcutModifiers.Alt)]
        public static void TogglePauseResume()
        {
            // Find the open Gruel TerraSplines window
            var window = Resources.FindObjectsOfTypeAll<TerraSplinesWindow>().FirstOrDefault();

            if (window != null)
            {
                // Toggle the pause/resume state
                window.TogglePauseResume();
            }
            else
            {
                Debug.LogWarning("Gruel TerraSplines window is not open. Cannot toggle pause/resume.");
            }
        }
    }
}