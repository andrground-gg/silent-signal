using UnityEngine;

public class ResonanceEmitterUnlockAction : SolarPanelAction
{
    [SerializeField] private ResonanceEmitter emitter;

    public override SolarPanelActionType ActionType => SolarPanelActionType.ResonanceEmitterUnlock;

    public override void Execute()
    {
        emitter?.Unlock();
    }
}
