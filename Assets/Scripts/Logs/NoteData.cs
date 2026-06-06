using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Note_", menuName = "Collectibles/Note", order = 0)]
public class NoteData : CollectibleData
{
    public override CollectibleType Type => CollectibleType.Note;

    /// Marker the author drops into <see cref="contentText"/> to split the note
    /// across its two sides. It never reaches the player — the reader shows the
    /// text with it removed, and each side gets the part on its own side.
    public const string PageBreak = "<page>";

    [Header("Note")]
    [TextArea(5, 30)]
    [Tooltip("Full note body shown in the reader UI. Insert <page> where the note should split across its two sides.")]
    public string contentText;

    [Tooltip("Optional paper/letterhead background sprite.")]
    public Sprite paperSprite;

    /// Part before the page break — the note's front side (whole text if no break).
    public string FrontText
    {
        get
        {
            int i = BreakIndex;
            return i < 0 ? (contentText ?? string.Empty) : contentText.Substring(0, i).TrimEnd();
        }
    }

    /// Part after the page break — the note's back side (empty if no break).
    public string BackText
    {
        get
        {
            int i = BreakIndex;
            return i < 0 ? string.Empty : contentText.Substring(i + PageBreak.Length).TrimStart();
        }
    }

    /// Full body with the marker removed, for the reader UI. Nothing is lost —
    /// the two sides are simply rejoined.
    public string FullText
    {
        get
        {
            int i = BreakIndex;
            if (i < 0) return contentText ?? string.Empty;
            return FrontText + "\n" + BackText;
        }
    }

    private int BreakIndex
        => string.IsNullOrEmpty(contentText) ? -1 : contentText.IndexOf(PageBreak, StringComparison.Ordinal);
}
