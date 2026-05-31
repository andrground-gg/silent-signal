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
    
    public virtual void Interact()
    {
        Debug.Log("Try to interact");
        if (!canInteract) return;
        Debug.Log("Interacted");
    }

    public virtual void OnHoverEnter()
    {
        Outline(true);
        Debug.Log("OnHoverEnter");
    }

    public virtual void OnHoverExit()
    {
        Outline(false);
        Debug.Log("OnHoverExit");
    }
    
    void Outline(bool enabled)
    {
        outline.enabled = enabled;
    }

    public void SetHighlight(bool on)
    {
        if (outline != null) outline.enabled = on;
    }
}
