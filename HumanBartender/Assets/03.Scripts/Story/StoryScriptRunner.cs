using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 2부 대본을 씬 단위로 실행한다(2부 운영 명세 §4·§6).
///
/// 공용 DialogueRunner를 쓰지 않는다. 그쪽은 id로 다음 대사를 가리키는 사슬을 걷고 이쪽은 seq로
/// 늘어선 스텝 배열을 걷는데, 한 실행기에 두 방식을 넣으면 순회 코드가 두 벌이 된다. 게다가 그 파일은
/// 거리의 NPC·오브젝트가 함께 쓴다. 대신 대사를 그리는 계층은 그대로 가져다 쓴다.
///
/// 이 클래스가 아는 것은 "무엇을 언제 실행할지"까지다. 그리는 일은 IStoryPresenter 너머로 넘긴다 —
/// 공용 대화 시스템이 바 화면과 실외 화면을 IDialoguePresenter로 갈라 놓은 것과 같은 이유다.
/// 대본을 걷는 방법은 어디서나 같지만 그리는 방법은 화면마다 다르다.
///
/// 지금은 say·enter·exit·choice·order·craft·serve·timeline을 실행하고 나머지 스텝은 기록만 남긴다.
/// </summary>
public class StoryScriptRunner : MonoBehaviour
{
    /// <summary>
    /// 플레이어. 바에서는 1인칭이라 초상을 세우지 않는데, 그 판단은 화면이 하고 여기서는 누구인지만 안다.
    /// </summary>
    const string PlayerCharacterId = "luna";

    IStoryPresenter presenter;
    IConditionUtil conditions;
    OrderRequestController orderController;
    UniTaskCompletionSource<OrderResult> orderResultSignal;

    /// <summary>
    /// 살아 있는 주문(§8.2의 current_order). order가 만들고 serve가 소비한다.
    ///
    /// 한 번에 하나만 든다. craft와 serve가 "직전 order"를 되짚는 대신 같은 객체를 보게 해서,
    /// 씬 제목이나 화면 가운데 인물로 서빙 대상을 추측하는 일이 생기지 않게 한다.
    /// </summary>
    OrderRequest currentOrder;

    /// <summary>대사를 넘기라는 입력을 기다리는 곳. 기다리는 중이 아니면 null이다.</summary>
    UniTaskCompletionSource advanceSignal;

    /// <summary>
    /// 지금 어느 자리에 누가 앉아 있는지. 명세가 2부 세션의 관리 대상으로 두는 값이라 여기서 든다
    /// (§3). 퇴장할 자리를 찾는 데 쓰고, 카메라 프레이밍이 활성 인원을 셀 때도 쓴다.
    /// </summary>
    readonly Dictionary<ESlotType, string> seatActors = new();

    /// <summary>좌석을 왼쪽부터 세는 순서. 화면에 넘길 자리 목록을 항상 같은 순서로 만들기 위해 둔다.</summary>
    static readonly ESlotType[] SeatOrder = { ESlotType.Left, ESlotType.Middle, ESlotType.Right };

    /// <summary>지금 대본을 돌고 있는지. 입력을 이쪽으로 보낼지 정하는 데 쓴다.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>지금 돌고 있는 대본. 선택지의 goto가 가리키는 씬을 여기서 찾는다.</summary>
    NewDayScriptBase script;

    /// <summary>그날의 연출을 재생하는 곳. timeline 스텝이 쓴다.</summary>
    ICutScenePlayer cutScenePlayer;

    /// <summary>화면과 평가기를 연결한다. RunAsync 전에 반드시 불러야 한다.</summary>
    public void Bind(IStoryPresenter storyPresenter, IConditionUtil conditionEvaluator,
                     OrderRequestController requests, ICutScenePlayer cutScenePlayerImpl)
    {
        presenter = storyPresenter;
        conditions = conditionEvaluator;
        orderController = requests;
        cutScenePlayer = cutScenePlayerImpl;
    }

