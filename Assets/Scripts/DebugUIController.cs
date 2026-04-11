using UnityEngine;

public class DebugUIController : MonoBehaviour
{
    [Header("UI Root")]
    public GameObject debugUI;

    [Header("Settings")]
    public KeyCode toggleKey = KeyCode.F1;
    public bool startEnabled = false;

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