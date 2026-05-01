using System;
using UnityEngine;

public class InteractionZone : MonoBehaviour
{
    [Header("Zone")]
    [SerializeField] Collider zoneCollider;

    [Header("Detection")]
    [SerializeField] string tag = "Player";

    public event Action OnEnteredInteractionZone;
    public event Action OnExitedInteractionZone;

    protected virtual void Reset()
    {
        zoneCollider = GetComponent<Collider>();

        if (zoneCollider != null)
            zoneCollider.isTrigger = true;
    }

    protected virtual void Awake()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<Collider>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(tag)) return;

        HandleEntered(other);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(tag)) return;

        HandleExited(other);
    }

    protected virtual void HandleEntered(Collider other)
    {
        OnEnteredInteractionZone?.Invoke();
    }

    protected virtual void HandleExited(Collider other)
    {
        OnExitedInteractionZone?.Invoke();
    }

}