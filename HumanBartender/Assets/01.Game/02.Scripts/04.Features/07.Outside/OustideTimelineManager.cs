using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;

/// <summary>CSV 컷신 ID를 씬에 바인딩된 Timeline 또는 Addressables로 재생한다.</summary>
public class OustideTimelineManager : MonoBehaviour, IOutsideTimeliner
{
    [SerializeField] PlayableDirector director;
    [SerializeField] CutsceneDialogueHandler handler;
    [SerializeField] NewCutSceneDataSO cutsceneData;
    [SerializeField] List<TimelineAsset> sceneTimelines = new();
    [Inject] IConditionUtil conditions;
    readonly TimelinePlayback playback = new();
    CancellationTokenSource request;

    public bool TryGetSceneTimeline(string resourceKey, out TimelineAsset asset)
    {
        asset = null;
        foreach (var candidate in sceneTimelines)
            if (candidate != null && candidate.name == resourceKey)
            {
                if (asset != null) throw new InvalidOperationException("Duplicate scene Timeline name: " + resourceKey);
                asset = candidate;
            }
        return asset != null;
    }

    void OnDisable() { request?.Cancel(); playback.Stop(); }
    public void PlayTimelineCutScene(string id) => PlayTimelineCutSceneAsync(id).Forget(Report);
    public void PlayTimelineCutScene(TimelineAsset asset)
    {
        if (asset == null) throw new ArgumentNullException(nameof(asset));
        PlayRequestAsync(null, asset, default).Forget(Report);
    }

    public UniTask PlayTimelineCutSceneAsync(string id, CancellationToken token = default) =>
        PlayRequestAsync(id, null, token);

    async UniTask PlayRequestAsync(string id, TimelineAsset boundAsset, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (request != null) throw new InvalidOperationException("An Outside cutscene is already running.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        request = source;
        AsyncOperationHandle<TimelineAsset>? handle = null;
        try
        {
            await NewDataLoadManager.WaitUntilLoadedAsync(source.Token);
            if (boundAsset != null)
            {
                var matches = (cutsceneData != null ? cutsceneData.cutSceneData : null)?
                    .Where(c => c.Kind == ENewCutSceneKind.Timeline && c.ResourceKey == boundAsset.name).ToArray();
                if (matches == null || matches.Length != 1)
                    throw new InvalidOperationException("A directly bound Timeline must have one CSV ID: " + boundAsset.name);
                id = matches[0].Id;
            }
            if (cutsceneData == null || !cutsceneData.TryGet(id, out var data) || data.Kind != ENewCutSceneKind.Timeline)
                throw new InvalidOperationException("Missing CSV Timeline: " + id);
            var asset = boundAsset;
            if (asset == null && !TryGetSceneTimeline(data.ResourceKey, out asset))
            {
                handle = await ResourceLoader.TryLoadAsync<TimelineAsset>(data.ResourceKey, source.Token);
                if (!handle.HasValue) throw new InvalidOperationException("Missing Timeline resource: " + data.ResourceKey);
                asset = handle.Value.Result;
            }
            await PlayAssetAsync(asset, id, source.Token);
        }
        finally
        {
            ResourceLoader.ReleaseHandle<TimelineAsset>(ref handle);
            if (ReferenceEquals(request, source)) request = null;
        }
    }

    async UniTask PlayAssetAsync(TimelineAsset asset, string dialogueId, CancellationToken token)
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        if (handler != null) await handler.BeginTimelineAsync(dialogueId, conditions);
        try { await playback.PlayAsync(director, asset, source.Token); }
        finally { if (handler != null) await handler.StopAsync(); }
        if (handler != null)
        {
            if (handler.LastError != null) throw new InvalidOperationException("Timeline dialogue failed.", handler.LastError);
        }
    }

    public void OnTriggerEnding() => SceneTransitionManager.Instance.LoadScene("TempEnding");
    static void Report(Exception e) { if (e is not OperationCanceledException) Debug.LogException(e); }
}
