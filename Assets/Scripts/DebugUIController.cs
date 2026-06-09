using UnityEngine;
using UnityEngine.UI;

public class DebugUIController : MonoBehaviour
{
    [Header("UI Root")]
    public GameObject debugUI;

    [Header("Settings")]
    public KeyCode toggleKey = KeyCode.F1;
    public bool startEnabled = false;

    [Header("Collectibles")]
    [SerializeField] private InvestigationBoard board;
    [SerializeField] private KeyCode discoverAllKey = KeyCode.F2;
    [SerializeField] private Button discoverAllButton;

    [Header("Time Scale")]
    [SerializeField] private KeyCode timeScaleUpKey   = KeyCode.Equals;     // "+" / "="
    [SerializeField] private KeyCode timeScaleDownKey = KeyCode.Minus;      // "-"
    [SerializeField] private float   timeScaleStep    = 1f;
    [SerializeField] private float   maxTimeScale     = 10f;

    void Awake()
    {
        if (discoverAllButton != null)
            discoverAllButton.onClick.AddListener(DiscoverAll);
    }

    void OnDestroy()
    {
        if (discoverAllButton != null)
            discoverAllButton.onClick.RemoveListener(DiscoverAll);
    }

    void Start()
    {
        if (debugUI != null)
            debugUI.SetActive(startEnabled);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            Toggle();
        }

        // if (Input.GetKeyDown(discoverAllKey))
        //     DiscoverAll();

        if (Input.GetKeyDown(timeScaleUpKey) || Input.GetKeyDown(KeyCode.KeypadPlus))
            ChangeTimeScale(timeScaleStep);

        if (Input.GetKeyDown(timeScaleDownKey) || Input.GetKeyDown(KeyCode.KeypadMinus))
            ChangeTimeScale(-timeScaleStep);
    }

    private void ChangeTimeScale(float delta)
    {
        Time.timeScale = Mathf.Clamp(Time.timeScale + delta, 0f, maxTimeScale);
        Debug.Log($"[Debug] Time.timeScale = {Time.timeScale}");
    }

    public void DiscoverAll()
    {
        if (board != null)
            board.RevealAll();
        else
            CollectibleRegistry.Instance?.DiscoverAll();
    }

    public void Toggle()
    {
        if (debugUI == null) return;

        debugUI.SetActive(!debugUI.activeSelf);
    }

    public void Enable()
    {
        if (debugUI != null)
            debugUI.SetActive(true);
    }

    public void Disable()
    {
        if (debugUI != null)
            debugUI.SetActive(false);
    }
}