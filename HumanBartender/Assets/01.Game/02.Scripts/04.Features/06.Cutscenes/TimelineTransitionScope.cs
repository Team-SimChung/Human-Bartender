using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>같은 화면 대상의 이전 전환과 Timeline 종료 후 작업을 취소한다.</summary>
public sealed class TimelineTransitionScope
{
    readonly Dictionary<UnityEngine.Object, CancellationTokenSource> active = new();

    public async UniTask RunAsync(UnityEngine.Object target, CutSceneTimelineManager manager,
        Func<CancellationToken, UniTask> transition)
    {
        if (active.TryGetValue(target, out var previous)) previous.Cancel();
        using var source = CancellationTokenSource.CreateLinkedTokenSource(manager.PlaybackToken, manager.GetCancellationTokenOnDestroy());
        active[target] = source;
        try
        {
            source.Token.ThrowIfCancellationRequested();
            await transition(source.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { if (manager != null) manager.FailPlayback(e); }
        finally { if (active.TryGetValue(target, out var current) && ReferenceEquals(current, source)) active.Remove(target); }
    }

    public void CancelAll()
    {
        foreach (var source in active.Values.ToArray()) source.Cancel();
    }
}
