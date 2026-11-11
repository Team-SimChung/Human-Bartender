using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>실제 Home 씬의 날짜별 상호작용을 반복 확인하는 에디터 전용 창.</summary>
internal sealed class HomeInteractionTestWindow : EditorWindow
{
    [SerializeField] private int selectedScenario = 1;
    [SerializeField] private bool outside;
    [SerializeField] private Vector2 scroll;
    [SerializeField] private bool showFlags;
    private string lastResult;

    [MenuItem("Tools/Human Bartender/Tests/Home Interaction Lab")]
    private static void Open()
    {
        OpenScene(false);
    }

    [MenuItem(OutsideInteractionTestScenarios.MenuPath)]
    private static void OpenOutside() => OpenScene(true);

    private static void OpenScene(bool outside)
    {
        var window = GetWindow<HomeInteractionTestWindow>("홈·외부 상호작용 테스트");
        if (window.outside != outside) window.selectedScenario = 0;
        window.outside = outside;
        window.minSize = new Vector2(520, 600);
        window.Show();
    }

    private void OnInspectorUpdate() => Repaint();

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("홈·외부 상호작용 테스트", EditorStyles.boldLabel);
        bool nextOutside = GUILayout.Toolbar(outside ? 1 : 0, new[] { "홈 상호작용", "외부 상호작용" }) == 1;
        if (nextOutside != outside) { outside = nextOutside; selectedScenario = 0; }
        if (GUILayout.Button("게임 전체 상호작용 지점 테스트 열기"))
            EditorApplication.ExecuteMenuItem(WorldInteractionTestCatalog.MenuPath);
        if (GUILayout.Button("바 내부 · 대사를 골라 시작")) EditorApplication.ExecuteMenuItem(BarDialogueTestCatalog.MenuPath);
        EditorGUILayout.HelpBox("저장된 Home·OutSide 씬·CSV·카메라·상호작용을 사용합니다. 작업 중인 씬 변경은 먼저 저장하세요. " +
            "상황을 다시 불러오면 완료 기록과 배치도 초기화됩니다. " +
            "테스트용 플레이어 복사본과 별도 저장 슬롯을 사용합니다.", MessageType.Info);

        DrawScenario();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(HomeInteractionTestSession.Status ?? "상황을 선택하고 테스트를 시작하세요.", MessageType.None);
        if (!string.IsNullOrEmpty(lastResult)) EditorGUILayout.LabelField(lastResult, EditorStyles.wordWrappedLabel);

