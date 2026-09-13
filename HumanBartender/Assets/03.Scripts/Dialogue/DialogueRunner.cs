using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VContainer;

public enum DialogueState
{
    Idle,
    WaitingForTrigger,
    Typing,
    WaitingForInput,
    WaitingForChoice,
    CompleteTrigger,
}

public class DialogueRunner : MonoBehaviour
{
    [Inject] private IPlayerDataReader PlayerData;
    [Inject] private IConditionUtil conditionUtil;

    private IDialoguePresenter presenter;

    private readonly Dictionary<string, DialogueData> currentDB = new();
    private DialogueData currentDialogue;

    private DialogueState currentState = DialogueState.Idle;
    private CancellationTokenSource runnerCts;
    private UniTaskCompletionSource completionSource;
    private UniTaskCompletionSource outsideInputCompletionSource;

    [SerializeField]
    private NewStreetDataSO StreetDataSO;

    public DialogueState CurrentState => currentState;
    public bool IsRunning => currentState != DialogueState.Idle;

    public ELanguage CurrentLanguage => GameStateManager.Instance != null
        ? GameStateManager.Instance.Language
        : ELanguage.Ko;

    public void Bind(IDialoguePresenter presenter)
    {
        this.presenter = presenter;
        Debug.Log($"[DialogueRunner] Presenter 바인딩 완료: {(presenter != null ? presenter.GetType().Name : "null")}");
    }
    public void Stop()
    {
        Debug.Log("[DialogueRunner] Stop 호출됨");

        // 1. CancellationToken Cancel
        runnerCts?.Cancel();

        // 2. 입력 대기 중인 UniTaskCompletionSource 강제 취소로 대기 해제
        outsideInputCompletionSource?.TrySetCanceled();
        completionSource?.TrySetCanceled();
        
        // 3. 즉시 상태 초기화 및 Presenter Hide
        currentState = DialogueState.Idle;
        try
        {
            presenter?.HideDialogue();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DialogueRunner] HideDialogue 오류: {e}");
        }
    }
    #region Legacy Dialogue System

    public async UniTask PlayAsync(DialogueData[] dialogues, string startId = null, CancellationToken externalToken = default)
    {
        if (presenter == null)
        {
            Debug.LogError("[DialogueRunner] Presenter가 바인딩되지 않았습니다. Bind()를 먼저 호출하세요.");
            return;
        }

        if (dialogues == null || dialogues.Length == 0)
        {
            Debug.LogWarning("[DialogueRunner] 빈 dialogue 배열");
            return;
        }

        if (IsRunning)
        {
            Debug.LogWarning($"[DialogueRunner] 이미 실행 중입니다. (현재 상태: {currentState})");
            return;
        }

        currentDB.Clear();
        foreach (var d in dialogues)
            currentDB[d.Id] = d;

        string firstId = string.IsNullOrEmpty(startId) ? dialogues[0].Id : startId;

        completionSource = new UniTaskCompletionSource();
        runnerCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken, this.GetCancellationTokenOnDestroy());

        try
        {
            DialogueEvent(firstId);
            await completionSource.Task.AttachExternalCancellation(runnerCts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError($"[DialogueRunner] PlayAsync 오류: {e}");
        }
        finally
        {
            currentState = DialogueState.Idle;
            currentDialogue = new();
            currentDB.Clear();

            try { presenter?.HideDialogue(); }
            catch (Exception e) { Debug.LogError($"[DialogueRunner] HideDialogue 오류: {e}"); }

            runnerCts?.Dispose();
            runnerCts = null;
            completionSource = null;
        }
    }

    public void OnAdvanceInput()
    {
        if (currentState == DialogueState.WaitingForTrigger
            || currentState == DialogueState.WaitingForChoice
            || currentState == DialogueState.Idle)
            return;

        if (currentState == DialogueState.Typing)
        {
            presenter.SkipTyping();
            currentState = DialogueState.WaitingForInput;
            return;
        }

        if (currentState == DialogueState.WaitingForInput)
        {
            if (currentDialogue.Choices != null && currentDialogue.Choices.Length > 0)
            {
                ShowChoices();
            }
            else if (!string.IsNullOrEmpty(currentDialogue.Next))
            {
                DialogueEvent(currentDialogue.Next);
            }
            else
            {
                EndScene();
            }
        }
    }

    private void DialogueEvent(string id)
    {
        if (string.IsNullOrEmpty(id) || !currentDB.ContainsKey(id))
        {
            if (!string.IsNullOrEmpty(id))
                Debug.LogWarning($"[DialogueRunner] dialogue id '{id}' 을(를) 찾을 수 없습니다.");

            EndScene();
            return;
        }

        PlayDialogueAsync(id).Forget();
    }

    private async UniTaskVoid PlayDialogueAsync(string dialogueId)
    {
        try
        {
            currentDialogue = currentDB[dialogueId];

            if (currentDialogue.Type == EDialogueType.System)
            {
                presenter.ShowSystemAction();

                DialogueEvent(currentDialogue.Next);
                return;
            }
            else if (currentDialogue.Type == EDialogueType.ConditionBranch)
            {
                NextConditions next = currentDialogue.Nextconditions.Value;
                string nextid = CheckCondition(next);
                DialogueEvent(nextid);
                return;
            }
            else if (currentDialogue.Type == EDialogueType.ChoiceRoot || currentDialogue.Type == EDialogueType.Choice)
            {
                if (currentDialogue.Choices != null && currentDialogue.Choices.Length > 0)
                {
                    ShowChoices();
                    return;
                }
            }

            currentState = DialogueState.Typing;
            await presenter.ShowDialogueAsync(currentDialogue, runnerCts.Token);
            currentState = DialogueState.WaitingForInput;
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError($"[DialogueRunner] PlayDialogueAsync 오류: {e}");
            EndScene();
        }
    }

    public string CheckCondition(NextConditions checkType)
    {
        switch (checkType.Stat)
        {
            case EConditionCheckType.None:
                break;

            case EConditionCheckType.Skill:
                return checkType.Default;

            case EConditionCheckType.Money:
                if (PlayerData.HasEnoughMoney(checkType.MinAmount))
                    return checkType.Branches[0].Goto;
                return checkType.Default;

            case EConditionCheckType.Flag:
                break;
        }

        return checkType.Default;
    }

    private void ShowChoices()
    {
        currentState = DialogueState.WaitingForChoice;
        presenter.ShowChoices(currentDialogue.Choices, OnChoiceSelected);
    }

    private void OnChoiceSelected(ChoiceData data)
    {
        DialogueEvent(data.Next);
    }

    private void EndScene()
    {
        currentState = DialogueState.Idle;
        presenter.EndScene();
        completionSource?.TrySetResult();
    }

    #endregion


    #region Outside Dialogue System (NewStreetDataSO 연동)

    public async UniTask PlayOutsideAsync(Step[] startSteps, CancellationToken externalToken = default)
    {
        Debug.Log($"[DialogueRunner] PlayOutsideAsync 호출됨. Step 개수: {startSteps?.Length ?? 0}");

        if (presenter == null)
        {
            Debug.LogError("[DialogueRunner] 실패: Presenter가 바인딩되지 않았습니다. Bind()를 먼저 호출했는지 확인하세요.");
            return;
        }

        if (startSteps == null || startSteps.Length == 0)
        {
            Debug.LogWarning("[DialogueRunner] 실패: 실행할 Step 배열이 비어있습니다.");
            return;
        }

        if (IsRunning)
        {
            Debug.LogWarning($"[DialogueRunner] 실패: 이미 대화가 진행 중입니다. (현재 상태: {currentState})");
            return;
        }

        runnerCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken, this.GetCancellationTokenOnDestroy());

        try
        {
            Debug.Log("[DialogueRunner] ExecuteOutsideStepsAsync 시작합니다.");
            await ExecuteOutsideStepsAsync(startSteps, runnerCts.Token);
            Debug.Log("[DialogueRunner] ExecuteOutsideStepsAsync 정상적으로 끝났습니다.");
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("[DialogueRunner] PlayOutsideAsync 가 취소(Cancel)되었습니다.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DialogueRunner] PlayOutsideAsync 중 예외 발생: {e}");
        }
        finally
        {
            Debug.Log("[DialogueRunner] PlayOutsideAsync 종료 (State -> Idle)");
            currentState = DialogueState.Idle;
            outsideInputCompletionSource = null;
            try { presenter?.EndScene(); }
            catch (Exception e) { Debug.LogError($"[DialogueRunner] HideDialogue 오류: {e}"); }

            runnerCts?.Dispose();
            runnerCts = null;
        }
    }

    public async UniTask PlayOutsideAsync(string sceneId, CancellationToken externalToken = default)
    {
        Debug.Log($"[DialogueRunner] PlayOutsideAsync(sceneId: '{sceneId}') 호출됨.");

        if (StreetDataSO == null)
        {
            Debug.LogError("[DialogueRunner] 실패: StreetDataSO가 Inspector에 할당되지 않았습니다.");
            return;
        }

        if (StreetDataSO.TryGetSteps(sceneId, out Step[] steps))
        {
            await PlayOutsideAsync(steps, externalToken);
        }
        else
        {
            Debug.LogError($"[DialogueRunner] 실패: StreetDataSO에서 SceneId '{sceneId}'를 찾을 수 없습니다.");
        }
    }

    public void AdvanceInputOutside()
    {
        Debug.Log($"[DialogueRunner] AdvanceInputOutside 호출됨. (현재 상태: {currentState})");

        if (presenter != null)
        {
            if (presenter.GetPlayMode() == EActivationMode.Proximity)
            {
                Debug.Log($"[DialogueRunner] 자동진행 입력 무시");
                return;
            }
        }

            if (currentState == DialogueState.Typing)
        {
            Debug.Log("[DialogueRunner] 타이핑 스킵 실행");
            presenter?.SkipTyping();
            currentState = DialogueState.WaitingForInput;
        }
        else if (currentState == DialogueState.WaitingForInput)
        {
            Debug.Log("[DialogueRunner] 다음 스텝 진행 신호 전달 (outsideInputCompletionSource)");
            outsideInputCompletionSource?.TrySetResult();
        }
    }

    private async UniTask ExecuteOutsideStepsAsync(Step[] steps, CancellationToken ct)
    {
        if (steps == null) return;

        for (int i = 0; i < steps.Length; i++)
        {
            ct.ThrowIfCancellationRequested();

            var step = steps[i];
            Debug.Log($"[DialogueRunner] Step [{i}/{steps.Length - 1}] 처리 시작 - Type: '{step.Type}', Actor: '{step.Actor}', When: '{step.When}'");

            if (!string.IsNullOrEmpty(step.When))
            {
                if (conditionUtil == null)
                {
                    Debug.LogWarning("[DialogueRunner] ConditionUtil이 Inject되지 않았습니다. 조건 검사를 건너뜁니다.");
                }
                else if (!conditionUtil.Check(step.When))
                {
                    Debug.Log($"[DialogueRunner] Step [{i}] 조건 미충족 ('{step.When}') -> 건너뜁니다.");
                    continue;
                }
            }

            switch (step.Type?.ToLower())
            {
                case "say":
                case "timeline":
                    await ProcessSayStepOutsideAsync(step, ct);
                    break;

                case "set_state":
                    ProcessSetStateStepOutside(step.Effects);
                    break;

                case "choice":
                    bool stopLoop = await ProcessChoiceStepOutsideAsync(step, ct);
                    if (stopLoop)
                    {
                        Debug.Log($"[DialogueRunner] Choice 처리 완료 후 현재 Step 루프를 중단합니다.");
                        return;
                    }
                    break;

                case "goto":
                    string targetSceneId = step.SceneId;

                    if (!string.IsNullOrEmpty(targetSceneId) && StreetDataSO != null && StreetDataSO.TryGetSteps(targetSceneId, out var nextSteps))
                    {
                        Debug.Log($"[DialogueRunner] 'goto' 진입: Scene ID '{targetSceneId}' (Steps: {nextSteps.Length}개) 실행");
                        await ExecuteOutsideStepsAsync(nextSteps, ct);
                    }
                    else
                    {
                        Debug.LogWarning($"[DialogueRunner] 'goto' 실패: StreetDataSO가 없거나 Scene ID '{targetSceneId}'를 찾을 수 없습니다.");
                    }
                    return;

                default:
                    Debug.LogWarning($"[DialogueRunner] 알 수 없는 스텝 타입: '{step.Type}'");
                    break;
            }
        }
    }

    private async UniTask ProcessSayStepOutsideAsync(Step step, CancellationToken ct)
    {
        currentState = DialogueState.Typing;

        string localizedText = GetTextOutside(step.Text);
        Debug.Log($"[DialogueRunner] ShowDialogueAsync 대기 중 - LocalizedText: \"{localizedText}\"");

        await presenter.ShowDialogueAsync(step.Actor, localizedText, step.Arg, ct);

        if (step.Sync == "auto")
        {
            Debug.Log("[DialogueRunner] Proximity 모드: 2초 후 자동 진행");

            // WaitingForInput 대신 자동 진행용 상태가 필요하다면 유지 또는 변경 가능합니다.
            currentState = DialogueState.WaitingForInput;

            // 2초 대기 (대기 중 Cancel 요청 시 즉시 중단)
            await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: ct);

            Debug.Log("[DialogueRunner] 2초 대기 완료 -> 다음 Step으로 이동 준비");
        }
        else
        {
            Debug.Log("[DialogueRunner] ShowDialogueAsync 연출 완료 -> 입력 대기 중 (WaitingForInput)");
            currentState = DialogueState.WaitingForInput;

            outsideInputCompletionSource = new UniTaskCompletionSource();
            await outsideInputCompletionSource.Task.AttachExternalCancellation(ct);

            Debug.Log("[DialogueRunner] 입력 수신됨 -> 다음 Step으로 이동 준비");
        }
    }

    private void ProcessSetStateStepOutside(string effects)
    {
        if (string.IsNullOrEmpty(effects)) return;
        conditionUtil.Set(effects);
        Debug.Log($"[DialogueRunner] Effect 적용 처리: {effects}");
    }

    private async UniTask<bool> ProcessChoiceStepOutsideAsync(Step step, CancellationToken ct)
    {
        if (step.Options == null || step.Options.Length == 0)
        {
            Debug.LogWarning("[DialogueRunner] Choice 타입 스텝이지만 Options가 비어있습니다.");
            return false;
        }

        currentState = DialogueState.WaitingForChoice;
        Debug.Log($"[DialogueRunner] Choice 선택지 출력 중... (선택지 개수: {step.Options.Length})");

        var choiceCompletionSource = new UniTaskCompletionSource<NewStreetOptionData>();

        presenter.ShowOutsideChoices(step.Options, selectedOption =>
        {
            Debug.Log($"[DialogueRunner] 선택지 클릭됨");
            choiceCompletionSource.TrySetResult(selectedOption);
        });

        var selectedOption = await choiceCompletionSource.Task.AttachExternalCancellation(ct);

        // 1. When 조건 검사
        bool hasCondition = !string.IsNullOrEmpty(selectedOption.When);
        bool isConditionFailed = hasCondition && (conditionUtil == null || !conditionUtil.Check(selectedOption.When));

        if (isConditionFailed)
        {
            Debug.Log($"[DialogueRunner] 조건 불만족('{selectedOption.When}') -> LockReason 대사 출력 및 입력 대기");

            // [타이핑 출력]
            currentState = DialogueState.Typing;
            string lockText = GetTextOutside(selectedOption.LockReason);
            await presenter.ShowDialogueAsync(null, lockText, null, ct);

            // [입력 대기 설정] ProcessSayStepOutsideAsync와 동일 구조
            currentState = DialogueState.WaitingForInput;
            outsideInputCompletionSource = new UniTaskCompletionSource();

            // 유저가 AdvanceInputOutside()를 호출해서 클릭할 때까지 여기서 멈춤
            await outsideInputCompletionSource.Task.AttachExternalCancellation(ct);

            Debug.Log("[DialogueRunner] LockReason 입력 수신됨 -> 다음 Step으로 진행 준비");

            // 대기 완료 후 CompletionSource 초기화
            outsideInputCompletionSource = null;

            // true를 반환하면 Choice 스텝 처리가 끝나고 ExecuteOutsideStepsAsync의 다음 Step(for문 다음)으로 넘어감
            return false;
        }
        // 2. 조건을 만족했거나 조건이 없는 경우
        if (selectedOption.ResultSteps != null && selectedOption.ResultSteps.Length > 0)
        {
            Debug.Log($"[DialogueRunner] 선택지 선택 결과 ResultSteps ({selectedOption.ResultSteps.Length}개) 실행");
            await ExecuteOutsideStepsAsync(selectedOption.ResultSteps, ct);
            return true;
        }
        return false;
    }

    private string GetTextOutside(Texts textData)
    {
        if (textData == null) return string.Empty;

        return CurrentLanguage switch
        {
            ELanguage.En => !string.IsNullOrEmpty(textData.En) ? textData.En : textData.Ko,
            _ => textData.Ko
        };
    }
    #endregion
}