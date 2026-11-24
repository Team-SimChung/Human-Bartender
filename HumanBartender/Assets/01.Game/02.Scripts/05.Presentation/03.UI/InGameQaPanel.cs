using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

/// <summary>빌드에서 진행 상황을 재현하는 내부 테스트 화면.</summary>
public sealed class InGameQaPanel : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] GameObject panelRoot;
    [SerializeField] Button closeButton;
    [SerializeField] TMP_Text statusText;

    [Header("일차 및 이동")]
    [SerializeField] TMP_Text dayText, pageText;
    [SerializeField] Button previousDay, nextDay, previousPage, nextPage, commuteIn, commuteOut;
    [Tooltip("바 스크립트의 씬 ID, 제목, 인물 또는 대사로 필터링한다.")]
    [SerializeField] TMP_InputField scriptSearch;
    [SerializeField] Button[] scriptButtons;
    [SerializeField] TMP_Text[] scriptLabels;

    [Header("테스트 상태")]
    [SerializeField] TMP_Text moneyText, flagText, forceText;
    [SerializeField] TMP_InputField moneyAmount, flagKey;
    [SerializeField] Button addMoney, subtractMoney, setFlag, clearFlag, forceChoices, resetProgress;

    static bool forceVisibleChoices;
    static BarJump? pendingBarJump;
    internal static bool ForceVisibleChoices => forceVisibleChoices;
    internal static bool IsOpen { get; private set; }

    internal readonly struct BarJump
    {
        internal readonly int Day;
        internal readonly string SceneId;
        internal readonly int StepSeq;
        internal readonly ENewScenePhase Phase;

        internal BarJump(int day, string sceneId, int stepSeq, ENewScenePhase phase)
        {
            Day = day;
            SceneId = sceneId;
            StepSeq = stepSeq;
            Phase = phase;
        }
    }

    readonly struct BarLine
    {
        internal readonly string SceneId, Title, Actor, Text;
        internal readonly int Seq;
        internal readonly ENewScenePhase Phase;

        internal BarLine(NewScriptSceneData scene, NewDialogueStepData step)
        {
            SceneId = scene.Id;
            Title = scene.Title;
            Actor = step.Actor;
            Text = step.Text?.Ko;
            Seq = step.Seq;
            Phase = scene.Phase;
        }

        internal bool Matches(string query) => new[] { SceneId, Title, Actor, Text }.Any(
            value => value != null && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Inject] PlayerDataSO player;
    [Inject] GameProgressionService progression;
    [Inject] ISceneTransitionService transitions;

    readonly InputAction toggleInput = new("QaTools", InputActionType.Button, "<Keyboard>/p");
    IDisposable pause;
    bool busy;
    int[] days = Array.Empty<int>();
    int dayIndex, page;
    BarLine[] lines = Array.Empty<BarLine>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        forceVisibleChoices = false;
        pendingBarJump = null;
        IsOpen = false;
    }

    internal static BarJump? TakeBarJump(int day)
    {
        if (!pendingBarJump.HasValue || pendingBarJump.Value.Day != day) return null;
        var jump = pendingBarJump;
        pendingBarJump = null;
        return jump;
    }

    void Awake()
    {
        panelRoot.SetActive(false);
        closeButton.onClick.AddListener(Close);
        previousDay.onClick.AddListener(() => MoveDay(-1));
        nextDay.onClick.AddListener(() => MoveDay(1));
        previousPage.onClick.AddListener(() => MovePage(-1));
        nextPage.onClick.AddListener(() => MovePage(1));
        commuteIn.onClick.AddListener(() => MoveOutside(EGameFlow.CommuteIn));
        commuteOut.onClick.AddListener(() => MoveOutside(EGameFlow.CommuteOut));
        scriptSearch.onValueChanged.AddListener(_ => RefreshScript());
        for (int index = 0; index < scriptButtons.Length; index++)
        {
            int row = index;
            scriptButtons[index].onClick.AddListener(() => MoveToLine(row));
        }

        moneyAmount.SetTextWithoutNotify("100");
        addMoney.onClick.AddListener(() => ChangeMoney(1));
        subtractMoney.onClick.AddListener(() => ChangeMoney(-1));
        flagKey.onValueChanged.AddListener(_ => RefreshFlag());
        setFlag.onClick.AddListener(() => ChangeFlag(true));
        clearFlag.onClick.AddListener(() => ChangeFlag(false));
        forceChoices.onClick.AddListener(ToggleForce);
        resetProgress.onClick.AddListener(() => RunOperationAsync(ResetProgressAsync).Forget());
    }

    void OnEnable()
    {
        SceneManager.activeSceneChanged += OnSceneChanged;
        toggleInput.performed += ToggleFromKeyboard;
        toggleInput.Enable();
    }

    void OnDisable()
    {
        toggleInput.Disable();
        toggleInput.performed -= ToggleFromKeyboard;
        SceneManager.activeSceneChanged -= OnSceneChanged;
        Close();
    }

    void OnDestroy() => toggleInput.Dispose();

    void OnSceneChanged(Scene previous, Scene current)
    {
        Close();
        if (current.name == "Main") ResetSession();
    }

    void ToggleFromKeyboard(InputAction.CallbackContext _)
    {
        if (busy || player == null) return;
        if (panelRoot.activeSelf)
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == null || selected.GetComponent<TMP_InputField>()?.isFocused != true) Close();
        }
        else if (SceneManager.GetActiveScene().name is "Play" or "OutSide" or "Home" &&
                 !UIDisplayOptions.IsOptionOpen && !transitions.IsBusy)
            Open();
    }

    void Open()
    {
        pause ??= GamePause.Acquire();
        IsOpen = true;
        EventSystem.current?.SetSelectedGameObject(null);
        panelRoot.SetActive(true);
        days = NewDataLoadManager.LoadedDays.Select(info => info.Day).Distinct().OrderBy(day => day).ToArray();
        dayIndex = Mathf.Max(0, Array.IndexOf(days, GameStateManager.Instance.CurrentDay));
        RefreshScript();
        RefreshMoney();
        RefreshFlag();
        RefreshForce();
    }

    void Close()
    {
        IsOpen = false;
        pause?.Dispose();
        pause = null;
        if (panelRoot == null || !panelRoot.activeSelf) return;
        var events = EventSystem.current;
        if (events?.currentSelectedGameObject != null &&
            events.currentSelectedGameObject.transform.IsChildOf(panelRoot.transform))
            events.SetSelectedGameObject(null);
        panelRoot.SetActive(false);
    }

    void MoveDay(int direction)
    {
        dayIndex = Mathf.Clamp(dayIndex + direction, 0, days.Length - 1);
        RefreshScript();
    }

    void RefreshScript()
    {
        page = 0;
        bool hasDay = days.Length != 0;
        dayText.text = hasDay ? $"{days[dayIndex]}일차" : "일차 준비 중";
        previousDay.interactable = hasDay && dayIndex > 0;
        nextDay.interactable = hasDay && dayIndex + 1 < days.Length;
        commuteIn.interactable = commuteOut.interactable = hasDay;
        string query = scriptSearch.text.Trim();
        lines = hasDay && NewDataLoadManager.TryGetBarScript(days[dayIndex], out var script)
            ? (script.Scenes ?? Array.Empty<NewScriptSceneData>())
                .Where(scene => scene.Phase is ENewScenePhase.BarOpen or ENewScenePhase.Bar)
                .OrderBy(scene => scene.Phase).ThenBy(scene => scene.Seq)
                .SelectMany(scene => (scene.Steps ?? Array.Empty<NewDialogueStepData>())
                    .Where(step => step.Type is ENewStepType.Say or ENewStepType.Order && !string.IsNullOrWhiteSpace(step.Text?.Ko))
                    .Select(step => new BarLine(scene, step)))
                .Where(line => query.Length == 0 || line.Matches(query)).ToArray()
            : Array.Empty<BarLine>();
        RefreshPage();
    }

    void MovePage(int direction)
    {
        page += direction;
        RefreshPage();
    }

    void RefreshPage()
    {
        int count = scriptButtons.Length;
        int pages = Mathf.Max(1, (lines.Length + count - 1) / count);
        page = Mathf.Clamp(page, 0, pages - 1);
        previousPage.interactable = page > 0;
        nextPage.interactable = page + 1 < pages;
        pageText.text = lines.Length == 0 ? "검색 결과 없음" : $"{page + 1} / {pages} · {lines.Length}개";
        for (int row = 0; row < count; row++)
        {
            int index = page * count + row;
            bool visible = index < lines.Length;
            scriptButtons[row].gameObject.SetActive(visible);
            if (!visible) continue;
            var line = lines[index];
            scriptLabels[row].text = $"{line.SceneId} #{line.Seq}  {line.Actor}\n{line.Text.Replace('\n', ' ').Replace('\r', ' ')}";
        }
    }

    void MoveToLine(int row)
    {
        int index = page * scriptButtons.Length + row;
        if (days.Length == 0 || index >= lines.Length) return;
        var line = lines[index];
        var jump = new BarJump(days[dayIndex], line.SceneId, line.Seq, line.Phase);
        RunOperationAsync(async token =>
        {
            pendingBarJump = jump;
            try
            {
                RequireSuccess(await progression.TravelForQaAsync(jump.Day, EGameFlow.Bar, token));
                if (pendingBarJump.HasValue)
                    throw new InvalidOperationException("[QA] 도착한 바에서 대사 시작점을 받지 못했습니다.");
                statusText.text = $"{jump.Day}일차 {jump.SceneId} #{jump.StepSeq}에서 시작합니다.";
            }
            finally { pendingBarJump = null; }
        }).Forget();
    }

    void MoveOutside(EGameFlow flow)
    {
        if (days.Length == 0) return;
        int day = days[dayIndex];
        RunOperationAsync(async token =>
        {
            RequireSuccess(await progression.TravelForQaAsync(day, flow, token));
            statusText.text = $"{day}일차 {(flow == EGameFlow.CommuteIn ? "출근" : "퇴근")}으로 이동했어요.";
        }).Forget();
    }

    async UniTask ResetProgressAsync(CancellationToken token)
    {
        int money = player.HasMoney(), skill = player.GetSkillValue();
        var affinity = player.ReadAffinity();
        var flags = player.ReadFlags();
        if (SceneManager.GetActiveScene().name != "Main")
        {
            var result = await transitions.RequestLoadScene("Main", cancellationToken: token).Completion;
            if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "[QA] 타이틀 이동에 실패했습니다.");
        }
        ResetSession();
        RequireSuccess(await progression.StartNewGameAsync(player.Init,
            () => player.ReplaceProgress(money, skill, affinity, flags), token));
        scriptSearch.SetTextWithoutNotify("");
        flagKey.SetTextWithoutNotify("");
        statusText.text = "현재 진행을 초기화했어요.";
    }

    static void RequireSuccess(GameProgressionResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(result.Message ?? result.Outcome.ToString(), result.Error);
    }

    async UniTask RunOperationAsync(Func<CancellationToken, UniTask> operation)
    {
        if (busy || transitions.IsBusy) return;
        busy = true;
        Close();
        try { await operation(this.GetCancellationTokenOnDestroy()); }
        catch (Exception error)
        {
            if (this == null) return;
            Debug.LogWarning($"[QA] {error.Message}");
            statusText.text = error.Message;
            if (SceneManager.GetActiveScene().name is "Play" or "OutSide" or "Home") Open();
        }
        finally { busy = false; }
    }

    void ChangeMoney(int direction)
    {
        if (!int.TryParse(moneyAmount.text, out int amount) || amount <= 0)
        {
            statusText.text = "증감할 양의 정수를 입력하세요.";
            return;
        }
        long balance = (long)player.HasMoney() + (long)direction * amount;
        player.SetMoney((int)Math.Clamp(balance, int.MinValue, int.MaxValue));
        RefreshMoney();
    }

    void RefreshMoney() => moneyText.text = $"소지금 {player.HasMoney():N0}";

    void ChangeFlag(bool value)
    {
        string key = flagKey.text.Trim();
        if (key.Length == 0 || key == "flag.")
        {
            statusText.text = "플래그 키를 입력하세요.";
            return;
        }
        player.AddFlag(key, value);
        var world = FindFirstObjectByType<WorldInteractionSceneContext>();
        RefreshFlag();
        if (world == null || !world.IsReady) return;
        try { world.RefreshContents(); }
        catch (InvalidOperationException error)
        {
            statusText.text = error.Message;
            Debug.LogWarning($"[QA] {error.Message}");
        }
    }

    void RefreshFlag()
    {
        string key = flagKey.text.Trim();
        flagText.text = key.Length == 0 || key == "flag." ? "플래그 상태" : player.CheckFlag(key) ? "현재 true" : "현재 false";
    }

    void ToggleForce()
    {
        forceVisibleChoices = !forceVisibleChoices;
        RefreshForce();
        statusText.text = forceVisibleChoices ? "패널을 닫고 선택지를 누르세요. 비용은 잔액이 부족해도 차감돼요." : "선택지 조건을 다시 적용해요.";
    }

    void RefreshForce() => forceText.text = $"포스 실행 {(forceVisibleChoices ? "켜짐" : "꺼짐")}";
}
