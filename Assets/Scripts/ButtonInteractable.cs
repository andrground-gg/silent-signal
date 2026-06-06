using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Interactable button that pulses its material between the default look and a
/// "red" attract state while it is waiting to be pressed. Once pressed it stops
/// blinking and switches to the "green" pressed material.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class ButtonInteractable : BaseInteractable
{
    [Header("Button Materials")]
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private Material blinkMaterial;    // red — the "press me" pulse
    [SerializeField] private Material pressedMaterial;  // green — stays after press

    [Header("Blink")]
    [SerializeField] private float blinkInterval = 0.5f;

    [Tooltip("Renderer whose material is swapped. Defaults to this object's Renderer.")]
    [SerializeField] private Renderer targetRenderer;

    [Header("Press Movement")]
    [Tooltip("Transform that slides when pressed. Defaults to this object's transform.")]
    [SerializeField] private Transform moveTarget;

    [Tooltip("Local-space offset the button travels when pressed.")]
    [SerializeField] private Vector3 pressOffset = new Vector3(0f, -0.5f, 0f);

    [SerializeField] private float moveDuration = 0.15f;
    [SerializeField] private Ease moveEase = Ease.OutBack;

    [Header("Condition")]
    [Tooltip("Optional gate. If set and not met, the press springs back and raises OnFailure.")]
    [SerializeField] private ButtonCondition condition;

    [Tooltip("How long the button takes to spring back when the condition fails.")]
    [SerializeField] private float failReturnDuration = 0.15f;
    [SerializeField] private Ease failReturnEase = Ease.OutBack;

    public event Action OnPressed;
    public event Action OnFailure;

    private Coroutine _blinkRoutine;
    private Tween _moveTween;
    private Vector3 _restLocalPos;
    private bool _pressed;

    protected override void Awake()
    {
        base.Awake();
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        if (moveTarget == null) moveTarget = transform;
        _restLocalPos = moveTarget.localPosition;
        StartBlinking();
    }

    private void StartBlinking()
    {
        if (_blinkRoutine != null) StopCoroutine(_blinkRoutine);
        _blinkRoutine = StartCoroutine(Blink());
    }

    private IEnumerator Blink()
    {
        bool red = false;
        while (!_pressed)
        {
            if (targetRenderer != null)
                targetRenderer.material = red ? blinkMaterial : defaultMaterial;
            red = !red;
            yield return new WaitForSeconds(blinkInterval);
        }
    }

    public override void Interact()
    {
        if (!canInteract || _pressed) return;

        // Condition not met: push in, spring back, and report failure.
        if (condition != null && !condition.IsMet())
        {
            PlayFailure();
            return;
        }

        _pressed = true;
        if (_blinkRoutine != null) StopCoroutine(_blinkRoutine);
        if (targetRenderer != null && pressedMaterial != null)
            targetRenderer.material = pressedMaterial;

        _moveTween?.Kill();
        _moveTween = moveTarget
            .DOLocalMove(_restLocalPos + LocalOffset(), moveDuration)
            .SetEase(moveEase);

        base.Interact();
        OnPressed?.Invoke();
    }

    private void PlayFailure()
    {
        // Press in, then return to rest — the button stays usable and keeps blinking.
        _moveTween?.Kill();
        _moveTween = DOTween.Sequence()
            .Append(moveTarget.DOLocalMove(_restLocalPos + LocalOffset(), moveDuration).SetEase(moveEase))
            .Append(moveTarget.DOLocalMove(_restLocalPos, failReturnDuration).SetEase(failReturnEase))
            .OnComplete(() => OnFailure?.Invoke());
    }

    // localPosition lives in the PARENT's space, not the button's own axes.
    // Rotate the offset by the button's local rotation so it travels along its
    // own right/up/forward even when the object is rotated.
    private Vector3 LocalOffset() => moveTarget.localRotation * pressOffset;

    /// <summary>
    /// Debug helper: returns the button to its un-pressed state — back at rest,
    /// default material, blinking again — so it can be tested repeatedly.
    /// </summary>
    public void ResetButton()
    {
        _moveTween?.Kill();
        _pressed = false;

        if (moveTarget == null) moveTarget = transform;
        moveTarget.localPosition = _restLocalPos;

        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        if (targetRenderer != null && defaultMaterial != null)
            targetRenderer.material = defaultMaterial;

        if (Application.isPlaying) StartBlinking();
    }

#if UNITY_EDITOR
    [ContextMenu("Reset Button")]
    private void EditorReset() => ResetButton();
#endif
}
