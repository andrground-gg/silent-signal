#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Watches the editor Hierarchy and keeps every InvestigationBoard's
/// `entries` list in sync with its child BoardInteractables.
///
/// Place this file under an Editor/ folder.
///
/// Why this exists: OnTransformChildrenChanged is unreliable in edit mode
/// without [ExecuteAlways], and [ExecuteAlways] caused scene state to
/// leak from play mode back into edit mode. This handles auto-fill from
/// outside the component, no runtime side effects.
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
            SyncEntries(board);
    }

    private static void SyncEntries(InvestigationBoard board)
    {
        // Collect current children with BoardInteractable.
        var current = new List<BoardInteractable>();
        board.GetComponentsInChildren<BoardInteractable>(includeInactive: true, current);

        var so = new SerializedObject(board);
        var prop = so.FindProperty("entries");
        if (prop == null) return;

        // Skip if already in sync — avoids dirtying the scene on every selection click.
        if (IsSameList(prop, current)) return;

        prop.arraySize = current.Count;
        for (int i = 0; i < current.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = current[i];

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(board);
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
    }

    private static bool IsSameList(SerializedProperty prop, List<BoardInteractable> current)
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
