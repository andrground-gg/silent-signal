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
    
    public override void OnHoverEnter()
    {
        if (data == null)
        {
            Debug.LogError("No Collectible data assigned.");
        }
        UIManager.Instance?.ShowBoardCollectible(data, transform);
        base.OnHoverEnter();
    }

    public override void OnHoverExit()
    {
        UIManager.Instance?.HideBoardCollectible();
        base.OnHoverExit();
    }
}
