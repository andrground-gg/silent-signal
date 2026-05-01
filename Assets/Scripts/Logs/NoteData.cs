using UnityEngine;

[CreateAssetMenu(fileName = "Note_", menuName = "Collectibles/Note", order = 0)]
public class NoteData : CollectibleData
{
    public override CollectibleType Type => CollectibleType.Note;

    [Header("Note")]
    [TextArea(5, 30)]
    [Tooltip("Full note body shown in the reader UI.")]
    public string contentText;

    [Tooltip("Optional paper/letterhead background sprite.")]
    public Sprite paperSprite;
}
