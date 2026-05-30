using UnityEngine;

public enum SolarPanelActionType
{
    CaveOpening,
    ResonanceEmitterUnlock,
}

public abstract class SolarPanelAction : MonoBehaviour
{
    public abstract SolarPanelActionType ActionType { get; }
    public abstract void Execute();
}
