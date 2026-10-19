using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;
using HumanBartender.CutsceneStudio;

/// <summary>CSV 컷씬 ID를 씬에 바인딩된 Timeline 또는 Addressables로 재생함.</summary>
public class OustideTimelineManager : MonoBehaviour, IOutsideTimeliner
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private CutsceneDialogueHandler handler;
    [SerializeField] private NewCutSceneDataSO cutsceneData;
    [SerializeField] private List<TimelineAsset> sceneTimelines = new();
    [Inject] private IConditionUtil conditions;
    private readonly TimelinePlayback playback = new();
    private CancellationTokenSource request;

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

    private void OnDisable() { request?.Cancel(); playback.Stop(); }
    public void PlayTimelineCutScene(string id) => PlayTimelineCutSceneAsync(id).Forget(Report);
    public void PlayTimelineCutScene(TimelineAsset asset)
    {
        if (asset == null) throw new ArgumentNullException(nameof(asset));
        PlayRequestAsync(null, asset, default).Forget(Report);
    }

    public UniTask PlayTimelineCutSceneAsync(string id, CancellationToken token = default) =>
        PlayRequestAsync(id, null, token);

    private async UniTask PlayRequestAsync(string id, TimelineAsset boundAsset, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (request != null) throw new InvalidOperationException("An Outside cutscene is already running.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        request = source;
        AsyncOperationHandle<TimelineAsset>? handle = null;
        try
        {
            if (boundAsset == null && await StudioPlaybackService.TryPlayAsync(this, id, source.Token)) return;
            await NewDataLoadManager.WaitUntilLoadedAsync(source.Token);
            if (boundAsset != null)
                id = FindBoundTimelineId(boundAsset);
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

    private string FindBoundTimelineId(TimelineAsset asset)
    {
        string id = null;
        int count = 0;
        if (cutsceneData != null && cutsceneData.cutSceneData != null)
            foreach (var candidate in cutsceneData.cutSceneData)
                if (candidate.Kind == ENewCutSceneKind.Timeline && candidate.ResourceKey == asset.name)
                {
                    id = candidate.Id;
                    count++;
                }
        if (count != 1)
            throw new InvalidOperationException("A directly bound Timeline must have one CSV ID: " + asset.name);
        return id;
    }

    private async UniTask PlayAssetAsync(TimelineAsset asset, string dialogueId, CancellationToken token)
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        if (handler != null) await handler.BeginTimelineAsync(dialogueId, conditions);
        try { await playback.PlayAsync(director, asset, source.Token); }
        finally { if (handler != null) await handler.StopAsync(); }
        if (handler != null && handler.LastError != null)
            throw new InvalidOperationException("Timeline dialogue failed.", handler.LastError);
    }

    public void OnTriggerEnding() => SceneTransitionManager.Instance.LoadScene("TempEnding");
    private static void Report(Exception e) { if (e is not OperationCanceledException) Debug.LogException(e); }
}
