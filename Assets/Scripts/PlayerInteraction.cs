using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] float interactDistance = 3f;
    [SerializeField] LayerMask interactLayer;
    [SerializeField] Camera cam;

    IInteractable current;

    void Update()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsNoteOpen)
        {
            if (current != null) { current.OnHoverExit(); current = null; }
            if (Input.GetKeyDown(KeyCode.E)) UIManager.Instance.HideNote();
            return;
        }

        if (ItemInspectionController.Instance != null && ItemInspectionController.Instance.IsInspecting)
        {
            if (current != null)
            {
                current.OnHoverExit();
                current = null;
            }
            return;
        }

        HandleHover();

        var inspection = ItemInspectionController.Instance;
        bool inspectionJustStopped = inspection != null && inspection.LastStopFrame == Time.frameCount;

        if (Input.GetKeyDown(KeyCode.E) && current != null && !inspectionJustStopped)
            current.Interact();
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