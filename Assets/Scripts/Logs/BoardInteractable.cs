using UnityEngine;

/// <summary>
/// Interactable piece pinned to the investigation board (photo, note, marker, etc).
/// Holds a reference to the CollectibleData it represents.
/// Data is injected at runtime via SetData() — typically by InvestigationBoard
/// when it spawns/reveals the piece.
/// </summary>
public class BoardInteractable : BaseInteractable
{
    [SerializeField] private CollectibleData data;

    public CollectibleData Data => data;

    public void SetData(CollectibleData newData)
    {
        data = newData;
    }

    public override void Interact()
    {
        if (!canInteract) return;
        if (data == null)
        {
            Debug.LogWarning($"[BoardInteractable] No data assigned on '{name}'.", this);
            return;
        }

        // TODO: route by type. For now — placeholder.
        // Note  -> UIManager.Instance?.ShowNote((NoteData)data);
        // Audio -> UIManager.Instance?.ShowAudioLog((AudioLogData)data);
        Debug.Log($"[BoardInteractable] Interacted with '{data.title}' (type: {data.Type}).");

        base.Interact();
    }
}
