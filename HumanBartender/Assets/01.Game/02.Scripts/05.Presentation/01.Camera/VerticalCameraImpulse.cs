using Unity.Cinemachine;
using UnityEngine;

/// <summary>Adds one vertical hit after camera framing without changing its lens.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineCamera))]
public sealed class VerticalCameraImpulse : CinemachineExtension, ICameraImpulse
{
    [Tooltip("World units. 0.04 is four source pixels at the Play scene's 100 pixels per unit.")]
    [SerializeField, Min(0f)] float amplitude = 0.04f;
    [SerializeField, Min(0.001f)] float duration = 0.11f;

    float startedAt;
    bool playing;

    public void PlayVerticalPulse()
    {
        if (!isActiveAndEnabled || amplitude <= 0f || duration <= 0f) return;
        float now = CinemachineCore.CurrentTime;
        // Closely spaced spans share the existing pulse instead of stacking or restarting it.
        if (playing && now >= startedAt && now - startedAt < duration) return;
        startedAt = now;
        playing = true;
    }

    public void StopImpulse() => playing = false;

    void OnDisable() => StopImpulse();

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Noise || vcam != ComponentOwner) return;
        if (deltaTime < 0f) { StopImpulse(); return; }
        if (!playing || !isActiveAndEnabled) return;

        float elapsed = CinemachineCore.CurrentTime - startedAt;
        if (elapsed < 0f || elapsed >= duration || duration <= 0f)
        {
            StopImpulse();
            return;
        }

        // A short held hit, a one-quarter-strength counter hit, then an abrupt return.
        // Absolute time keeps repeated Cinemachine evaluations in the same frame identical.
        float t = elapsed / duration;
        float offset = t < 0.3f ? amplitude : t < 0.65f ? -amplitude * 0.25f : 0f;
        state.PositionCorrection += Vector3.up * offset;
    }
}
