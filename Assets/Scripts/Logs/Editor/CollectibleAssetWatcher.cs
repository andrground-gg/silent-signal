#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Watches for CollectibleData asset creation/deletion/movement and refreshes
/// the allCollectibles list on any CollectibleRegistry instance in the
/// currently open scene(s).
///
/// Place this file under an Editor/ folder.
/// </summary>
public class CollectibleAssetWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        bool needsRefresh =
            ContainsCollectibleAsset(importedAssets) ||
            ContainsAnyAsset(deletedAssets) ||           // can't load deleted assets — check by extension only
            ContainsCollectibleAsset(movedAssets);

        if (!needsRefresh) return;

        RefreshRegistries();
    }

    /// <summary>For imported/moved — file exists, we can load and verify type.</summary>
    private static bool ContainsCollectibleAsset(string[] paths)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset")) continue;
            var asset = AssetDatabase.LoadAssetAtPath<CollectibleData>(path);
            if (asset != null) return true;
        }
        return false;
    }

    /// <summary>
    /// For deleted assets we can't load them anymore (file is gone),
    /// so we trigger refresh on any .asset deletion. RefreshRegistries
    /// is cheap and idempotent — false positives are harmless.
    /// </summary>
    private static bool ContainsAnyAsset(string[] paths)
    {
        foreach (var path in paths)
        {
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".asset"))
                return true;
        }
        return false;
    }

    private static void RefreshRegistries()
    {
        // Gather all CollectibleData currently in the project.
        var guids = AssetDatabase.FindAssets($"t:{nameof(CollectibleData)}");
        var allData = new List<CollectibleData>(guids.Length);
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<CollectibleData>(path);
            if (asset != null) allData.Add(asset);
        }

        var registries = Object.FindObjectsByType<CollectibleRegistry>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (registries.Length == 0) return;

        foreach (var registry in registries)
        {
            var so = new SerializedObject(registry);
            var prop = so.FindProperty("allCollectibles");
            if (prop == null) continue;

            prop.arraySize = allData.Count;
            for (int i = 0; i < allData.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = allData[i];

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(registry);
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(registry.gameObject.scene);
        }

        Debug.Log($"[CollectibleAssetWatcher] Refreshed {registries.Length} registry(ies) with {allData.Count} collectible(s).");
    }
}
#endif