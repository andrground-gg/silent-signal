using UnityEngine;

/// <summary>
/// Base "action" that gates a <see cref="ButtonInteractable"/> press. Attach a
/// concrete subclass next to the button and reference it: the button only
/// commits the press when <see cref="IsMet"/> returns true, otherwise it
/// springs back and raises OnFailure.
/// </summary>
public abstract class ButtonCondition : MonoBehaviour
{
    public abstract bool IsMet();
}
