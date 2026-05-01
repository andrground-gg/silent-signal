using UnityEngine;

public enum CollectibleType
{
    Note,
    Audio
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

    [Header("Investigation Board")]
    [Tooltip("Optional. If set, on FIRST discovery this id is pushed to the InvestigationBoard.")]
    public string boardUpdateID;

    private void OnValidate()
    {
        // Auto-fill id from asset name if missing, so designers can't ship empty ids.
        if (string.IsNullOrEmpty(id))
        {
            id = name;
        }
    }
}
