using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>외부·홈의 기존 자동·수동 스토리 실행 경로. 주변 대사는 별도 시스템에서 처리함.</summary>
public sealed class OutsideStoryFlow : MonoBehaviour
{
    [Inject] private InteractiveEntityManager entities;
    [Inject] private IConditionUtil conditions;
    [Inject] private IPopupService popups;
    private readonly System.Collections.Generic.HashSet<string> notifiedEvents = new();
    private DialogueRunner runner;
    private OutsideDialoguePresenter presenter;
    private Player player;
    private WorldInteractionController worldInteractions;
    private bool ready;
    private bool busy;
    private string error;
    NewSceneData? retryScene;

    public bool IsRunning => busy;
    public bool IsReady => ready;

    private async void Start()
    {
        try
        {
            var token = this.GetCancellationTokenOnDestroy();
            await NewDataLoadManager.WaitUntilLoadedAsync(token);
            if (!await entities.WaitUntilReadyAsync()) throw new InvalidOperationException(entities.InitializationError);
            runner = entities.DialogueRunner;
            presenter = entities.DialoguePresenter;
            player = entities.StoryPlayer;
            worldInteractions = WorldInteractionController.ForScene(gameObject.scene);
            if (runner == null || presenter == null || player == null)
                throw new InvalidOperationException("스토리 대화 UI 또는 플레이어가 연결되지 않았습니다.");
            ready = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception failure) { error = failure.Message; Debug.LogException(failure); }
        finally { busy = false; }
    }

    private NewSceneData[] Scenes => gameObject.scene.name == "Home"
        ? NewDataLoadManager.HomeStory.Scenes : NewDataLoadManager.StreetStory.Scenes;

    private bool Eligible(NewSceneData scene)
    {
        var state = GameStateManager.Instance;
        return WorldInteractionSceneRules.IsEligible(scene, gameObject.scene.name,
            state.CurrentDay, state.GameFlow, conditions);
    }

    private bool Near(NewSceneData scene) => Vector2.Distance(player.transform.position, new Vector2(scene.X, scene.Y)) <= scene.Radius;

    private void Update()
    {
        if (!ready || busy || error != null || runner.IsRunning || player.State != EInteractorState.None) return;
        foreach (var scene in Scenes ?? Array.Empty<NewSceneData>())
            if (scene.StartMode == ENewSceneTrigger.Auto && Eligible(scene) && Near(scene))
            { PlayAsync(scene, this.GetCancellationTokenOnDestroy()).Forget(ReportFailure); break; }
    }

    public async UniTask PlayNightAsync(CancellationToken token)
    {
        if (!ready || busy || runner.IsRunning) throw new InvalidOperationException("집 대화가 아직 준비되지 않았습니다.");
        if (gameObject.scene.name != "Home" || GameStateManager.Instance.GameFlow != EGameFlow.CommuteOut)
            throw new InvalidOperationException("귀가 후 집에서만 테라스 대화를 시작할 수 있습니다.");
        foreach (var scene in (Scenes ?? Array.Empty<NewSceneData>()).OrderBy(s => s.Seq))
            if (scene.StartMode == ENewSceneTrigger.Manual && Eligible(scene)) await PlayAsync(scene, token);
    }

    private async UniTask PlayAsync(NewSceneData scene, CancellationToken token)
    {
        if (busy || runner.IsRunning) throw new InvalidOperationException("다른 대화가 진행 중입니다.");
        busy = true;
        retryScene = scene;
        using var lease = InteractionStateLease.Acquire(player, EInteractorState.Interct);
        try
        {
            if (scene.StartMode == ENewSceneTrigger.Auto && notifiedEvents.Add(scene.Id))
                await popups.Popup(PopupType.EventOccurred, "");
            presenter.playMode = EActivationMode.Interact;
            runner.Bind(presenter);
            StoryExecutionResult result = await runner.PlayOutsideAsync(scene.Steps, token);
            if (result.Status == StoryExecutionStatus.Cancelled) throw new OperationCanceledException(token);
            if (!result.Completed) throw new InvalidOperationException(result.Error);
            if (!string.IsNullOrEmpty(scene.CompletionFlag)) conditions.ApplyRequired("flag." + scene.CompletionFlag + " = true");
            retryScene = null;
        }
        finally { busy = false; }
    }

    private void ReportFailure(Exception failure)
    {
        if (failure is OperationCanceledException) return;
        error = failure.Message;
        Debug.LogException(failure);
    }

    private void OnGUI()
    {
        if (!ready) return;
        if (worldInteractions != null && worldInteractions.IsRunning) return;
        if (runner.IsRunning && runner.CurrentState != DialogueState.WaitingForChoice)
        {
            if (GUI.Button(new Rect(Screen.width - 145, Screen.height - 65, 130, 45), "다음 ▶")) runner.AdvanceInputOutside();
            return;
        }
        if (error != null)
        {
            GUI.Box(new Rect(20, 20, 450, 110), "대화 진행 오류");
            GUI.Label(new Rect(30, 48, 430, 40), error);
            if (retryScene.HasValue && GUI.Button(new Rect(30, 90, 150, 30), "대화 다시 시도"))
            { var scene = retryScene.Value; error = null; PlayAsync(scene, this.GetCancellationTokenOnDestroy()).Forget(ReportFailure); }
            return;
        }
        if (busy || runner.IsRunning || player.State != EInteractorState.None || Camera.main == null) return;
        foreach (var scene in Scenes ?? Array.Empty<NewSceneData>())
        {
            if (worldInteractions != null && worldInteractions.OwnsStory(scene.Id)) continue;
            if (scene.StartMode != ENewSceneTrigger.Interact || !Eligible(scene) || !Near(scene)) continue;
            Vector3 p = Camera.main.WorldToScreenPoint(new Vector3(scene.X, scene.Y + .8f));
            if (p.z < 0) continue;
            if (GUI.Button(new Rect(p.x - 110, Screen.height - p.y - 36, 220, 36), scene.Title))
            { PlayAsync(scene, this.GetCancellationTokenOnDestroy()).Forget(ReportFailure); break; }
        }
    }
}
