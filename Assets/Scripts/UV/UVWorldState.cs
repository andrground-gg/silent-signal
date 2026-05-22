using System;

public class UVWorldState : Singleton<UVWorldState>
{
    public bool IsUVActive { get; private set; }
    public event Action<bool> OnUVStateChanged;

    public void SetUVActive(bool active)
    {
        if (IsUVActive == active) return;
        IsUVActive = active;
        OnUVStateChanged?.Invoke(active);
    }

    public void Toggle() => SetUVActive(!IsUVActive);
}
