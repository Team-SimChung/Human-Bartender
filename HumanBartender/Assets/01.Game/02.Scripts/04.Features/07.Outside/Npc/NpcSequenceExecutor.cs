using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>한 NPC 연출 목록의 동작과 좌표를 실행한다. 대화 수명과 진행 상태는 호출자가 관리한다.</summary>
internal sealed class NpcSequenceExecutor
{
    private readonly Transform owner;
    private readonly NpcActor actor;
    private readonly Transform player;
    private readonly Vector3 playerSessionStart;
    private readonly IReadOnlyDictionary<NpcActor, Vector3> initialPositions;
    private readonly NpcSceneStaging staging;
    private readonly ISceneFadeService fades;
    private readonly IPopupService popups;
    private readonly ISoundEffectPlayer sound;
    private bool dark;
    private float recoveryFade;

    internal NpcSequenceExecutor(Transform owner, NpcActor actor, Transform player, Vector3 playerSessionStart,
        IReadOnlyDictionary<NpcActor, Vector3> initialPositions, NpcSceneStaging staging,
        ISceneFadeService fades, IPopupService popups, ISoundEffectPlayer sound)
    {
        this.owner = owner;
        this.actor = actor;
        this.player = player;
        this.playerSessionStart = playerSessionStart;
        this.initialPositions = initialPositions;
        this.staging = staging;
        this.fades = fades;
        this.popups = popups;
        this.sound = sound;
    }

    internal async UniTask ExecuteAsync(NpcActionStep[] actions, CancellationTokenSource source)
    {
        foreach (var step in actions)
        {
            source.Token.ThrowIfCancellationRequested();
            await ExecuteStepAsync(step, source);
        }
        source.Token.ThrowIfCancellationRequested();
    }

    /// <summary>병렬 작업 취소 이후 호출한다. 복구가 끝나기 전에는 대화 실행 잠금을 풀지 않는다.</summary>
    internal UniTask RecoverScreenAsync()
    {
        return dark && fades != null
            ? fades.FadeInAsync(recoveryFade, CancellationToken.None)
            : UniTask.CompletedTask;
    }

