using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ItemInspectionController : Singleton<ItemInspectionController>
{
    [SerializeField] private float rotationSpeed = 200f;
    [SerializeField] private float backgroundAlpha = 0.8f;
    [SerializeField] private int renderTextureSize = 600;

    [Header("Inspection Scene")]
    [SerializeField] private string inspectionSceneName = "InspectionScene";
    [SerializeField] private string inspectionLayer = "Inspection";

    [Header("Editor Canvas (optional)")]
    [SerializeField] private GameObject inspectionRoot;   // the object that holds the inspection UI; toggled on/off
    [SerializeField] private Canvas inspectionCanvas;
    [SerializeField] private RawImage itemDisplay;
    [SerializeField] private RenderTexture sharedRenderTexture; // assign the same RT used by ItemDisplay
    [SerializeField] private NoteReaderUI noteReaderUI;

    public bool IsInspecting { get; private set; }
    public int LastStopFrame { get; private set; }

    private PlayerController _player;
    private GameObject _instance;
    private RenderTexture _rt;
    private Canvas _canvas;
    private int _startFrame;

    private InspectionStage _stage;
    private Scene _inspectionScene;
    private Scene _previousActiveScene;
    private bool _pendingDiscovery;

    protected override void Awake()
    {
        base.Awake();
        SetupRenderTexture();
        BuildUI();
    }

    void Start()
    {
        _player = FindObjectOfType<PlayerController>();
        StartCoroutine(LoadInspectionScene());
    }

    IEnumerator LoadInspectionScene()
    {
        if (SceneManager.GetSceneByName(inspectionSceneName).isLoaded)
        {
            _inspectionScene = SceneManager.GetSceneByName(inspectionSceneName);
        }
        else
        {
            yield return SceneManager.LoadSceneAsync(inspectionSceneName, LoadSceneMode.Additive);
            _inspectionScene = SceneManager.GetSceneByName(inspectionSceneName);
        }

        _stage = InspectionStage.Instance;
        if (_stage != null && _stage.StageCamera != null)
        {
            _stage.StageCamera.targetTexture = _rt;
            _stage.StageCamera.enabled = false;
        }
        else
        {
            Debug.LogWarning($"[Inspection] Scene '{inspectionSceneName}' loaded but no InspectionStage found.");
        }
    }

    void SetupRenderTexture()
    {
        if (sharedRenderTexture != null)
        {
            _rt = sharedRenderTexture;
            return;
        }

        _rt = new RenderTexture(renderTextureSize, renderTextureSize, 24, RenderTextureFormat.ARGB32);
        _rt.antiAliasing = 2;
    }

    void SetUIVisible(bool visible)
    {
        // Prefer an explicit root object; otherwise toggle the canvas's own GameObject
        // so children (background, item display) are fully hidden, not just unrendered.
        if (inspectionRoot != null)
            inspectionRoot.SetActive(visible);
        else if (_canvas != null)
            _canvas.gameObject.SetActive(visible);
    }

    void BuildUI()
    {
        if (inspectionCanvas != null)
        {
            _canvas = inspectionCanvas;
            if (itemDisplay != null)
                itemDisplay.texture = _rt;
            SetUIVisible(false);
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

        SetUIVisible(false);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    public void StartInspection(GameObject prefab, Quaternion rotation = default, CollectibleData data = null, float spawnDistance = 0f, bool showDiscoveryOnExit = false)
    {
        if (IsInspecting || prefab == null) return;
        if (_stage == null || _stage.StageCamera == null)
        {
            Debug.LogWarning("[Inspection] Stage not ready — inspection scene still loading?");
            return;
        }

        // spawnDistance > 0 places the item along the camera's forward at that distance;
        // otherwise the item sits exactly on the anchor.
        Transform anchor = _stage.ItemAnchor != null ? _stage.ItemAnchor : _stage.StageCamera.transform;
        Vector3 spawnPos = spawnDistance > 0f
            ? _stage.StageCamera.transform.position + _stage.StageCamera.transform.forward * spawnDistance
            : anchor.position;

        _instance = Instantiate(prefab, spawnPos, rotation);
        SceneManager.MoveGameObjectToScene(_instance, _inspectionScene);
        SetLayerRecursively(_instance, LayerMask.NameToLayer(inspectionLayer));

        foreach (var col in _instance.GetComponentsInChildren<Collider>())
            col.enabled = false;
        foreach (var r in _instance.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = ShadowCastingMode.Off;

        _stage.StageCamera.enabled = true;
        SetUIVisible(true);

        // Drive ambient/skybox/fog from the inspection scene while inspecting.
        _previousActiveScene = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(_inspectionScene);

        // Record the frame so that the same E keydown that triggered Interact()
        // does not immediately close the inspection in the same frame.
        _startFrame = Time.frameCount;

        IsInspecting = true;
        _pendingDiscovery = showDiscoveryOnExit;
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
            _instance.transform.Rotate(_stage.StageCamera.transform.up, -dx, Space.World);
            _instance.transform.Rotate(_stage.StageCamera.transform.right, dy, Space.World);
        }

        if (Input.GetKeyDown(KeyCode.E) && Time.frameCount > _startFrame)
            StopInspection();
    }

    public void StopInspection()
    {
        if (!IsInspecting) return;

        if (_previousActiveScene.IsValid())
            SceneManager.SetActiveScene(_previousActiveScene);

        Destroy(_instance);
        _instance = null;
        if (_stage != null && _stage.StageCamera != null)
            _stage.StageCamera.enabled = false;
        SetUIVisible(false);
        IsInspecting = false;
        LastStopFrame = Time.frameCount;
        _player.IsInspecting = false;
        CursorManager.ReleaseUI();

        // Surface the discovery text now that the inspection view is gone.
        if (_pendingDiscovery)
        {
            _pendingDiscovery = false;
            UIManager.Instance?.ShowDiscoveryText();
        }
    }

    void OnDestroy()
    {
        // Only release a RT we created ourselves; leave the shared asset alone.
        if (_rt != null && _rt != sharedRenderTexture) _rt.Release();
    }
}
