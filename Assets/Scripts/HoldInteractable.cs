public abstract class HoldInteractable : BaseInteractable
{
    public abstract float HoldDuration { get; }
    public virtual  bool  CanHold         => true;
    public virtual  void  OnHoldComplete()  { }
    public virtual  void  OnHoldCancelled() { }
}