        if (EditorApplication.isPlaying && HomeInteractionTestSession.IsActive)
        {
            DrawRuntime();
            DrawSaveDirectory();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(outside ? OutsideInteractionTestScenarios.CoverageNote : HomeInteractionTestScenarios.CoverageNote,
            MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    private void DrawScenario()
    {
        var scenarios = outside ? OutsideInteractionTestScenarios.All : HomeInteractionTestScenarios.All;
        selectedScenario = Mathf.Clamp(selectedScenario, 0, scenarios.Count - 1);
        selectedScenario = EditorGUILayout.Popup("준비할 상황", selectedScenario,
            scenarios.Select(value => value.Name).ToArray());
        var scenario = scenarios[selectedScenario];
        EditorGUILayout.LabelField($"Day {scenario.Day} · {PhaseName(scenario.Flow)}", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField(scenario.Description, EditorStyles.wordWrappedLabel);
        if (scenario.ExpectedStories.Length > 0)
            EditorGUILayout.LabelField("확인할 대본", string.Join(" → ", scenario.ExpectedStories), EditorStyles.wordWrappedLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            using (new EditorGUI.DisabledScope(playing || HomeInteractionTestSession.IsBusy))
                if (GUILayout.Button("선택 상황으로 테스트 시작"))
                {
                    lastResult = null;
                    HomeInteractionTestSession.Start(scenario.Id);
                }
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || !HomeInteractionTestSession.IsActive ||
                HomeInteractionTestSession.IsBusy))
                if (GUILayout.Button("선택 상황 다시 불러오기"))
                {
                    lastResult = null;
                    HomeInteractionTestSession.LoadScenarioAsync(scenario.Id).Forget(ReportError);
                }
            using (new EditorGUI.DisabledScope(!playing || !HomeInteractionTestSession.IsActive))
                if (GUILayout.Button("테스트 종료", GUILayout.Width(90))) HomeInteractionTestSession.Stop();
        }
        if (EditorApplication.isPlaying && !HomeInteractionTestSession.IsActive)
            EditorGUILayout.HelpBox("현재 Play 세션은 테스트 세션이 아닙니다. Play를 종료한 뒤 이 창에서 시작하세요.", MessageType.Info);
    }

    private void DrawRuntime()
    {
        var context = HomeInteractionTestSession.Context;
        var controller = HomeInteractionTestSession.Controller;
        var player = HomeInteractionTestSession.Player;
        var state = GameStateManager.Instance;
        EditorGUILayout.LabelField("현재 실행 상태", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("씬 / 일차 / 국면", $"{SceneManager.GetActiveScene().name} / Day {state.CurrentDay} / {PhaseName(state.GameFlow)}");
        if (player != null) EditorGUILayout.LabelField("소지금", player.HasMoney().ToString());
        string expectedScene = outside ? "OutSide" : "Home";
        if (context == null || context.gameObject.scene.name != expectedScene || controller == null || !context.IsReady || !controller.IsReady)
        {
            EditorGUILayout.LabelField(expectedScene + " 준비 중이거나 다른 씬으로 이동했습니다. 상황을 다시 불러와 계속 확인할 수 있습니다.",
                EditorStyles.wordWrappedLabel);
            return;
        }

        bool running = controller.IsRunning;
        EditorGUILayout.LabelField("상호작용", running ? controller.ActiveTarget?.name ?? "전환 중" : "입력 대기");
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(HomeInteractionTestSession.IsBusy || !running))
                if (GUILayout.Button("대사 진행 / 선택 확정")) controller.TryHandleInput();
            using (new EditorGUI.DisabledScope(HomeInteractionTestSession.IsBusy || !controller.View.IsChoosing))
            {
                if (GUILayout.Button("이전 선택", GUILayout.Width(80))) controller.View.MoveChoice(-1);
                if (GUILayout.Button("다음 선택", GUILayout.Width(80))) controller.View.MoveChoice(1);
            }
            using (new EditorGUI.DisabledScope(HomeInteractionTestSession.IsBusy || !running || !controller.CanCancel))
                if (GUILayout.Button("상호작용 취소", GUILayout.Width(100))) HomeInteractionTestSession.Cancel();
        }
        EditorGUILayout.LabelField("Game 창에서도 기존 이동·상호작용 키와 선택지를 그대로 사용할 수 있습니다.", EditorStyles.wordWrappedMiniLabel);

        bool idle = !HomeInteractionTestSession.IsBusy && !running && !context.Runner.IsRunning &&
            context.Player.State == EInteractorState.None && !UIDisplayOptions.IsOptionOpen;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("씬의 전체 상호작용", EditorStyles.boldLabel);
        foreach (var target in WorldInteractionTarget.InScene(context.gameObject.scene).OrderBy(TargetId))
            DrawTarget(target, controller, idle);

        if (!outside)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("TV 첫 시청 / 재시청", EditorStyles.boldLabel);
            bool watched = player != null && player.CheckFlag($"tv_news_d{state.CurrentDay}_seen");
            EditorGUILayout.LabelField("오늘의 시청 기록", watched ? "이미 봄 — 재시청 선택지 확인" : "아직 안 봄 — 최초 방송 확인");
            using (new EditorGUI.DisabledScope(!idle || player == null))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("처음 보는 상태로")) SetTelevisionWatched(false);
                if (GUILayout.Button("이미 본 상태로")) SetTelevisionWatched(true);
            }
            if (state.CurrentDay == 1 && state.GameFlow == EGameFlow.CommuteIn)
                EditorGUILayout.LabelField("Day 1 출근은 주변 자동 방송입니다. 재시청 선택지는 퇴근 상황에서 확인하세요.", EditorStyles.wordWrappedMiniLabel);
        }

        if (player != null)
        {
            showFlags = EditorGUILayout.Foldout(showFlags, "진행 플래그 확인", true);
            if (showFlags)
                foreach (var pair in player.ReadFlags().OrderBy(value => value.Key))
                    EditorGUILayout.LabelField(pair.Key, pair.Value ? "true" : "false");
        }
    }

    private void DrawTarget(WorldInteractionTarget target, WorldInteractionController controller, bool idle)
    {
        bool available = controller.TryResolve(target, out var story);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(TargetName(target), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(TargetId(target), EditorStyles.miniLabel);
            string detail = available
                ? $"사용 가능 · {target.StartMode} · {story.Id}"
                : UnavailableReason(target);
            EditorGUILayout.LabelField(detail, EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!idle))
                    if (GUILayout.Button(target.IsAmbient ? "접근하여 자동 방송 확인" : "대상 위치로 이동"))
                        HomeInteractionTestSession.TeleportToTarget(target);
                using (new EditorGUI.DisabledScope(!idle || !available || target.IsAmbient))
                    if (GUILayout.Button("이동 후 상호작용 실행")) PlayTargetAsync(target).Forget(ReportError);
                if (GUILayout.Button("선택", GUILayout.Width(50)))
                {
                    Selection.activeGameObject = target.gameObject;
                    EditorGUIUtility.PingObject(target.gameObject);
                }
            }
        }
    }

    private async UniTask PlayTargetAsync(WorldInteractionTarget target)
    {
        var result = await HomeInteractionTestSession.PlayTargetAsync(target);
        lastResult = $"실행 결과: {result.Status}" + (string.IsNullOrEmpty(result.Error) ? "" : $" · {result.Error}");
        if (this != null) Repaint();
    }

    private static void SetTelevisionWatched(bool value)
    {
        HomeInteractionTestSession.Player.AddFlag($"tv_news_d{GameStateManager.Instance.CurrentDay}_seen", value);
        HomeInteractionTestSession.Context.RefreshContents();
    }

    private static void DrawSaveDirectory()
    {
        if (string.IsNullOrEmpty(HomeInteractionTestSession.SaveDirectory)) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("테스트 저장 슬롯", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(HomeInteractionTestSession.SaveDirectory, EditorStyles.wordWrappedMiniLabel,
            GUILayout.Height(34));
        if (GUILayout.Button("테스트 저장 폴더 열기"))
            EditorUtility.RevealInFinder(HomeInteractionTestSession.SaveDirectory);
    }

    private static string TargetId(WorldInteractionTarget target) => target.Content != null ? target.Content.SourceId : target.StorySceneId;
    private static string TargetName(WorldInteractionTarget target) => TargetId(target) switch
    {
        "home_exit_door" => "현관문 — 출근 / 외부 이동",
        "home_sofa" => "소파 — 저장 / 취소 / 수면 / 테라스",
        "home_tv" => "TV — 날짜별 방송 / 재시청 / 접근 자동 방송",
        "neighborhood-homeCat" => "부비 — 쓰다듬기 / 취소",
        _ => target.name
    };

    private static string UnavailableReason(WorldInteractionTarget target)
    {
        if (target.Content != null && !target.Content.Definition.HasValue) return "현재 출근·퇴근 / 등장 조건에서 사용할 수 없습니다.";
        if (!target.isActiveAndEnabled) return "현재 조건에서 비활성 상태입니다.";
        if (target.Participant != null && !target.Participant.CanInteract) return "NPC 등장 또는 진행 조건이 맞지 않습니다.";
        if (target.StartMode == WorldInteractionStartMode.ScriptOnly) return "다른 사건에서만 호출하는 대상입니다.";
        return "날짜 / 대화 완료 / 진행 조건에서 사용할 수 없습니다.";
    }

    private static string PhaseName(EGameFlow value) => value switch
    {
        EGameFlow.CommuteIn => "출근",
        EGameFlow.CommuteOut => "퇴근",
        _ => value.ToString()
    };

    private void ReportError(Exception error)
    {
        lastResult = "오류: " + error.Message;
        Debug.LogError("[HomeInteractionTest] " + error.Message);
        if (this != null) Repaint();
    }
}