    private async UniTask ExecuteStepAsync(NpcActionStep step, CancellationTokenSource source)
    {
        var performer = step.performer != null ? step.performer : actor;
        switch (step.kind)
        {
            case NpcActionKind.MoveBy:
                await performer.MoveToAsync(performer.transform.position + new Vector3(step.offset.x, step.offset.y), source.Token);
                break;
            case NpcActionKind.Approach:
                await ApproachAsync(step, performer, source.Token);
                break;
            case NpcActionKind.Face:
                performer.FaceDuringConversation(ResolveTarget(step));
                break;
            case NpcActionKind.Animation:
                await performer.PlayAnimationAsync(step.stateName, step.returnState, step.duration, source.Token);
                break;
            case NpcActionKind.Wait:
                await UniTask.Delay(TimeSpan.FromSeconds(step.duration), cancellationToken: source.Token);
                break;
            case NpcActionKind.ScreenFadeOut:
                RequireFades();
                recoveryFade = step.duration;
                await fades.FadeOutAsync(step.duration, source.Token);
                dark = true;
                break;
            case NpcActionKind.ScreenFadeIn:
                RequireFades();
                recoveryFade = step.duration;
                await fades.FadeInAsync(step.duration, source.Token);
                dark = false;
                break;
            case NpcActionKind.Depart:
                await performer.DepartAsync(ResolveOffset(step, performer.transform), step.duration, source.Token);
                break;
            case NpcActionKind.ObjectivePopup:
                if (popups == null) throw new InvalidOperationException("NPC 연출의 목표 팝업 서비스가 없습니다.");
                await popups.Popup(PopupType.ObjectiveCompleted, step.text);
                break;
            case NpcActionKind.Show:
                performer.SetVisible(true);
                break;
            case NpcActionKind.Hide:
                performer.SetVisible(false);
                break;
            case NpcActionKind.MoveTo:
                await MoveActorAsync(step, performer, source);
                break;
            case NpcActionKind.SetPosition:
                performer.SetPosition(ResolvePosition(step, performer));
                break;
            case NpcActionKind.FadeOut:
                await performer.FadeOutAsync(step.duration, source.Token);
                break;
            case NpcActionKind.Sound:
                if (sound == null) throw new InvalidOperationException("NPC 연출의 효과음 서비스가 없습니다.");
                sound.PlaySE(step.text);
                break;
            case NpcActionKind.DepartWhileMoving:
                await performer.DepartWhileMovingAsync(ResolveOffset(step, performer.transform),
                    step.walkDuration, step.duration, step.fadeEase, source.Token);
                break;
            case NpcActionKind.MoveToAndFade:
                await DepartToAsync(step, performer, source.Token);
                break;
            case NpcActionKind.ArcMove:
                await performer.MoveArcToAsync(ResolvePosition(step, performer), step.duration, step.arcHeight, source.Token);
                break;
            case NpcActionKind.MovePlayer:
                await MovePlayerAsync(step, source);
                break;
            case NpcActionKind.SetPlayerPosition:
                RequireStaging();
                staging.SetPlayerPosition(ResolvePosition(step, player, playerSessionStart));
                break;
            case NpcActionKind.FacePlayer:
                RequireStaging();
                staging.FacePlayer(ResolveTarget(step).position + (Vector3)step.offset);
                break;
            case NpcActionKind.CameraFocus:
            case NpcActionKind.CameraPan:
            case NpcActionKind.CameraSet:
                await MoveCameraAsync(step, performer, source);
                break;
            case NpcActionKind.CameraRestore:
                RequireStaging();
                await staging.RestoreCameraAsync(step.duration, source.Token);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(step.kind));
        }
    }

    private UniTask ApproachAsync(NpcActionStep step, NpcActor performer, CancellationToken token)
    {
        var destination = performer.transform.position;
        float deltaX = ResolveTarget(step).position.x - destination.x;
        destination.x += Mathf.Sign(deltaX) * Mathf.Max(0, Mathf.Abs(deltaX) - step.distance);
        return performer.MoveToAsync(destination, token);
    }

    private async UniTask MoveActorAsync(NpcActionStep step, NpcActor performer, CancellationTokenSource source)
    {
        if (step.cameraWithMovement) RequireStaging();
        var destination = ResolvePosition(step, performer);
        var movement = performer.MoveToAsync(destination, MoveDuration(step, performer.transform.position, destination),
            step.movementEase, step.keepFacing, source.Token);
        if (step.cameraWithMovement)
        {
            var center = performer.transform.position;
            if (step.cameraMidpoint) center.x = (center.x + player.position.x) * .5f;
            center += (Vector3)step.cameraOffset;
            await RunTogether(source, movement, staging.FocusCameraAsync(center, step.cameraWidth, 0, source.Token));
        }
        else await movement;
    }

    private async UniTask DepartToAsync(NpcActionStep step, NpcActor performer, CancellationToken token)
    {
        var destination = ResolvePosition(step, performer);
        float totalTime = step.unitsPerSecond > 0
            ? Vector3.Distance(performer.transform.position, destination) / step.unitsPerSecond
            : step.walkDuration + step.duration;
        float fadeTime = Mathf.Min(totalTime, step.duration);
        if (totalTime > .0001f)
            await performer.DepartWhileMovingAsync(destination - performer.transform.position,
                totalTime - fadeTime, fadeTime, step.fadeEase, token);
        else performer.SetVisible(false);
    }

    private async UniTask MovePlayerAsync(NpcActionStep step, CancellationTokenSource source)
    {
        RequireStaging();
        var destination = ResolvePosition(step, player, playerSessionStart);
        if (step.clampPlayerLeft) destination.x = Mathf.Min(player.position.x, destination.x);
        var movement = staging.MovePlayerAsync(destination, step.duration, step.movementEase, source.Token, step.keepFacing);
        if (step.cameraWithMovement)
        {
            var center = ResolveTarget(step).position;
            if (step.cameraMidpoint) center.x = (center.x + destination.x) * .5f;
            center += (Vector3)step.cameraOffset;
            await RunTogether(source, movement, staging.FocusCameraAsync(center, step.cameraWidth, step.duration, source.Token));
        }
        else await movement;
    }

    private async UniTask MoveCameraAsync(NpcActionStep step, NpcActor performer, CancellationTokenSource source)
    {
        RequireStaging();
        var center = ResolvePosition(step, performer);
        if (step.cameraMidpoint) center.x = (center.x + player.position.x) * .5f;
        if (step.kind == NpcActionKind.CameraSet)
            staging.SetCameraFrame(center, step.cameraWidth);
        else if (step.kind == NpcActionKind.CameraFocus)
            await staging.FocusCameraAsync(center, step.cameraWidth, step.duration, source.Token);
        else if (step.fadeInTogether > 0)
        {
            RequireFades();
            recoveryFade = step.fadeInTogether;
            await RunTogether(source,
                staging.PanCameraAsync(center, step.cameraWidth, step.duration, step.movementEase, source.Token),
                fades.FadeInAsync(step.fadeInTogether, source.Token));
            dark = false;
        }
        else await staging.PanCameraAsync(center, step.cameraWidth, step.duration, step.movementEase, source.Token);
    }

    private static async UniTask RunTogether(CancellationTokenSource source, UniTask first, UniTask second)
    {
        // 한쪽 실패 시 둘 다 취소하고, 두 작업의 finally가 끝난 다음 화면·자세를 복구한다.
        var errors = await UniTask.WhenAll(new[] { FinishAsync(first, source), FinishAsync(second, source) });
        Exception failure = null;
        foreach (var error in errors)
        {
            if (error == null) continue;
            if (failure == null) failure = error;
            if (error is OperationCanceledException) continue;
            failure = error;
            break;
        }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static async UniTask<Exception> FinishAsync(UniTask task, CancellationTokenSource source)
    {
        try
        {
            await task;
            return null;
        }
        catch (Exception error)
        {
            source.Cancel();
            return error;
        }
    }

    private Transform ResolveTarget(NpcActionStep step)
    {
        var value = step.target != null ? step.target : player;
        if (value == null || value.gameObject.scene != owner.gameObject.scene)
            throw new InvalidOperationException("접근·바라보기 대상이 현재 씬에 없습니다.");
        return value;
    }

    private Vector3 ResolvePosition(NpcActionStep step, NpcActor performer)
        => ResolvePosition(step, performer.transform, initialPositions[performer]);

    private Vector3 ResolvePosition(NpcActionStep step, Transform subject, Vector3 initial)
    {
        var origin = step.positionOrigin switch
        {
            NpcPositionOrigin.Current => subject.position,
            NpcPositionOrigin.Initial => initial,
            NpcPositionOrigin.Target => ResolveTarget(step).position,
            NpcPositionOrigin.World => new Vector3(0, 0, subject.position.z),
            _ => throw new ArgumentOutOfRangeException(nameof(step.positionOrigin))
        };
        var position = origin + (Vector3)ResolveOffset(step, subject);
        if (step.horizontalOnly)
        {
            position.y = subject.position.y;
            position.z = subject.position.z;
        }
        return position;
    }

    private Vector2 ResolveOffset(NpcActionStep step, Transform subject)
    {
        var offset = step.offset;
        if (step.offsetMode != NpcOffsetMode.Fixed)
        {
            float delta = subject.position.x - ResolveTarget(step).position.x;
            offset.x = Mathf.Abs(offset.x) * (delta < 0 ? -1 : 1);
        }
        return offset;
    }

    private static float MoveDuration(NpcActionStep step, Vector3 from, Vector3 to) =>
        step.unitsPerSecond > 0 ? Mathf.Max(.0001f, Vector3.Distance(from, to) / step.unitsPerSecond) : step.duration;

    private void RequireStaging()
    {
        if (staging == null || player == null)
            throw new InvalidOperationException("플레이어·카메라 연출에는 Npc Scene Staging을 연결하세요: " + owner.name);
    }

    private void RequireFades()
    {
        if (fades == null) throw new InvalidOperationException("NPC 연출의 화면 페이드 서비스가 없습니다.");
    }
}
