using UnityEngine;
using UnityEngine.UI;

public class HoldInteractionUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Image      fillImage;
    [SerializeField] private float      startFill         = 0.3f;
    [SerializeField] private float      referenceDistance = 2f;

    private Camera  _cam;
    private Vector3 _initialScale;

    private void Awake()
    {
        _cam          = Camera.main;
        _initialScale = transform.localScale;
        root.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!root.activeSelf || _cam == null) return;
        transform.forward = _cam.transform.forward;

        float dist = Vector3.Distance(transform.position, _cam.transform.position);
        transform.localScale = _initialScale * (dist / referenceDistance);
    }

    public void Show(Vector3 worldPosition)
    {
        transform.position   = worldPosition;
        transform.forward    = _cam != null ? _cam.transform.forward : Vector3.forward;
        fillImage.fillAmount = startFill;
        root.SetActive(true);
    }

    public void SetProgress(float t)
    {
        fillImage.fillAmount = Mathf.Lerp(startFill, 1f, t);
    }

    public void Hide() => root.SetActive(false);
}
