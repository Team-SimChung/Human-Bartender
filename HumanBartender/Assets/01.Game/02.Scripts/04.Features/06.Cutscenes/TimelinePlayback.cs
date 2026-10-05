using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>Timeline 재생부터 실제 종료·취소 정리까지 관리한다.</summary>
public sealed class TimelinePlayback
{
    CancellationTokenSource active;
    public bool IsPlaying => active != null;
    public void Stop() => active?.Cancel();

    public async UniTask PlayAsync(PlayableDirector director, TimelineAsset timeline, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (director == null || timeline == null) throw new InvalidOperationException("Timeline director or asset is missing.");
        if (IsPlaying) throw new InvalidOperationException("A timeline is already playing; stop and await it first.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token);
        active = source;
        var stopped = new UniTaskCompletionSource();
        var previousAsset = director.playableAsset;
        var previousWrapMode = director.extrapolationMode;
        void OnStopped(PlayableDirector value) { if (value == director) stopped.TrySetResult(); }
        director.stopped += OnStopped;
        try
        {
            director.playableAsset = timeline;
            director.extrapolationMode = DirectorWrapMode.None;
            director.time = 0;
            director.Play();
            // 일시정지 시간을 포함하므로 길이 대신 실제 종료를 기다린다.
            await stopped.Task.AttachExternalCancellation(source.Token);
            // stopped 콜백 뒤 Unity가 그래프/트랙 정리를 마친 다음 완료한다.
            await UniTask.Yield(source.Token);
            source.Token.ThrowIfCancellationRequested();
        }
        finally
        {
            if (director != null)
            {
                director.stopped -= OnStopped;
                director.Stop();
                director.playableAsset = previousAsset;
                director.extrapolationMode = previousWrapMode;
            }
            if (ReferenceEquals(active, source)) active = null;
        }
    }
}
