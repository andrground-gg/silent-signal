using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum CollectibleType
{
    Note,
    Audio
}

[Serializable]
public class BoardContentUpdate
{
    [Tooltip("When this key is unlocked, the text below is appended to boardContentText.")]
    public LogKeys triggerKey;

    [TextArea(2, 5)]
    public string textToAppend;
}

/// <summary>
/// Base ScriptableObject for any piece of collectible information
/// (notes, audio logs). Holds shared fields described in the design doc.
/// </summary>
public abstract class CollectibleData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique stable id. Used for save/load and registry lookup.")]
    public string id;

    [Tooltip("Display title shown in UI and in keeper's archive.")]
    public string title;

    public abstract CollectibleType Type { get; }

    public LogKeys key;

    [TextArea(5, 30)]
    [Tooltip("Base text shown on the board popup.")]
    public string boardContentText;

    [Tooltip("Optional sprite shown in the investigation board.")]
    public Sprite boardSprite;

    
    [Tooltip("Shown while triggerKey is not unlocked yet.")]
    public string lockedPlaceholder = "-???????";
    
    [Header("Board Content Updates")]
    [Tooltip("Each entry: when triggerKey gets unlocked, its text is appended to boardContentText on the popup.")]
    public List<BoardContentUpdate> boardUpdates = new List<BoardContentUpdate>();

    /// <summary>
    /// Returns boardContentText combined with all updates whose trigger key is unlocked.
    /// Pass an isUnlocked predicate (typically CollectibleRegistry.IsDiscovered) so this
    /// SO doesn't depend on runtime systems directly.
    /// </summary>
    public string GetCurrentBoardText(Func<LogKeys, bool> isUnlocked)
    {
        if (boardUpdates == null || boardUpdates.Count == 0)
            return boardContentText;

        var sb = new StringBuilder();
        sb.Append(boardContentText);

        foreach (var update in boardUpdates)
        {
            if (update == null) continue;
            if (update.triggerKey == LogKeys.None) continue;

            sb.AppendLine();

            if (isUnlocked != null && isUnlocked(update.triggerKey))
                sb.Append(update.textToAppend);
            else
                sb.Append(lockedPlaceholder);
        }
        
        return sb.ToString();
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id))
            id = name;
    }
}