    /// <summary>
    /// 그날의 바 대본을 끝까지 실행한다. 자동 씬이 하나도 없으면 아무것도 하지 않고 돌아온다.
    /// </summary>
    public StoryExecutionResult LastResult { get; private set; }

    public async UniTask<StoryExecutionResult> RunAsync(NewDayScriptBase dayScript, CancellationToken token)
    {
        if (IsRunning) return new StoryExecutionResult(StoryExecutionStatus.Failed, "Story is already running.");
        if (presenter == null || conditions == null)
            return new StoryExecutionResult(StoryExecutionStatus.Failed, "Story must be bound before running.");
        script = dayScript;
        conditions = conditions.CreateExecutionScope();
        IsRunning = true;
        try
        {
            token.ThrowIfCancellationRequested();
            var cursor = new StorySceneCursor(script, conditions);
            if (!cursor.IsEmpty)
            {
                await ApplyFramingAsync(token);
                while (cursor.TryTakeNext(out NewScriptSceneData scene))
                {
                    token.ThrowIfCancellationRequested();
                    if (await PlayFromAsync(scene, token)) break;
                }
            }
            token.ThrowIfCancellationRequested();
            LastResult = new StoryExecutionResult(StoryExecutionStatus.Completed);
        }
        catch (OperationCanceledException) { LastResult = new StoryExecutionResult(StoryExecutionStatus.Cancelled); }
        catch (Exception e) { LastResult = new StoryExecutionResult(StoryExecutionStatus.Failed, e.Message); }
        finally
        {
            advanceSignal = null;
            seatActors.Clear();
            var cleanupErrors = new List<string>();
            void Cleanup(Action action)
            {
                try { action(); }
                catch (Exception e) { cleanupErrors.Add(e.Message); }
            }
            if (currentOrder != null && orderController != null) Cleanup(() => orderController.CancelOrder(currentOrder.Id));
            orderResultSignal = null;
            if (IsAlive(presenter)) Cleanup(presenter.Clear);
            currentOrder = null;
            Cleanup(() => conditions.Result = null);
            IsRunning = false;
            if (cleanupErrors.Count > 0)
            {
                string detail = string.Join("; ", cleanupErrors);
                LastResult = new StoryExecutionResult(LastResult.Completed ? StoryExecutionStatus.Failed : LastResult.Status,
                    string.IsNullOrEmpty(LastResult.Error) ? "Cleanup: " + detail : LastResult.Error + "; Cleanup: " + detail);
            }
        }
        return LastResult;
    }

    static bool IsAlive(object target) => target != null && (target is not UnityEngine.Object obj || obj != null);

    /// <summary>
    /// 씬 하나를 재생하고, 선택지가 다른 씬을 가리키면 그쪽으로 이어 간다.
    ///
    /// goto로 옮겨 간 씬이 끝나면 원래 씬으로 돌아오지 않는다(§12.4). 돌아온다면 이미 고른 선택지
    /// 뒤의 대사를 다시 만나게 된다. 자동 씬 커서는 그다음 자리에서 이어진다.
    ///
    /// end_part를 만나면 true — 그날의 2부는 거기서 끝이다.
    /// </summary>
    async UniTask<bool> PlayFromAsync(NewScriptSceneData scene, CancellationToken token)
    {
        // 씬을 옮겨 다니는 데이터가 스스로를 가리키면 끝나지 않는다. 한 바퀴 안에서 지나온 씬을 세어
        // 지나치게 길어지면 멈춘다.
        var visited = new HashSet<string>();

        while (true)
        {
            if (!visited.Add(scene.Id))
            {
                throw new InvalidOperationException($"[Story] 씬 '{scene.Id}'로 되돌아왔습니다. goto가 고리를 이룹니다.");
                return false;
            }

            Debug.Log($"[Story] 씬 시작 — {scene.Id} (seq {scene.Seq})");

            (bool ended, string gotoSceneId) = await PlaySceneAsync(scene, token);

            Debug.Log($"[Story] 씬 종료 — {scene.Id}");

            if (ended) return true;
            if (gotoSceneId == null) return false;

            if (!TryFindScene(gotoSceneId, out scene))
            {
                // 없는 씬을 가리키면 아무 씬으로도 대신하지 않는다(§14 SCENE_REFERENCE_MISSING).
                throw new InvalidOperationException($"[Story] goto가 가리키는 씬 '{gotoSceneId}'을 찾지 못했습니다.");
                return false;
            }
        }
    }

