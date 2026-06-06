using TMPro;
using UnityEngine;

/// <summary>
/// Renders a note's text onto its two sides so it can be read by rotating the
/// note during inspection. Both labels are always shown — no page separator.
/// </summary>
public class NoteInspectableContent : InspectableContent
{
    [SerializeField] private TMP_Text frontText;
    [SerializeField] private TMP_Text backText;

    public override void Apply(CollectibleData data)
    {
        if (!(data is NoteData note)) return;

        if (frontText != null) frontText.text = note.FrontText;
        if (backText != null)  backText.text  = note.BackText;
    }
}
