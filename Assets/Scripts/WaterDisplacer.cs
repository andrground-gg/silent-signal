using UnityEngine;

/// <summary>
/// Attach this to any GameObject that should displace the water surface.
/// The WaterDisplacementController on the water plane will pick it up automatically.
/// </summary>
public class WaterDisplacer : MonoBehaviour
{
    [Tooltip("Radius of influence on the water surface. " +
             "Typically match this to your collider's approximate XZ extent.")]
    [Min(0.01f)] public float radius = 0.5f;

    [Tooltip("If true, auto-register/unregister with the nearest WaterDisplacementController.")]
    public bool autoRegister = true;

    WaterDisplacementController _controller;

    void OnEnable()
    {
        if (!autoRegister) return;
        _controller = FindAnyObjectByType<WaterDisplacementController>();
        _controller?.Register(this);
    }

    void OnDisable()
    {
        if (!autoRegister) return;
        _controller?.Unregister(this);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