    /// <summary>
    /// 씬 하나의 스텝을 seq 순으로 실행한다.
    /// end_part면 ended가 참이고, 선택지가 다른 씬을 가리켰으면 그 씬 id를 함께 돌려준다.
    /// </summary>
    async UniTask<(bool ended, string gotoSceneId)> PlaySceneAsync(NewScriptSceneData scene, CancellationToken token)
    {
        if (scene.Steps == null) return (false, null);

        foreach (var step in scene.Steps)
        {
            token.ThrowIfCancellationRequested();
            // 조건이 거짓인 스텝은 그것 하나만 건너뛴다. 씬 전체를 멈추지 않는다(§9.1).
            if (!conditions.CheckRequired(step.When)) continue;

            if (step.Type == ENewStepType.EndPart)
            {
                // 아직 내지 않은 잔이 남았는데 끝내면 그 주문은 영영 처리되지 않는다(§5 종료 금지 조건).
                // 대본이 잘못 적힌 것이므로 임의로 주문을 지우지 않고 알린 뒤 계속 간다.
                if (currentOrder != null && currentOrder.State != OrderState.Completed)
                {
                    throw new InvalidOperationException($"[Story] 처리하지 않은 주문({currentOrder.Details.ReceiverId} / " +
                                   $"{currentOrder.Details.CocktailId})이 남아 end_part를 받아들이지 않습니다: {scene.Id}");
                    continue;
                }

                Debug.Log($"[Story] end_part — {scene.Id}");
                return (true, null);
            }

            if (step.Type == ENewStepType.Choice)
            {
                string target = await PlayChoiceAsync(scene, step, token);

                // 선택지의 effects는 고른 항목의 것을 이미 적용했다. 스텝 자체의 effects는 그다음이다.
                token.ThrowIfCancellationRequested();
                conditions.ApplyRequired(step.Effects, $"{scene.Id}#{step.Seq}");

                if (target != null) return (false, target);

                continue;
            }

            await PlayStepAsync(scene, step, token);

            // effects는 스텝을 확정한 순간 한 번만. 같은 스텝을 두 번 지나도 값이 두 번 움직이지 않게
            // 씬과 seq를 합친 것을 표로 쓴다.
            token.ThrowIfCancellationRequested();
            conditions.ApplyRequired(step.Effects, $"{scene.Id}#{step.Seq}");
        }

        return (false, null);
    }

    bool TryFindScene(string sceneId, out NewScriptSceneData found)
    {
        foreach (var candidate in script.Scenes)
        {
            if (candidate.Id != sceneId) continue;

            found = candidate;
            return true;
        }

        found = default;
        return false;
    }

    async UniTask PlayStepAsync(NewScriptSceneData scene, NewDialogueStepData step, CancellationToken token)
    {
        switch (step.Type)
        {
            case ENewStepType.Say:
                await SayAsync(step, token);
                return;

            case ENewStepType.Enter:
                await EnterAsync(scene, step, token);
                return;

            case ENewStepType.Exit:
                await ExitAsync(step, token);
                return;

            case ENewStepType.Effect:
                // effects만 적용하는 스텝. 적용은 부르는 쪽이 이미 한다.
                return;

            case ENewStepType.Order:
                await OrderAsync(scene, step, token);
                return;

            case ENewStepType.Craft:
                await CraftAsync(scene, step, token);
                return;

            case ENewStepType.Serve:
                await ServeAsync(scene, step, token);
                return;

            case ENewStepType.Timeline:
                await TimelineAsync(scene, step, token);
                return;

            default:
                // 아직 붙이지 않은 스텝. 조용히 지나가면 대본이 어디까지 왔는지 알 수 없어 남긴다.
                throw new NotSupportedException($"Unsupported required step: {step.Type} ({scene.Id}#{step.Seq})");
                return;
        }
    }

