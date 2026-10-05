using System;
using VContainer.Unity;

/// <summary>Scene-scoped mapping from dialogue cues to camera feedback.</summary>
public sealed class DialogueCameraFeedback : IInitializable, IDisposable
{
    readonly UIDialogueTextView view;
    readonly ICameraImpulse camera;

    public DialogueCameraFeedback(UIDialogueTextView view, ICameraImpulse camera)
    {
        this.view = view;
        this.camera = camera;
    }

    public void Initialize()
    {
        view.ShakeSpanRevealed += camera.PlayVerticalPulse;
        view.MotionStopped += camera.StopImpulse;
    }

    public void Dispose()
    {
        view.ShakeSpanRevealed -= camera.PlayVerticalPulse;
        view.MotionStopped -= camera.StopImpulse;
        camera.StopImpulse();
    }
}
