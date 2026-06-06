using UnityEngine;

/// <summary>
/// Button gate that passes only once the <see cref="ResonanceEmitter"/> is
/// unlocked. Attach next to a <see cref="ButtonInteractable"/> and reference it
/// on the button so it can't be pressed before the emitter is powered up.
/// </summary>
public class EmitterUnlockedCondition : ButtonCondition
{
    [SerializeField] private ResonanceEmitter emitter;

    public override bool IsMet() => emitter != null && emitter.IsUnlocked;
}