    // ── 대사 ────────────────────────────────────────────────────────────

    // ── 선택지 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 선택지를 띄우고 고른 것을 확정한다. 고른 항목이 다른 씬을 가리키면 그 id를 돌려준다.
    ///
    /// 조건이 거짓인 항목도 목록에 남긴다 — 감추면 그런 선택지가 있었다는 사실 자체가 사라진다(§12.4).
    /// 고른 순간 그 항목의 effects를 한 번만 적용한다.
    /// </summary>
    async UniTask<string> PlayChoiceAsync(NewScriptSceneData scene, NewDialogueStepData step,
                                          CancellationToken token)
    {
        if (!TryGetChoices(step.Arg, out NewChoiceOptionData[] choices))
        {
            throw new InvalidOperationException($"[Story] 선택지 '{step.Arg}'를 대본에서 찾지 못했습니다: {scene.Id}#{step.Seq}");
            return null;
        }

        var options = new List<StoryChoiceOption>(choices.Length);
        bool anySelectable = false;

        foreach (var choice in choices)
        {
            bool selectable = conditions.CheckRequired(choice.When);
            anySelectable |= selectable;

            options.Add(new StoryChoiceOption(choice.Text?.Ko, selectable, choice.LockReason?.Ko));
        }

        // 고를 수 있는 것이 하나도 없으면 화면을 띄우지 않는다. 띄우면 플레이어가 빠져나올 길이 없다
        // (§14 NO_SELECTABLE_CHOICE).
        if (!anySelectable)
        {
            throw new InvalidOperationException($"[Story] 고를 수 있는 선택지가 하나도 없습니다: {step.Arg} ({scene.Id}#{step.Seq})");
            return null;
        }

        int picked = await presenter.ShowChoicesAsync(options, token);

        if (picked < 0 || picked >= choices.Length) throw new InvalidOperationException("No valid choice was selected.");

        NewChoiceOptionData chosen = choices[picked];

        token.ThrowIfCancellationRequested();
        if (!options[picked].IsSelectable) throw new InvalidOperationException("A locked choice was selected.");
        conditions.ApplyRequired(chosen.Effects, $"{scene.Id}#{step.Seq}#choice{picked}");

        Debug.Log($"[Story] 선택 — {step.Arg}[{picked}] \"{chosen.Text?.Ko}\"" +
                  (string.IsNullOrEmpty(chosen.Goto) ? "" : $" → {chosen.Goto}"));

        // goto가 비어 있는 것은 정상값이다. 그대로 다음 스텝으로 이어 간다(§12.4).
        return string.IsNullOrEmpty(chosen.Goto) ? null : chosen.Goto;
    }

    bool TryGetChoices(string choiceId, out NewChoiceOptionData[] choices)
    {
        choices = null;

        return !string.IsNullOrEmpty(choiceId) &&
               script.Choices != null &&
               script.Choices.TryGetValue(choiceId, out choices) &&
               choices != null && choices.Length > 0;
    }

    // ── 대사 ────────────────────────────────────────────────────────────

    /// <summary>대사 하나를 띄우고 플레이어가 넘길 때까지 기다린다. 그리는 일은 화면이 한다.</summary>
    async UniTask SayAsync(NewDialogueStepData step, CancellationToken token)
    {
        string body = step.Text?.Ko;

        if (string.IsNullOrEmpty(body))
        {
            throw new InvalidOperationException($"[Story] 본문이 없는 say입니다: {step.DialogueId ?? $"seq {step.Seq}"}");
            return;
        }

        await presenter.ShowSayAsync(
            new StorySayRequest(step.Actor, step.Arg, body, step.Actor == PlayerCharacterId), token);

        await WaitForAdvanceAsync(token);
    }

