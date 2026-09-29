using UnityEngine;
using UnityEngine.Playables;

/// <summary>RootMove 트랙 믹서. 활성 클립의 이동 데이터를 읽어 CutSceneRoot·BGRoot의 앵커 위치를 제어한다.</summary>
public class CutSceneRootMoveMixerBehaviour : PlayableBehaviour
{
    // Root와 BGRoot의 Hold 상태를 독립적으로 기억
    private bool hasHeldRoot;
    private Vector2 heldRootPos;

    private bool hasHeldBGRoot;
    private Vector2 heldBGRootPos;

    // 이전 프레임에서 활성이었던 클립 추적
    private int lastActiveIndex = -1;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var manager = playerData as CutSceneTimelineManager;
        if (manager == null) return;

        int inputCount = playable.GetInputCount();
        int currentActiveIndex = -1;

        bool isRootAnimating = false;
        bool isBGRootAnimating = false;

        // 활성 클립 찾기 + manager 연결 및 애니메이션 상태 추적
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;

            currentActiveIndex = i;

            var inputPlayable = (ScriptPlayable<CutSceneRootMoveBehaviour>)playable.GetInput(i);
            var behaviour = inputPlayable.GetBehaviour();
            behaviour.manager = manager;

            if (behaviour.isBGRoot) isBGRootAnimating = true;
            else isRootAnimating = true;
        }

        // 클립이 방금 끝났는지 감지
        if (lastActiveIndex >= 0 && currentActiveIndex < 0)
        {
            var prevPlayable = (ScriptPlayable<CutSceneRootMoveBehaviour>)playable.GetInput(lastActiveIndex);
            var prevBehaviour = prevPlayable.GetBehaviour();
            RectTransform prevTargetRect = prevBehaviour.GetTargetRect();

            switch (prevBehaviour.endMode)
            {
                case ERootMoveEndMode.Hold:
                    if (prevBehaviour.isBGRoot)
                    {
                        hasHeldBGRoot = true;
                        heldBGRootPos = prevBehaviour.EndPos;
                    }
                    else
                    {
                        hasHeldRoot = true;
                        heldRootPos = prevBehaviour.EndPos;
                    }
                    break;

                case ERootMoveEndMode.Return:
                    if (prevBehaviour.isBGRoot) hasHeldBGRoot = false;
                    else hasHeldRoot = false;

                    if (prevTargetRect != null)
                        prevTargetRect.anchoredPosition = prevBehaviour.StartPos;
                    break;
            }
        }

        // 현재 클립에서 애니메이션 중이 아닌 Root들에 대해서만 Hold 상태 적용
        if (!isRootAnimating && hasHeldRoot && manager.CutSceneRoot != null)
        {
            manager.CutSceneRoot.anchoredPosition = heldRootPos;
        }

        if (!isBGRootAnimating && hasHeldBGRoot && manager.CutSceneBGRoot != null)
        {
            manager.CutSceneBGRoot.anchoredPosition = heldBGRootPos;
        }

        lastActiveIndex = currentActiveIndex;
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        hasHeldRoot = false;
        hasHeldBGRoot = false;
        lastActiveIndex = -1;
    }
}