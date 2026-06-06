using UnityEngine;

/// <summary>
/// Base "action" that pushes collectible data onto the inspectable object itself.
/// Attach a concrete subclass (e.g. <see cref="NoteInspectableContent"/>) to an
/// inspection prefab. ItemInspectionController calls <see cref="Apply"/> right
/// after the object spawns, so the data can be rendered on world-space canvases.
/// </summary>
public abstract class InspectableContent : MonoBehaviour
{
    public abstract void Apply(CollectibleData data);
}
