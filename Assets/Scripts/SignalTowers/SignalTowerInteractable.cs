using UnityEngine;

public class SignalTowerInteractable : BaseInteractable
{
    [SerializeField] private SignalTower tower;

    private bool _isHovering;

    private void Start()
    {
        tower.OnCanReflectChanged += _ =>
        {
            canInteract = !tower.IsRotating;
            if (tower.IsRotating && _isHovering)
                base.OnHoverExit();
            else if (!tower.IsRotating && _isHovering)
                base.OnHoverEnter();
        };
    }

    public override void Interact()
    {
        base.Interact();
        tower.EnterControlMode();
    }

    [ContextMenu("Rotate Tower")]
    private void RotateTowerFromEditor()
    {
        tower.TryRotate();
    }

    public override void OnHoverEnter()
    {
        _isHovering = true;
        if (!tower.IsRotating)
            base.OnHoverEnter();
    }

    public override void OnHoverExit()
    {
        _isHovering = false;
        base.OnHoverExit();
    }
}
