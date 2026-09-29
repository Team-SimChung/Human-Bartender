using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

/// <summary>Resolution 트랙 믹서. 활성 클립의 해상도 설정을 읽어 CutSceneTimelineManager의 CanvasScaler에 적용한다.</summary>
public class CutSceneResolutionMixerBehaviour : PlayableBehaviour
{
    private CutSceneTimelineManager manager;
    readonly TimelineTransitionScope transitions = new();
    private Vector2 originalResolution;
    private float originalMatch;
    private bool originalSaved;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        manager = playerData as CutSceneTimelineManager;
        if (manager == null || manager.CanvasScaler == null) return;

        CanvasScaler scaler = manager.CanvasScaler;

        // 원본 해상도 백업 (최초 1회)
        if (!originalSaved)
        {
            originalSaved = true;
            originalResolution = scaler.referenceResolution;
            originalMatch = scaler.matchWidthOrHeight;
        }

        int inputCount = playable.GetInputCount();

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            var inputPlayable = (ScriptPlayable<CutSceneResolutionBehaviour>)playable.GetInput(i);
            var behaviour = inputPlayable.GetBehaviour();
            behaviour.manager = manager;

            if (weight > 0f && !behaviour.isActive)
            {
                // ── 클립 시작 ─────────────────────────────────────────
                behaviour.isActive = true;
                transitions.CancelAll();
                behaviour.previousResolution = scaler.referenceResolution;
                behaviour.previousMatch = scaler.matchWidthOrHeight;

                if (behaviour.transition && behaviour.transitionDuration > 0)
                {
                    transitions.RunAsync(scaler, manager, token => TransitionResolution(
                        scaler,
                        behaviour.previousResolution,
                        behaviour.resolution,
                        behaviour.previousMatch,
                        behaviour.matchWidthOrHeight,
                        behaviour.transitionDuration,
                        behaviour.transitionEase, token
                    )).Forget();
                }
                else
                {
                    scaler.referenceResolution = behaviour.resolution;
                    scaler.matchWidthOrHeight = behaviour.matchWidthOrHeight;
                }

                behaviour.applied = true;
            }
            else if (weight <= 0f && behaviour.isActive)
            {
                // ── 클립 종료 ─────────────────────────────────────────
                behaviour.isActive = false;

                // 다음 클립이 없으면 원본으로 복구
                if (!HasActiveClip(playable, i))
                {
                    transitions.CancelAll();
                    if (behaviour.transition && behaviour.transitionDuration > 0)
                    {
                        transitions.RunAsync(scaler, manager, token => TransitionResolution(
                            scaler,
                            scaler.referenceResolution,
                            originalResolution,
                            scaler.matchWidthOrHeight,
                            originalMatch,
                            behaviour.transitionDuration,
                            behaviour.transitionEase, token
                        )).Forget();
                    }
                    else
                    {
                        scaler.referenceResolution = originalResolution;
                        scaler.matchWidthOrHeight = originalMatch;
                    }
                }
            }
        }
    }

    bool HasActiveClip(Playable playable, int exceptIndex)
    {
        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            if (i == exceptIndex) continue;
            if (playable.GetInputWeight(i) > 0f) return true;
        }
        return false;
    }

    async UniTask TransitionResolution(
        CanvasScaler scaler,
        Vector2 fromRes, Vector2 toRes,
        float fromMatch, float toMatch,
        float duration, Ease ease, CancellationToken token)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            token.ThrowIfCancellationRequested();
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = DOVirtual.EasedValue(0f, 1f, t, ease);

            scaler.referenceResolution = Vector2.Lerp(fromRes, toRes, easedT);
            scaler.matchWidthOrHeight = Mathf.Lerp(fromMatch, toMatch, easedT);

            await UniTask.Yield(token);
        }

        token.ThrowIfCancellationRequested();
        scaler.referenceResolution = toRes;
        scaler.matchWidthOrHeight = toMatch;
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        transitions.CancelAll();
        // Timeline 종료 시 원본 복구
        if (manager != null && manager.CanvasScaler != null && originalSaved)
        {
            manager.CanvasScaler.referenceResolution = originalResolution;
            manager.CanvasScaler.matchWidthOrHeight = originalMatch;
        }
    }
}
