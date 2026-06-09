using System;
using UnityEngine;
using UnityEngine.UI;

// Shared signal-tower control menu. A tower shows it on interact and listens for
// its Rotate / Exit events. Keep this component on an always-active object and
// toggle the child `root`, otherwise the singleton would never initialize.
public class ControlCanvas : Singleton<ControlCanvas>
{
    [Tooltip("Visual content to show/hide (the buttons panel).")]
    [SerializeField] private GameObject root;

    [Header("Buttons")]
    [SerializeField] private Button rotateButton;
    [SerializeField] private Button exitButton;

    public event Action OnRotate;
    public event Action OnExit;

    private bool _isShown;

    protected override void Awake()
    {
        base.Awake();

        if (rotateButton != null) rotateButton.onClick.AddListener(() => OnRotate?.Invoke());
        if (exitButton   != null) exitButton.onClick.AddListener(() => OnExit?.Invoke());

        if (root != null) root.SetActive(false);
    }

    public void Show()
    {
        if (_isShown) return;
        _isShown = true;
        if (root != null) root.SetActive(true);
        CursorManager.RequestUI();
    }

    public void Hide()
    {
        if (!_isShown) return;
        _isShown = false;
        if (root != null) root.SetActive(false);
        CursorManager.ReleaseUI();
    }
}
