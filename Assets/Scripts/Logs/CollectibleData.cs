using UnityEngine;

public enum CollectibleType
{
    Note,
    Audio
}

public abstract class CollectibleData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string title;

    public abstract CollectibleType Type { get; }

    public LogKeys key;

    [Header("Board")]
    public BoardTopic topic;

    [TextArea(5, 30)]
    public string boardContentText;

    [Tooltip("Optional sprite shown in the investigation board.")]
    public Sprite boardSprite;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id))
            id = name;
    }
}