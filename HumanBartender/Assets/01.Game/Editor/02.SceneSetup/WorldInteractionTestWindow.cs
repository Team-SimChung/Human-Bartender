using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Home/OutSide의 실제 상호작용 지점까지 확장한 수동 플레이 테스트 도구.</summary>
internal sealed class WorldInteractionTestWindow : EditorWindow
{
    [SerializeField] private int sceneIndex;
    [SerializeField] private int day = 1;
    [SerializeField] private bool returningHome;
    [SerializeField] private string selectedKey;
    [SerializeField] private bool startFromScript;
    [SerializeField] private string selectedScriptId;
    [SerializeField] private string scriptSearch = "";
    [SerializeField] private Vector2 scroll;
    [SerializeField] private string search = "";
    [SerializeField] private bool showFlags;
    [SerializeField] private string flagSearch = "";
    [SerializeField] private string customFlag = "";
    [SerializeField] private int money = 1000;
    [SerializeField] private WorldInteractionTestCatalog.Point[] points = Array.Empty<WorldInteractionTestCatalog.Point>();
    private NewInteractPointData[] definitions = Array.Empty<NewInteractPointData>();
    private string[] flagNames = Array.Empty<string>();
    private NewSceneData[] homeScripts = Array.Empty<NewSceneData>();
    private NewSceneData[] outsideScripts = Array.Empty<NewSceneData>();
    private string error;

    [MenuItem(WorldInteractionTestCatalog.MenuPath)]
    private static void Open()
    {
        var window = GetWindow<WorldInteractionTestWindow>("게임 상호작용 테스트");
        window.minSize = new Vector2(600, 600);
        window.Show();
    }

    private void OnEnable() => Reload();
    private void OnInspectorUpdate() => Repaint();

