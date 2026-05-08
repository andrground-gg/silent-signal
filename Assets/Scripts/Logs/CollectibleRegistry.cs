using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// Tracks which collectibles have been discovered.
/// Uses LogKeys enum instead of strings for type safety.
/// </summary>
public class CollectibleRegistry : Singleton<CollectibleRegistry>
{
    private const string PrefsKey = "CollectibleRegistry.Discovered";

    private readonly HashSet<LogKeys> discoveredKeys = new HashSet<LogKeys>();

    [Tooltip("Drop ALL NoteData / AudioLogData assets here so the archive and board can find them.")]
    [SerializeField] private List<CollectibleData> allCollectibles = new List<CollectibleData>();

    public event Action<CollectibleData> OnFirstDiscovery;

    protected override void Awake()
    {
        base.Awake();
        Load();
        ValidateAssets();
    }

    /// <summary>
    /// Logs duplicates and None-keyed assets at startup so problems
    /// are visible without opening the editor tool.
    /// </summary>
    private void ValidateAssets()
    {
        var seen = new Dictionary<LogKeys, CollectibleData>();
        foreach (var c in allCollectibles)
        {
            if (c == null) continue;

            if (c.key == LogKeys.None)
            {
                Debug.LogWarning($"[CollectibleRegistry] '{c.name}' has key = None.", c);
                continue;
            }

            if (c.key != LogKeys.None && seen.TryGetValue(c.key, out var existing))
            {
                Debug.LogError(
                    $"[CollectibleRegistry] Duplicate key '{c.key}' on '{c.name}' and '{existing.name}'. " +
                    "Both will be treated as the same entry.", c);
            }
            else
            {
                seen[c.key] = c;
            }
        }
    }

    public bool IsDiscovered(CollectibleData data)
        => data != null && discoveredKeys.Contains(data.key);

    public bool IsDiscovered(LogKeys key)
        => key != LogKeys.None && discoveredKeys.Contains(key);

    public bool MarkDiscovered(CollectibleData data)
    {
        if (data == null || data.key == LogKeys.None) return false;
        if (!discoveredKeys.Add(data.key)) return false;

        UIManager.Instance.ShowDiscoveryText();
        Save();
        Debug.Log($"[CollectibleRegistry] Discovered key '{data.key}'");
        OnFirstDiscovery?.Invoke(data);
        return true;
    }

    public IEnumerable<NoteData> GetDiscoveredNotes()
    {
        foreach (var c in allCollectibles)
            if (c is NoteData note && discoveredKeys.Contains(note.key))
                yield return note;
    }

    public IEnumerable<AudioLogData> GetDiscoveredAudioLogs()
    {
        foreach (var c in allCollectibles)
            if (c is AudioLogData log && discoveredKeys.Contains(log.key))
                yield return log;
    }

    public IEnumerable<CollectibleData> GetAllDiscovered()
    {
        foreach (var c in allCollectibles)
            if (c != null && discoveredKeys.Contains(c.key))
                yield return c;
    }

    // ---------- Persistence ----------
    // Stored as comma-separated int values of LogKeys enum.

    private void Save()
    {
        var ints = new List<string>(discoveredKeys.Count);
        foreach (var k in discoveredKeys) ints.Add(((int)k).ToString());
        PlayerPrefs.SetString(PrefsKey, string.Join(",", ints));
        PlayerPrefs.Save();
    }

    private void Load()
    {
        discoveredKeys.Clear();
        if (!PlayerPrefs.HasKey(PrefsKey)) return;
        var raw = PlayerPrefs.GetString(PrefsKey);
        if (string.IsNullOrEmpty(raw)) return;

        foreach (var s in raw.Split(','))
        {
            if (int.TryParse(s, out var i) && Enum.IsDefined(typeof(LogKeys), i))
                discoveredKeys.Add((LogKeys)i);
        }
    }

    [ContextMenu("Reset Saves")]
    private void DebugReset()
    {
        discoveredKeys.Clear();
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }

    // ---------- Editor-only ----------

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void HookSceneEvents()
    {
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        // Find every registry in the just-opened scene and refresh it.
        var registries = FindObjectsByType<CollectibleRegistry>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var registry in registries)
            registry.RefreshAllCollectibles();
    }

    [ContextMenu("Find All Collectibles In Project")]
    private void RefreshAllCollectibles()
    {
        allCollectibles.Clear();

        var guids = AssetDatabase.FindAssets($"t:{nameof(CollectibleData)}");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<CollectibleData>(path);
            if (asset != null) allCollectibles.Add(asset);
        }

        EditorUtility.SetDirty(this);
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(gameObject.scene);

        Debug.Log($"[CollectibleRegistry] Found {allCollectibles.Count} collectible(s).", this);
    }
#endif
}