    // ── 연출 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 컷씬 하나를 재생하고 끝날 때까지 기다린다. arg가 json/cutscenes.json의 컷씬 id다.
    ///
    /// 끝나면 화면을 비운다(ClearCutScene). 안 비우면 컷씬 그림이 남은 채로 다음 대사가 시작돼,
    /// 대본상으로는 바로 돌아온 것인데 화면만 연출에 머문다.
    ///
    /// 필수 연출의 실패는 상위 실행 결과로 전파하고, effects는 적용하지 않는다.
    /// </summary>
    async UniTask TimelineAsync(NewScriptSceneData scene, NewDialogueStepData step, CancellationToken token)
    {
        if (string.IsNullOrEmpty(step.Arg))
        {
            throw new InvalidOperationException($"[Story] timeline 스텝에 컷씬 id(arg)가 없습니다: {scene.Id}#{step.Seq}");
            return;
        }

        if (cutScenePlayer == null)
        {
            throw new InvalidOperationException($"[Story] 컷씬 재생기가 없어 '{step.Arg}'를 건너뜁니다: {scene.Id}#{step.Seq}");
            return;
        }

        Debug.Log($"[Story] 컷씬 — {step.Arg} ({scene.Id}#{step.Seq})");

        token.ThrowIfCancellationRequested();

        try
        {
            await cutScenePlayer.PlayCutScene(step.Arg, token: token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"[Story] 컷씬 '{step.Arg}' 재생에 실패했습니다: {scene.Id}#{step.Seq}\n{e}");
        }
        finally
        {
            if (IsAlive(cutScenePlayer)) cutScenePlayer.ClearCutScene();
        }
    }

    // ── 주문·제조·서빙 ──────────────────────────────────────────────────
    //
    // 셋은 한 묶음이다(§8.2). order가 주문자와 정답 칵테일을 만들고, craft가 그 문맥으로 제조 화면을
    // 열고, serve가 주문자의 코스터에 실제로 놓인 결과를 확정한다. 문맥이 끊기면 어느 잔을 누구에게
    // 냈는지가 사라지므로, 앞이 없으면 뒤를 실행하지 않고 멈춘다.

    /// <summary>
    /// 주문을 만든다.
    ///
    /// 주문 문구를 먼저 다 출력한 뒤에 문맥을 확정한다(§8.2). 문구가 흐르는 도중에 주문이 살아 있으면
    /// 손님이 말을 마치기도 전에 제조가 열린다.
    /// </summary>
    async UniTask OrderAsync(NewScriptSceneData scene, NewDialogueStepData step, CancellationToken token)
    {
        if (!TryParseOrderedCocktail(step.Arg, out string cocktailId))
        {
            throw new InvalidOperationException($"[Story] 주문 칵테일을 읽지 못했습니다(arg=\"{step.Arg}\"): {scene.Id}#{step.Seq}");
            return;
        }

        if (currentOrder != null && currentOrder.State != OrderState.Completed)
        {
            throw new InvalidOperationException($"[Story] 앞 주문({currentOrder.Details.ReceiverId})이 아직 끝나지 않아 새 주문을 만들지 않습니다: " +
                           $"{scene.Id}#{step.Seq}");
            return;
        }

        // 주문 문구는 일반 say와 같은 규칙으로 낸다. 표정 인자는 order의 arg가 이미 차지하고 있어 없다.
        string body = step.Text?.Ko;
        if (!string.IsNullOrEmpty(body))
        {
            await presenter.ShowSayAsync(
                new StorySayRequest(step.Actor, null, body, step.Actor == PlayerCharacterId), token);

            await WaitForAdvanceAsync(token);
        }

        if (!TryFindSeatOf(step.Actor, out ESlotType seat))
        {
            // 앉지 않은 인물은 잔을 받을 자리가 없다. 가운데 자리로 대신하지 않는다 —
            // 그러면 엉뚱한 자리에 코스터가 놓이고 원인이 멀어진다.
            throw new InvalidOperationException($"[Story] 주문자 '{step.Actor}'가 앉아 있지 않습니다: {scene.Id}#{step.Seq}");
            return;
        }

        // 지난 잔의 결과는 여기서 버린다. 새 주문이 시작됐는데 앞 잔의 등급이 남아 있으면
        // 그 사이의 조건식이 이미 지난 잔을 보고 갈린다(§10.3).
        conditions.Result = null;

        if (currentOrder != null) throw new InvalidOperationException("Previous story order has not been served.");
        if (orderController == null) throw new InvalidOperationException("OrderRequestController 연결이 필요합니다.");
        var details = new OrderDetails($"{scene.Id}#{step.Seq}", step.Actor, cocktailId);
        var signal = new UniTaskCompletionSource<OrderResult>();
        orderResultSignal = signal;
        // 지역 신호를 캡처해 이전 주문의 늦은 콜백이 새 주문에 적용되지 않게 한다.
        currentOrder = orderController.Request(details, result => signal.TrySetResult(result));
    }

