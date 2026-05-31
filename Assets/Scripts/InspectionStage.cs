using UnityEngine;

// Lives inside the additive inspection scene. Exposes the stage camera and the
// anchor where inspected items are placed, so ItemInspectionController can find
// them after the scene loads.
public class InspectionStage : MonoBehaviour
{
    public static InspectionStage Instance { get; private set; }

    [SerializeField] private Camera    stageCamera;
    [SerializeField] private Transform itemAnchor;

    public Camera    StageCamera => stageCamera;
    public Transform ItemAnchor  => itemAnchor;

    private void Awake()  => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
