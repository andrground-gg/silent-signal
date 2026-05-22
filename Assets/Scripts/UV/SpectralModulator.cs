using UnityEngine;

/// <summary>
/// Spectral Modulator — lever that drives UV world state.
/// Pull = UV on, release = UV off.
/// All animation and audio come from Lever base class.
/// </summary>
public class SpectralModulator : Lever
{
    protected override void Awake()
    {
        base.Awake();
        OnPulled  += () => UVWorldState.Instance.SetUVActive(true);
        OnRelease += () => UVWorldState.Instance.SetUVActive(false);
    }
}
