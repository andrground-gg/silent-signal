using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float     interactDistance = 3f;
    [SerializeField] private LayerMask interactLayer;
    [SerializeField] private Camera    cam;

    private IInteractable     _current;
    private float             _holdProgress;
    private HoldInteractionUI _holdUI;
    private Vector3           _hitPoint;

    void Update()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsNoteOpen)
        {
            CancelHold();
            if (_current != null) { _current.OnHoverExit(); _current = null; }
            if (Input.GetKeyDown(KeyCode.E)) UIManager.Instance.HideNote();
            return;
        }

        if (ItemInspectionController.Instance != null && ItemInspectionController.Instance.IsInspecting)
        {
            CancelHold();
            if (_current != null) { _current.OnHoverExit(); _current = null; }
            return;
        }

        HandleHover();

        if (_current == null) return;

        var inspection = ItemInspectionController.Instance;
        bool inspectionJustStopped = inspection != null && inspection.LastStopFrame == Time.frameCount;
        if (inspectionJustStopped) return;

        if (_current is HoldInteractable ha)
        {
            if (ha.CanHold) HandleHold(ha);
        }
        else if (Input.GetKeyDown(KeyCode.E))
            _current.Interact();
    }

    void HandleHold(HoldInteractable target)
    {
        if (Input.GetKey(KeyCode.E))
        {
            if (_holdProgress == 0f) _holdUI?.Show(_hitPoint);

            _holdProgress = Mathf.MoveTowards(_holdProgress, 1f, Time.deltaTime / target.HoldDuration);
            _holdUI?.SetProgress(_holdProgress);

            if (_holdProgress >= 1f)
            {
                _holdProgress = 0f;
                _holdUI?.Hide();
                target.OnHoldComplete();
            }
        }
        else if (_holdProgress > 0f)
        {
            CancelHold();
        }
    }

    void CancelHold()
    {
        if (_holdProgress <= 0f) return;
        _holdProgress = 0f;
        _holdUI?.Hide();
        (_current as HoldInteractable)?.OnHoldCancelled();
    }

    void HandleHover()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit hit;

        IInteractable newInteractable = null;

        if (Physics.Raycast(ray, out hit, interactDistance, interactLayer))
        {
            newInteractable = hit.collider.GetComponentInParent<IInteractable>();
            _hitPoint = hit.point;
        }

        if (newInteractable != _current)
        {
            CancelHold();
            _current?.OnHoverExit();
            newInteractable?.OnHoverEnter();
            _current = newInteractable;
            _holdUI = (newInteractable as MonoBehaviour)?.GetComponentInChildren<HoldInteractionUI>(true);
        }
    }
}