    /// <summary>기존 craft 스텝 진입점. 제조는 UI에서 진행하고 다음 serve 스텝이 주문 결과를 기다린다.</summary>
    async UniTask CraftAsync(NewScriptSceneData scene, NewDialogueStepData step, CancellationToken token)
    {
        if (orderController == null)
        {
            throw new InvalidOperationException($"[Story] orderController가 없어 제조로 넘어가지 못했습니다: {scene.Id}#{step.Seq}");
            return;
        }

        string tutorialCocktailId = ParseTutorialCocktail(step.Arg);

        // 튜토리얼은 무엇을 만들지가 대본에 적혀 있어 주문 없이도 열 수 있다.
        if (currentOrder == null && tutorialCocktailId == null)
        {
            throw new InvalidOperationException($"[Story] 주문이 없어 제조를 시작할 수 없습니다: {scene.Id}#{step.Seq}");
            return;
        }

        token.ThrowIfCancellationRequested();
        if (currentOrder == null) orderController.OpenMenu();
        await UniTask.CompletedTask;
    }

    /// <summary>
    /// 완성 잔이 주문자에게 놓이기를 기다렸다가 결과를 확정한다.
    ///
    /// 결과 문맥을 먼저 공개하고 그다음에 이 스텝의 effects가 적용된다 — effects를 부르는 것은
    /// 이 함수가 돌아온 뒤라 순서가 저절로 지켜진다(§9.1).
    /// </summary>
    async UniTask ServeAsync(NewScriptSceneData scene, NewDialogueStepData step, CancellationToken token)
    {
        if (orderController == null || currentOrder == null)
        {
            throw new InvalidOperationException($"[Story] 낼 주문이 없어 서빙할 수 없습니다: {scene.Id}#{step.Seq}");
            return;
        }

        if (!string.IsNullOrEmpty(step.Actor) && step.Actor != currentOrder.Details.ReceiverId)
        {
            // serve.actor는 주문자와 같아야 한다. 다르면 대본이 어긋난 것이라 주문자 쪽을 따른다.
            throw new InvalidOperationException($"[Story] serve의 actor '{step.Actor}'가 주문자 '{currentOrder.Details.ReceiverId}'와 다릅니다: " +
                           $"{scene.Id}#{step.Seq}");
        }

        if (orderResultSignal == null)
            throw new InvalidOperationException("주문 결과 콜백이 등록되지 않았습니다.");

        var result = await orderResultSignal.Task.AttachExternalCancellation(token);
        token.ThrowIfCancellationRequested();
        if (result.State == OrderState.Cancelled) throw new OperationCanceledException("주문이 취소되었습니다.");
        if (!result.Completed) throw new InvalidOperationException(result.Error ?? "서빙 처리에 실패했습니다.");
        var serve = result.Serve;
        conditions.Result = new StoryResultContext(serve.CraftGrade, serve.FinalGrade,
            serve.OrderMatch, serve.OrderedCocktailId, serve.ServedCocktailId);
        currentOrder = null;
        orderResultSignal = null;
    }

