using System;
using UnityEngine;

/// <summary>웹 연출을 처음 적용하거나 명시적으로 재적용할 때 사용하는 공통 기본값이다. 실행 중인 씬 설정을 읽거나 덮어쓰지 않는다.</summary>
internal static class WebNpcSequenceDefaults
{
    internal const float DepartureDistance = .845f;

    internal static NpcActionStep CreateDeparture(NpcActor actor, float offsetX)
    {
        return new NpcActionStep
        {
            kind = NpcActionKind.DepartWhileMoving, performer = actor, offset = new Vector2(offsetX, 0),
            // 웹은 0.65/초로 1초 이동한 뒤, 마지막 0.3초 페이드 중에도 같은 속도로 이동한다.
            walkDuration = 1, duration = .3f, fadeEase = NpcMotionEase.SmoothStep
        };
    }

    internal static NpcSequence[] CreateContractSequences(NpcActor samho, NpcActor dealer)
    {
        if (samho == null) throw new ArgumentNullException(nameof(samho));
        if (dealer == null) throw new ArgumentNullException(nameof(dealer));
        return new[]
        {
            Command("samho-approach", OutsideStoryWorldAction.NotionApproach,
                new NpcActionStep
                {
                    kind = NpcActionKind.MoveTo, performer = samho, positionOrigin = NpcPositionOrigin.Target,
                    offset = new Vector2(-.75f, 0), horizontalOnly = true, duration = .95f,
                    movementEase = NpcMotionEase.SmoothStep, cameraWithMovement = true, cameraMidpoint = true,
                    cameraOffset = new Vector2(0, -.05f - samho.transform.position.y)
                }, new NpcActionStep { kind = NpcActionKind.Face, performer = samho }),
            Command("dealer-exit", OutsideStoryWorldAction.NotionDealerExit, CreateDeparture(dealer, -DepartureDistance)),
            Command("samho-exit", OutsideStoryWorldAction.NotionSamhoExit, CreateDeparture(samho, DepartureDistance))
        };
    }

    private static NpcSequence Command(string id, OutsideStoryWorldAction command, params NpcActionStep[] actions)
    {
        return new NpcSequence { id = id, trigger = NpcSequenceTrigger.WorldAction, worldAction = command, actions = actions };
    }
}
