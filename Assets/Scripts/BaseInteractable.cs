using UnityEngine;

[RequireComponent(typeof(Outline))]
public class BaseInteractable : MonoBehaviour, IInteractable
{
    private Outline outline;
    protected bool canInteract = true;
    protected virtual void Awake()
    {
        outline = GetComponent<Outline>();
        outline.enabled = false;
    }
    
    public void Interact()
    {
        if (!canInteract) return;
        Debug.Log("Interacted");
    }

    public virtual void OnHoverEnter()
    {
        Outline(true);
    }

    public virtual void OnHoverExit()
    {
        Outline(false);
    }
    
    void Outline(bool enabled)
    {
        outline.enabled = enabled;
    }
}
