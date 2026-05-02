#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Watches the editor Hierarchy and keeps every InvestigationBoard's
/// `entries` and `connections` lists in sync with its children.
///
/// Place this file under an Editor/ folder.
/// </summary>
[InitializeOnLoad]
public static class InvestigationBoardAutoFill
{
    static InvestigationBoardAutoFill()
    {
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
    }

    private static void OnHierarchyChanged()
    {
        if (Application.isPlaying) return;

        var boards = Object.FindObjectsByType<InvestigationBoard>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var board in boards)
            Sync(board);
    }

    private static void Sync(InvestigationBoard board)
    {
        var entries = new List<BoardInteractable>();
        board.GetComponentsInChildren<BoardInteractable>(includeInactive: true, entries);

        var connections = new List<BoardConnection>();
        board.GetComponentsInChildren<BoardConnection>(includeInactive: true, connections);

        var so = new SerializedObject(board);
        var entriesProp = so.FindProperty("entries");
        var connectionsProp = so.FindProperty("connections");

        bool entriesChanged = entriesProp != null && WriteIfChanged(entriesProp, entries);
        bool connectionsChanged = connectionsProp != null && WriteIfChanged(connectionsProp, connections);

        if (!entriesChanged && !connectionsChanged) return;

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(board);
        EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
    }

    /// <summary>
    /// Writes `current` into the array property only if its content differs.
    /// Returns true if a write happened.
    /// </summary>
    private static bool WriteIfChanged<T>(SerializedProperty prop, List<T> current) where T : Object
    {
        if (IsSameList(prop, current)) return false;

        prop.arraySize = current.Count;
        for (int i = 0; i < current.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = current[i];

        return true;
    }

    private static bool IsSameList<T>(SerializedProperty prop, List<T> current) where T : Object
    {
        if (prop.arraySize != current.Count) return false;
        for (int i = 0; i < current.Count; i++)
        {
            if (prop.GetArrayElementAtIndex(i).objectReferenceValue != current[i])
                return false;
        }
        return true;
    }
}
#endif