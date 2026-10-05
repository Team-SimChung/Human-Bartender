/// <summary>Transient camera feedback, independent of framing and zoom controls.</summary>
public interface ICameraImpulse
{
    void PlayVerticalPulse();
    void StopImpulse();
}