    private void Reload()
    {
        try
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) points = WorldInteractionTestCatalog.LoadPoints();
            definitions = WorldInteractionTestCatalog.LoadDefinitions();
            flagNames = WorldInteractionTestCatalog.LoadFlagNames();
            homeScripts = WorldInteractionTestCatalog.LoadScripts("Home");
            outsideScripts = WorldInteractionTestCatalog.LoadScripts("OutSide");
            error = null;
        }
        catch (Exception exception) { Report(exception); }
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("게임 상호작용 테스트", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Home·OutSide 씬에 배치된 실제 지점과 CSV를 사용합니다. 숨겨진 대상도 목록에 포함됩니다. " +
            "플레이어·저장은 기존 Home 테스트처럼 격리되며, 상황을 다시 불러오면 변경한 진행 조건이 초기화됩니다. " +
            "작업 중인 씬은 먼저 저장하세요. 0일차 거리의 첫 출근 강제 보행·경계는 생략합니다.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("기믹 / 칵테일 테스트")) EditorApplication.ExecuteMenuItem("Tools/Human Bartender/Tests/Gimmick Play Lab");
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("씬·CSV 목록 갱신")) Reload();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("홈 상호작용 프리셋")) EditorApplication.ExecuteMenuItem("Tools/Human Bartender/Tests/Home Interaction Lab");
            if (GUILayout.Button("외부 상호작용 프리셋")) EditorApplication.ExecuteMenuItem(OutsideInteractionTestScenarios.MenuPath);
            if (GUILayout.Button("바 내부 대사 선택")) EditorApplication.ExecuteMenuItem(BarDialogueTestCatalog.MenuPath);
        }
        DrawSetup();
        EditorGUILayout.HelpBox(HomeInteractionTestSession.Status, MessageType.None);
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        if (EditorApplication.isPlaying && HomeInteractionTestSession.IsActive) DrawRuntime();
        else if (EditorApplication.isPlayingOrWillChangePlaymode && !HomeInteractionTestSession.IsActive)
        {
            EditorGUILayout.HelpBox("현재 Play 세션을 종료한 뒤 지점 테스트를 시작하세요.", MessageType.Info);
            if (GimmickPlayTestSession.IsActive && GUILayout.Button("기믹 테스트 종료")) GimmickPlayTestSession.Stop();
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawSetup()
    {
        startFromScript = GUILayout.Toolbar(startFromScript ? 1 : 0, new[] { "상호작용 지점부터", "월드 대본부터" }) == 1;
        sceneIndex = EditorGUILayout.Popup("시작 씬", Mathf.Clamp(sceneIndex, 0, 1), WorldInteractionTestCatalog.Scenes);
        day = EditorGUILayout.IntSlider("일차", day, 0, 3);
        returningHome = EditorGUILayout.Popup("국면", returningHome ? 1 : 0, new[] { "출근", "퇴근" }) == 1;
        string sceneName = WorldInteractionTestCatalog.Scenes[sceneIndex];
        var choices = points.Where(point => point.SceneName == sceneName).ToArray();
        if (choices.Length == 0)
        {
            EditorGUILayout.HelpBox("편집 모드에서 씬·CSV 목록을 갱신하세요.", MessageType.Info);
            return;
        }
        string scriptId = startFromScript ? DrawScriptSelection(sceneName) : null;
        int selected = Array.FindIndex(choices, point => point.Key == selectedKey);
        selected = EditorGUILayout.Popup(startFromScript ? "연출을 사용할 지점" : "시작 지점", Math.Max(0, selected),
            choices.Select(point => point.Name + " · " + point.Key).ToArray());
        selectedKey = choices[selected].Key;
        EditorGUILayout.LabelField(choices[selected].HierarchyPath, EditorStyles.wordWrappedMiniLabel);
        string id = WorldInteractionTestCatalog.ScenarioId(sceneName, day,
            returningHome ? EGameFlow.CommuteOut : EGameFlow.CommuteIn);
        bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(playing || startFromScript && scriptId == null))
                if (GUILayout.Button(startFromScript ? "선택 대본부터 테스트 시작" : "선택 지점으로 테스트 시작", GUILayout.Height(28)))
                    Try(() => HomeInteractionTestSession.Start(id, selectedKey, scriptId));
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || !HomeInteractionTestSession.IsActive || HomeInteractionTestSession.IsBusy || startFromScript && scriptId == null))
                if (GUILayout.Button(startFromScript ? "조건 초기화 후 대본부터 시작" : "조건 초기화 후 선택 지점으로 이동", GUILayout.Height(28)))
                {
                    error = null;
                    HomeInteractionTestSession.LoadScenarioAsync(id, selectedKey, scriptId).Forget(Report);
                }
            using (new EditorGUI.DisabledScope(!playing || !HomeInteractionTestSession.IsActive))
                if (GUILayout.Button("테스트 종료", GUILayout.Height(28), GUILayout.Width(85))) HomeInteractionTestSession.Stop();
        }
        if (startFromScript)
        {
            var context = HomeInteractionTestSession.Context;
            var controller = HomeInteractionTestSession.Controller;
            bool canPlay = EditorApplication.isPlaying && HomeInteractionTestSession.IsActive && scriptId != null &&
                context != null && context.gameObject.scene.name == sceneName && context.IsReady &&
                controller != null && controller.IsReady && !HomeInteractionTestSession.IsBusy &&
                !controller.IsRunning && !context.Runner.IsRunning && context.Player.State == EInteractorState.None;
            using (new EditorGUI.DisabledScope(!canPlay))
                if (GUILayout.Button("현재 조건으로 선택 대본 실행 (플래그 유지)"))
                {
                    error = null;
                    PlayScript(selectedKey, scriptId).Forget(Report);
                }
        }
    }

    private string DrawScriptSelection(string sceneName)
    {
        EditorGUILayout.HelpBox("지점의 첫 메뉴를 생략하고 선택 대본 ID의 첫 단계부터 실행합니다. " +
            "아래 지점의 NPC·화자·카메라·컷씬 연결을 사용하므로 원래 이 대본을 호출하는 지점을 고르세요. " +
            "대본 진입 조건은 생략하며 각 단계·선택지의 조건과 효과는 그대로 실행합니다. " +
            "생략한 앞 대사의 플래그·등장 상태는 자동 생성하지 않습니다. 필요하면 시작 후 조건을 수정하고 현재 조건으로 실행하세요.", MessageType.Info);
        scriptSearch = EditorGUILayout.TextField("대본 검색 (ID / 제목)", scriptSearch);
        var scripts = (sceneName == "Home" ? homeScripts : outsideScripts)
            .Where(story => Contains(story.Id + " " + story.Title, scriptSearch)).ToArray();
        if (scripts.Length == 0)
        {
            EditorGUILayout.LabelField("검색 조건에 맞는 실행 가능한 대본이 없습니다.");
            return null;
        }
        int index = Array.FindIndex(scripts, story => story.Id == selectedScriptId);
        index = EditorGUILayout.Popup("시작 대본", Math.Max(0, index), scripts.Select(story =>
            story.Id + (string.IsNullOrEmpty(story.Title) ? "" : " · " + story.Title)).ToArray());
        selectedScriptId = scripts[index].Id;
        DrawStory(scripts[index]);
        EditorGUILayout.LabelField($"선택 대본 {scripts[index].Steps.Length}단계 · 앞 대본 재생 생략", EditorStyles.miniLabel);
        return selectedScriptId;
    }

    private void DrawRuntime()
    {
        var context = HomeInteractionTestSession.Context;
        var controller = HomeInteractionTestSession.Controller;
        var state = GameStateManager.Instance;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"현재: {SceneManager.GetActiveScene().name} · Day {state.CurrentDay} · {state.GameFlow}", EditorStyles.boldLabel);
        if (GUILayout.Button("Game 창 열기")) EditorApplication.ExecuteMenuItem("Window/General/Game");
        if (context == null || !context.IsReady || controller == null || !controller.IsReady)
        {
            EditorGUILayout.LabelField("씬 준비 중이거나 월드 상호작용이 없는 씬입니다. 위에서 상황을 다시 불러올 수 있습니다.", EditorStyles.wordWrappedLabel);
            return;
        }
        if (controller.SuppressDetectionForTest)
        {
            EditorGUILayout.HelpBox("대본 직접 테스트 중: 주변 자동 사건·접근 대화 감지가 멈춰 있습니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(HomeInteractionTestSession.IsBusy || controller.IsRunning || context.Runner.IsRunning ||
                context.Player.State != EInteractorState.None))
                if (GUILayout.Button("접근 자동 감지 재개")) Try(HomeInteractionTestSession.ResumeDetection);
        }
        EditorGUILayout.LabelField("진행 중", controller.IsRunning ? controller.ActiveTarget?.name ?? "상호작용" : "입력 대기");
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(HomeInteractionTestSession.IsBusy || !controller.IsRunning))
                if (GUILayout.Button("대사 진행 / 선택 확정")) controller.TryHandleInput();
            using (new EditorGUI.DisabledScope(HomeInteractionTestSession.IsBusy || !controller.View.IsChoosing))
            {
                if (GUILayout.Button("이전 선택")) controller.View.MoveChoice(-1);
                if (GUILayout.Button("다음 선택")) controller.View.MoveChoice(1);
            }
            using (new EditorGUI.DisabledScope(HomeInteractionTestSession.IsBusy || !controller.IsRunning || !controller.CanCancel))
                if (GUILayout.Button("상호작용 취소")) HomeInteractionTestSession.Cancel();
        }
        bool idle = !HomeInteractionTestSession.IsBusy && !controller.IsRunning && !context.Runner.IsRunning &&
            context.Player.State == EInteractorState.None;
        search = EditorGUILayout.TextField("현재 씬 지점 검색", search);
        var targets = WorldInteractionTestCatalog.Targets(context.gameObject.scene);
        EditorGUILayout.LabelField($"현재 씬 지점 {targets.Length}개 (비활성 포함)", EditorStyles.miniBoldLabel);
        foreach (var target in targets)
            if (Contains(target.name + " " + WorldInteractionTestCatalog.Key(target), search)) DrawTarget(target, controller, idle);
        DrawConditions(idle);
        if (!string.IsNullOrEmpty(HomeInteractionTestSession.SaveDirectory) && GUILayout.Button("테스트 저장 폴더 열기"))
            EditorUtility.RevealInFinder(HomeInteractionTestSession.SaveDirectory);
    }

    private void DrawTarget(WorldInteractionTarget target, WorldInteractionController controller, bool idle)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            bool available = controller.TryResolve(target, out var story);
            EditorGUILayout.LabelField(target.name + " · " + WorldInteractionTestCatalog.Key(target), EditorStyles.boldLabel);
            string reason = !target.isActiveAndEnabled ? "비활성 · 등장 조건/씬 배치 확인"
                : target.StartMode == WorldInteractionStartMode.ScriptOnly ? "다른 사건에서만 호출"
                : available ? "실행 가능" : "현재 날짜·국면·플래그·완료 기록 또는 대상 상태로 실행 불가";
            EditorGUILayout.LabelField($"{reason} · {target.StartMode} · 반경 {target.InteractionRadius:0.##}", EditorStyles.wordWrappedMiniLabel);
            if (available) DrawStory(story);
            else if (target.Content?.Dialogue is { } dialogue) DrawStory(dialogue);
            else if (target.Content == null)
            {
                var data = target.gameObject.scene.name == "Home" ? NewDataLoadManager.HomeStory : NewDataLoadManager.StreetStory;
                foreach (var candidate in data.Scenes ?? Array.Empty<NewSceneData>())
                    if (candidate.Id == target.StorySceneId) DrawStory(candidate);
            }
            if (target.Content != null)
                foreach (var definition in definitions.Where(value => value.SourceId == target.Content.SourceId))
                    EditorGUILayout.LabelField($"CSV {definition.Id} · {definition.Phase} · 등장: {OrAlways(definition.SpawnWhen)} · 상호작용: {OrAlways(definition.InteractWhen)}",
                        EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!idle))
                {
                    if (GUILayout.Button(target.IsAmbient || target.StartMode == WorldInteractionStartMode.Automatic
                        ? "접근하여 자동 실행 확인" : "대상 위치로 이동")) Try(() => HomeInteractionTestSession.TeleportToTarget(target));
                    using (new EditorGUI.DisabledScope(!available || target.IsAmbient))
                        if (GUILayout.Button("이동 후 상호작용 실행")) Play(target).Forget(Report);
                }
                if (GUILayout.Button("선택", GUILayout.Width(48)))
                {
                    Selection.activeGameObject = target.gameObject;
                    EditorGUIUtility.PingObject(target);
                }
            }
        }
    }

    private void DrawConditions(bool idle)
    {
        var player = HomeInteractionTestSession.Player;
        if (player == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("테스트 진행 조건", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("현재 소지금", player.HasMoney().ToString());
        using (new EditorGUI.DisabledScope(!idle))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                money = EditorGUILayout.IntField("변경할 소지금", money);
                if (GUILayout.Button("적용", GUILayout.Width(60))) Try(() => HomeInteractionTestSession.SetMoney(money));
            }
            showFlags = EditorGUILayout.Foldout(showFlags, "플래그 수정 (CSV 조건 + 현재 플레이어)", true);
            if (!showFlags) return;
            flagSearch = EditorGUILayout.TextField("플래그 검색", flagSearch);
            foreach (string key in flagNames.Concat(player.ReadFlags().Keys).Distinct().OrderBy(value => value).ToArray())
            {
                if (!Contains(key, flagSearch)) continue;
                bool previous = player.CheckFlag(key);
                bool next = EditorGUILayout.ToggleLeft(key, previous);
                if (next != previous) Try(() => HomeInteractionTestSession.SetFlag(key, next));
            }
            customFlag = EditorGUILayout.TextField("직접 입력할 플래그", customFlag);
            using (new EditorGUILayout.HorizontalScope())
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(customFlag)))
            {
                if (GUILayout.Button("입력 플래그 켜기")) Try(() => HomeInteractionTestSession.SetFlag(customFlag.Trim(), true));
                if (GUILayout.Button("입력 플래그 끄기")) Try(() => HomeInteractionTestSession.SetFlag(customFlag.Trim(), false));
            }
        }
    }

    private static void DrawStory(NewSceneData story) => EditorGUILayout.LabelField(
        $"대본 {story.Id} · Day {story.Day?.ToString() ?? "전체"} · {story.Route} · 조건: {OrAlways(story.When)} · 완료: {OrAlways(story.CompletionFlag)}",
        EditorStyles.wordWrappedMiniLabel);

    private static string OrAlways(string value) => string.IsNullOrWhiteSpace(value) ? "없음" : value;
    private static bool Contains(string value, string query) => string.IsNullOrEmpty(query) || value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

    private async UniTask Play(WorldInteractionTarget target)
    {
        error = null;
        var result = await HomeInteractionTestSession.PlayTargetAsync(target);
        if (!string.IsNullOrEmpty(result.Error)) error = result.Error;
    }

    private async UniTask PlayScript(string key, string scriptId)
    {
        var result = await HomeInteractionTestSession.PlayScriptAsync(
            WorldInteractionTestCatalog.FindTarget(SceneManager.GetActiveScene(), key), scriptId);
        if (!string.IsNullOrEmpty(result.Error)) error = result.Error;
    }

    private void Try(Action action)
    {
        try { error = null; action(); }
        catch (Exception exception) { Report(exception); }
    }

    private void Report(Exception exception)
    {
        if (exception is OperationCanceledException) return;
        error = exception.Message;
        Debug.LogError("[WorldTest] " + exception.Message);
    }
}
