using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class ItemInspectionController : Singleton<ItemInspectionController>
{
    [SerializeField] private float rotationSpeed = 200f;
    [SerializeField] private float backgroundAlpha = 0.8f;
    [SerializeField] private int renderTextureSize = 600;
    [SerializeField] private float stageCameraDistance = 3f;
    [SerializeField] private float stageCameraFov = 40f;

    [Header("Editor Canvas (optional)")]
    [SerializeField] private Canvas inspectionCanvas;
    [SerializeField] private RawImage itemDisplay;
    [SerializeField] private NoteReaderUI noteReaderUI;

    public bool IsInspecting { get; private set; }
    public int LastStopFrame { get; private set; }

    // Item is spawned here — far from any actual geometry.
    private static readonly Vector3 StagePosition = new Vector3(10000f, 10000f, 10000f);

    private PlayerController _player;
    private GameObject _instance;
    private Camera _stageCamera;
    private RenderTexture _rt;
    private Canvas _canvas;
    private int _startFrame;

    protected override void Awake()
    {
        base.Awake();
        BuildStageCamera();
        BuildUI();
    }

    void Start()
    {
        _player = FindObjectOfType<PlayerController>();
    }

    void BuildStageCamera()
    {
        var go = new GameObject("_InspectionStageCamera");
        go.transform.SetParent(transform);
        // Sit behind the stage along -Z, look toward +Z at the item.
        go.transform.position = StagePosition - Vector3.forward * stageCameraDistance;

        _stageCamera = go.AddComponent<Camera>();
        _stageCamera.clearFlags = CameraClearFlags.SolidColor;
        _stageCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _stageCamera.fieldOfView = stageCameraFov;
        _stageCamera.nearClipPlane = 0.1f;
        _stageCamera.farClipPlane = stageCameraDistance * 4f;
        _stageCamera.enabled = false;

        _rt = new RenderTexture(renderTextureSize, renderTextureSize, 24, RenderTextureFormat.ARGB32);
        _rt.antiAliasing = 2;
        _stageCamera.targetTexture = _rt;
    }

    void BuildUI()
    {
        if (inspectionCanvas != null)
        {
            _canvas = inspectionCanvas;
            if (itemDisplay != null)
                itemDisplay.texture = _rt;
            _canvas.enabled = false;
            return;
        }

        var root = new GameObject("_InspectionCanvas");
        root.transform.SetParent(transform);

        _canvas = root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();

        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(root.transform, false);
        var bg = bgGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, backgroundAlpha);
        bg.raycastTarget = false;
        Stretch(bg.rectTransform);

        var displayGO = new GameObject("ItemDisplay");
        displayGO.transform.SetParent(root.transform, false);
        var raw = displayGO.AddComponent<RawImage>();
        raw.texture = _rt;
        var dr = raw.rectTransform;
        dr.anchorMin = dr.anchorMax = dr.pivot = new Vector2(0.5f, 0.5f);
        dr.sizeDelta = new Vector2(renderTextureSize, renderTextureSize);
        dr.anchoredPosition = Vector2.zero;

        _canvas.enabled = false;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    public void StartInspection(GameObject prefab, Quaternion rotation = default, CollectibleData data = null)
    {
        if (IsInspecting || prefab == null) return;

        _instance = Instantiate(prefab, StagePosition, rotation);
        foreach (var col in _instance.GetComponentsInChildren<Collider>())
            col.enabled = false;
        foreach (var r in _instance.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = ShadowCastingMode.Off;

        _stageCamera.enabled = true;
        _canvas.enabled = true;

        // Record the frame so that the same E keydown that triggered Interact()
        // does not immediately close the inspection in the same frame.
        _startFrame = Time.frameCount;

        IsInspecting = true;
        _player.IsInspecting = true;
        CursorManager.RequestUI();

        if (noteReaderUI != null && data is NoteData noteData)
            noteReaderUI.SetData(noteData);
    }

    void Update()
    {
        if (!IsInspecting) return;

        if (Input.GetMouseButton(0))
        {
            float dx = Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            float dy = Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
            _instance.transform.Rotate(_stageCamera.transform.up, -dx, Space.World);
            _instance.transform.Rotate(_stageCamera.transform.right, dy, Space.World);
        }

        if (Input.GetKeyDown(KeyCode.E) && Time.frameCount > _startFrame)
            StopInspection();
    }

    public void StopInspection()
    {
        if (!IsInspecting) return;

        Destroy(_instance);
        _instance = null;
        _stageCamera.enabled = false;
        _canvas.enabled = false;
        IsInspecting = false;
        LastStopFrame = Time.frameCount;
        _player.IsInspecting = false;
        CursorManager.ReleaseUI();
    }

    void OnDestroy()
    {
        if (_rt != null) _rt.Release();
    }
}
