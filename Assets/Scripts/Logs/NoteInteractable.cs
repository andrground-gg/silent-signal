using UnityEngine;

/// <summary>
/// World-placed readable note. Player interacts -> opens reader UI.
/// Note can be reread at the same physical location (per design doc).
/// </summary>
public class NoteInteractable : BaseInteractable
{
    [SerializeField] private NoteData data;

    public override void Interact()
    {
        if (!canInteract || data == null) return;

        // Open reader first so the UI is visible immediately.
        // NoteReaderUI.Instance?.Show(data);

        UIManager.Instance.ShowNote(data);
        CollectibleRegistry.Instance?.MarkDiscovered(data);
        
        base.Interact();
        // Mark discovered + fire board hook only on FIRST discovery.
        // var registry = CollectibleRegistry.Instance;
        // if (registry != null && registry.MarkDiscovered(data))
        // {
        //     InvestigationBoard.Instance?.UnlockNode(data.boardUpdateID);
        // }
    }
}
