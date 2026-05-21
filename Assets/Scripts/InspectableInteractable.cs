using UnityEngine;

public class InspectableInteractable : BaseInteractable
{
    [SerializeField] private GameObject inspectionPrefab;
    [SerializeField] private CollectibleData collectibleData;

    [Header("Spawn Settings")]
    [SerializeField] private Vector3 spawnRotation;

    public override void Interact()
    {
        if (!canInteract || inspectionPrefab == null) return;

        ItemInspectionController.Instance.StartInspection(inspectionPrefab, Quaternion.Euler(spawnRotation), collectibleData);

        if (collectibleData != null)
            CollectibleRegistry.Instance?.MarkDiscovered(collectibleData);

        base.Interact();
    }
}
