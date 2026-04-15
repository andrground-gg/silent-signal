using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] float interactDistance = 3f;
    [SerializeField] LayerMask interactLayer;
    [SerializeField] Camera cam;

    IInteractable current;

    void Update()
    {
        HandleHover();

        if (Input.GetKeyDown(KeyCode.E) && current != null)
        {
            current.Interact();
        }
    }

    void HandleHover()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit hit;

        IInteractable newInteractable = null;

        if (Physics.Raycast(ray, out hit, interactDistance, interactLayer))
        {
            newInteractable = hit.collider.GetComponentInParent<IInteractable>();
        }

        if (newInteractable != current)
        {
            current?.OnHoverExit();
            newInteractable?.OnHoverEnter();
            current = newInteractable;
        }
    }
}