    /// <summary>order.arg의 "exact:&lt;cocktail_id&gt;"에서 칵테일 id를 꺼낸다.</summary>
    static bool TryParseOrderedCocktail(string arg, out string cocktailId)
    {
        cocktailId = null;

        const string prefix = "exact:";
        if (string.IsNullOrEmpty(arg) || !arg.StartsWith(prefix, StringComparison.Ordinal)) return false;

        cocktailId = arg.Substring(prefix.Length).Trim();
        return cocktailId.Length > 0;
    }

    /// <summary>
    /// craft.arg를 읽는다. "tutorial:&lt;id&gt;"면 그 칵테일을, "order"면 null(주문대로)을 돌려준다.
    /// </summary>
    static string ParseTutorialCocktail(string arg)
    {
        const string prefix = "tutorial:";
        if (string.IsNullOrEmpty(arg) || !arg.StartsWith(prefix, StringComparison.Ordinal)) return null;

        string cocktailId = arg.Substring(prefix.Length).Trim();
        return cocktailId.Length > 0 ? cocktailId : null;
    }

    // ── 등장·퇴장 ───────────────────────────────────────────────────────

    async UniTask EnterAsync(NewScriptSceneData scene, NewDialogueStepData step, CancellationToken token)
    {
        if (!TryParseSlot(step.Arg, out ESlotType slot))
        {
            throw new InvalidOperationException($"[Story] enter의 자리를 읽지 못했습니다: '{step.Arg}' (L·M·R이어야 합니다)");
            return;
        }

        if (seatActors.TryGetValue(slot, out string sitting) && sitting != step.Actor)
            throw new InvalidOperationException($"[Story] {slot} 자리에 '{sitting}'가 앉아 있는데 '{step.Actor}'가 또 앉습니다.");

        seatActors[slot] = step.Actor;
        WarnIfSeatingInvalid();

        await presenter.EnterAsync(step.Actor, slot, token);

        // 다음도 enter면 지금 화면을 잡지 않는다. 두 명으로 시작하는 씬이 1인 프레임을 한 번 거치면
        // 사람이 늘지도 않은 채 화면만 다가갔다 물러난다(§10.1.1 초기 진입).
        if (NextExecutableStepIsEnter(scene, step)) return;

        await ApplyFramingAsync(token);
    }

    async UniTask ExitAsync(NewDialogueStepData step, CancellationToken token)
    {
        // 퇴장은 자리를 지정하지 않는다(arg가 null). 어디에 앉혔는지는 이쪽이 기억하고 있다.
        if (!TryFindSeatOf(step.Actor, out ESlotType slot))
        {
            Debug.LogWarning($"[Story] '{step.Actor}'가 앉아 있는 자리를 찾지 못해 퇴장을 건너뜁니다.");
            return;
        }

        seatActors.Remove(slot);
        presenter.Exit(slot);

        await ApplyFramingAsync(token);
    }

    /// <summary>
    /// 지금 앉아 있는 사람들을 화면에 넘긴다(§10.1.1).
    ///
    /// 몇 대 몇의 프레임으로 잡을지는 화면이 정한다. 여기서는 seat_actors가 정본이라는 것만 지킨다 —
    /// 씬 제목이나 대본에 적힌 숫자로 인원을 세면 둘이 어긋나는 날이 온다.
    /// </summary>
    UniTask ApplyFramingAsync(CancellationToken token)
    {
        var occupied = new List<ESlotType>(seatActors.Count);

        // 좌석 순서대로 넘긴다. 뒤에서 중점을 잡을 때 집어넣은 순서에 따라 결과가 달라지면 곤란하다.
        foreach (ESlotType slot in SeatOrder)
        {
            if (seatActors.ContainsKey(slot)) occupied.Add(slot);
        }

        return presenter.ApplyFramingAsync(occupied, token);
    }

