using UnityEngine;

public class InspectableInteractable : BaseInteractable
{
    [SerializeField] private GameObject inspectionPrefab;
    [SerializeField] private CollectibleData collectibleData;
    [SerializeField] private InspectableContent inspectableContent;

    [Header("Spawn Settings")]
    [SerializeField] private Vector3 spawnRotation;
    [SerializeField] private float spawnDistance = 0f; // 0 = use stage anchor

    public override void Interact()
    {
        if (!canInteract || inspectionPrefab == null) return;

        // Render the data onto the item itself (e.g. a note's text on its pages).
        if (inspectableContent != null)
            inspectableContent.Apply(collectibleData);

        // Mark discovered up front, but show the discovery text only after the
        // player leaves inspection so it isn't hidden behind the inspection view.
        bool firstDiscovery = collectibleData != null
            && (CollectibleRegistry.Instance?.MarkDiscovered(collectibleData) ?? false);

        ItemInspectionController.Instance.StartInspection(inspectionPrefab, Quaternion.Euler(spawnRotation), collectibleData, spawnDistance, firstDiscovery);

        base.Interact();
    }
}
