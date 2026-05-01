using UnityEngine;

public class NoteInteractionZone : InteractionZone
{
    protected override void HandleExited(Collider other)
    {
        UIManager.Instance.HideNote();
        base.HandleExited(other);
    }
}