    /// <summary>
    /// 이 스텝 바로 뒤에 실행될 스텝이 또 enter인지. when이 거짓이라 건너뛸 스텝은 세지 않는다.
    /// </summary>
    bool NextExecutableStepIsEnter(NewScriptSceneData scene, NewDialogueStepData step)
    {
        if (scene.Steps == null) return false;

        bool passed = false;

        foreach (var candidate in scene.Steps)
        {
            if (!passed)
            {
                // seq로 찾는다. 스텝은 구조체라 같은 것인지를 참조로 물을 수 없다.
                if (candidate.Seq == step.Seq) passed = true;
                continue;
            }

            if (!conditions.CheckRequired(candidate.When)) continue;

            return candidate.Type == ENewStepType.Enter;
        }

        return false;
    }

    static bool TryParseSlot(string arg, out ESlotType slot)
    {
        switch (arg)
        {
            case "L": slot = ESlotType.Left; return true;
            case "M": slot = ESlotType.Middle; return true;
            case "R": slot = ESlotType.Right; return true;
            default: slot = ESlotType.None; return false;
        }
    }

    bool TryFindSeatOf(string actorId, out ESlotType slot)
    {
        foreach (var pair in seatActors)
        {
            if (pair.Value != actorId) continue;

            slot = pair.Key;
            return true;
        }

        slot = ESlotType.None;
        return false;
    }

    /// <summary>
    /// 앉은 모양이 규칙에 맞는지 본다. 화면에는 최대 두 명이고, 두 명이면 L·R 양 끝에 앉는다 —
    /// 2부의 2인 프레임(1280×720)은 그 두 자리의 중점에 서도록 잡혀 있다.
    ///
    /// 자리를 임의로 옮겨 고치지 않는다. 대본이 잘못 적힌 것이라 원본을 고쳐야 하고,
    /// 런타임이 보정하면 그 잘못이 눈에 띄지 않는다.
    /// </summary>
    void WarnIfSeatingInvalid()
    {
        if (seatActors.Count > 2)
        {
            throw new InvalidOperationException($"[Story] 한 화면에 {seatActors.Count}명이 앉았습니다. 2부는 최대 두 명입니다.");
            return;
        }

        if (seatActors.Count == 2 &&
            !(seatActors.ContainsKey(ESlotType.Left) && seatActors.ContainsKey(ESlotType.Right)))
        {
            throw new InvalidOperationException("[Story] 두 손님이 L·R이 아닌 자리에 앉았습니다. 2부의 2인 배치는 L·R입니다.");
        }
    }

    // ── 진행 입력 ───────────────────────────────────────────────────────

    /// <summary>
    /// 플레이어가 대사를 넘길 때까지 기다린다.
    ///
    /// 타이핑을 건너뛰는 입력과 대사를 넘기는 입력을 같은 키로 받는데, 그 둘을 한 번의 누름으로
    /// 같이 처리하면 문장이 뜨자마자 사라진다. 타이핑은 표시 계층이 이미 기다렸으므로 여기서는
    /// 넘기는 입력만 받는다.
    /// </summary>
    async UniTask WaitForAdvanceAsync(CancellationToken token)
    {
        advanceSignal = new UniTaskCompletionSource();

        try
        {
            await advanceSignal.Task.AttachExternalCancellation(token);
        }
        finally
        {
            advanceSignal = null;
        }
    }

    /// <summary>진행 입력. 대사를 기다리는 중이 아니면 아무 일도 하지 않는다.</summary>
    public void OnAdvanceInput()
    {
        if (advanceSignal == null)
        {
            // 타이핑 도중이면 그것부터 끝낸다.
            presenter?.SkipTyping();
            return;
        }

        advanceSignal.TrySetResult();
    }
}
