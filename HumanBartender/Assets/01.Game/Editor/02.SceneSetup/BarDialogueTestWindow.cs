using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>바 내부의 대사 본문을 골라 그 줄부터 플레이하는 수동 테스트 창.</summary>
internal sealed class BarDialogueTestWindow : EditorWindow
{
    [SerializeField] private int day;
    [SerializeField] private string search = "";
    [SerializeField] private string selectedKey;
    [SerializeField] private int phaseFilter;
    [SerializeField] private ENewGrade previousGrade = ENewGrade.Excellent;
    [SerializeField] private bool previousOrderMatch = true;
    [SerializeField] private Vector2 scroll;
    [SerializeField] private Vector2 linesScroll;
    private BarDialogueTestCatalog.Line[] lines = Array.Empty<BarDialogueTestCatalog.Line>();
    private string error;

    [MenuItem(BarDialogueTestCatalog.MenuPath)]
    private static void Open()
    {
        var window = GetWindow<BarDialogueTestWindow>("바 내부 대사 테스트");
        window.minSize = new Vector2(680, 640);
        window.Show();
    }

    private void OnEnable() => Reload();
    private void OnInspectorUpdate() => Repaint();

    private void Reload()
    {
        try { lines = BarDialogueTestCatalog.LoadLines(day); error = null; }
        catch (Exception exception) { lines = Array.Empty<BarDialogueTestCatalog.Line>(); Report(exception); }
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("바 내부 · 대사를 골라 시작", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("실제 Play 씬과 바 대본을 사용합니다. 앞 대사·컷씬·튜토리얼은 생략하고, " +
            "그 시점의 좌석·인물·미완료 주문·스텝 효과를 테스트 복사본에 준비합니다. " +
            "앞 선택지의 결과는 자동으로 고르지 않습니다. 선택한 첫 줄은 표시하고 이후 대사·선택·제조는 원래 로직으로 진행합니다.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("홈 상호작용")) EditorApplication.ExecuteMenuItem("Tools/Human Bartender/Tests/Home Interaction Lab");
            if (GUILayout.Button("외부 상호작용")) EditorApplication.ExecuteMenuItem(OutsideInteractionTestScenarios.MenuPath);
            if (GUILayout.Button("기믹 / 칵테일")) EditorApplication.ExecuteMenuItem("Tools/Human Bartender/Tests/Gimmick Play Lab");
        }
        int nextDay = EditorGUILayout.IntSlider("대사 일차", day, 0, 3);
        if (nextDay != day) { day = nextDay; selectedKey = null; Reload(); }
        search = EditorGUILayout.TextField("대사 검색 (본문 / 인물 / ID)", search);
        phaseFilter = GUILayout.Toolbar(phaseFilter, new[] { "전체 대사", "개점 대사", "2부 대사" });
        if (GUILayout.Button("CSV 대사 목록 갱신")) Reload();
        var filtered = lines.Where(line => Matches(line) && (phaseFilter == 0 ||
            line.Phase == (phaseFilter == 1 ? ENewScenePhase.BarOpen : ENewScenePhase.Bar))).ToArray();
        EditorGUILayout.LabelField($"검색 결과 {filtered.Length}줄 / 전체 {lines.Length}줄", EditorStyles.miniBoldLabel);
        linesScroll = EditorGUILayout.BeginScrollView(linesScroll, EditorStyles.helpBox, GUILayout.Height(240));
        foreach (var line in filtered)
        {
            string body = line.Text.Replace('\n', ' ').Replace('\r', ' ');
            string label = $"[{PhaseName(line.Phase)}] {line.Step.Actor} · {body}";
            if (GUILayout.Toggle(selectedKey == line.Key, new GUIContent(label, line.Key + "\n" + line.Text),
                EditorStyles.miniButton, GUILayout.Height(23), GUILayout.Width(Mathf.Max(250, position.width - 70)))) selectedKey = line.Key;
        }
        EditorGUILayout.EndScrollView();
        var selected = lines.FirstOrDefault(line => line.Key == selectedKey);
        if (selected != null)
        {
            EditorGUILayout.LabelField($"선택: {selected.Step.Actor} · {selected.SceneId} #{selected.Step.Seq}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(selected.Text, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("원래 대사 조건", string.IsNullOrEmpty(selected.Step.When) ? "없음" : selected.Step.When,
                EditorStyles.wordWrappedMiniLabel);
        }
        else EditorGUILayout.HelpBox("위 목록에서 시작할 대사 한 줄을 선택하세요.", MessageType.Info);
        previousGrade = (ENewGrade)EditorGUILayout.EnumPopup("생략한 앞 서빙의 등급", previousGrade);
        previousOrderMatch = EditorGUILayout.Toggle("생략한 앞 서빙의 주문 일치", previousOrderMatch);
        EditorGUILayout.LabelField("이 값은 건너뛴 서빙 뒤 분기를 준비할 때만 사용합니다. 이후 실제 서빙은 실제 결과를 사용합니다. " +
            "미완료 자유 주문은 해당 일차에 사용 가능한 레시피를 자동 선택합니다.", EditorStyles.wordWrappedMiniLabel);
        bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(playing || selected == null))
                if (GUILayout.Button("선택 대사부터 테스트 시작", GUILayout.Height(30)))
                    Try(() => HomeInteractionTestSession.StartBarDialogue(day, selected.SceneId, selected.Step.Seq, previousGrade, previousOrderMatch));
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || !HomeInteractionTestSession.IsActive ||
                HomeInteractionTestSession.IsBusy || selected == null))
                if (GUILayout.Button("조건 초기화 후 선택 대사부터 다시", GUILayout.Height(30)))
                {
                    error = null;
                    HomeInteractionTestSession.ReloadBarDialogueAsync(day, selected.SceneId, selected.Step.Seq,
                        previousGrade, previousOrderMatch).Forget(Report);
                }
            using (new EditorGUI.DisabledScope(!HomeInteractionTestSession.IsActive || !playing))
                if (GUILayout.Button("테스트 종료", GUILayout.Height(30), GUILayout.Width(90))) HomeInteractionTestSession.Stop();
        }
        EditorGUILayout.HelpBox(HomeInteractionTestSession.Status, MessageType.None);
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        if (EditorApplication.isPlaying && HomeInteractionTestSession.IsActive)
        {
            EditorGUILayout.LabelField($"현재: {SceneManager.GetActiveScene().name} · Day {GameStateManager.Instance.CurrentDay}");
            var runner = HomeInteractionTestSession.BarRunner;
            if (runner != null)
            {
                EditorGUILayout.LabelField("현재 대사 ID", runner.CurrentDialogueId ?? "연출·선택·제조 또는 대기");
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!HomeInteractionTestSession.IsBarPlaying || !runner.IsPresentingLine || UIDisplayOptions.IsOptionOpen))
                        if (GUILayout.Button("대사 진행")) UnityEngine.Object.FindFirstObjectByType<StoryFlow>()?.TryAdvance();
                    using (new EditorGUI.DisabledScope(!HomeInteractionTestSession.IsBarPlaying))
                        if (GUILayout.Button("대사 테스트 중단")) HomeInteractionTestSession.CancelBarDialogue();
                    if (GUILayout.Button("Game 창 열기")) EditorApplication.ExecuteMenuItem("Window/General/Game");
                }
                EditorGUILayout.LabelField("선택지와 제조·서빙은 Game 창에서 조작합니다. 대본 종료 후 정산·퇴근은 자동으로 실행하지 않습니다.",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }
        else if (playing) EditorGUILayout.HelpBox("다른 Play 세션을 종료한 뒤 이 창에서 시작하세요.", MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    private bool Matches(BarDialogueTestCatalog.Line line) => string.IsNullOrEmpty(search) ||
        (line.Text + " " + line.Step.Actor + " " + line.Step.DialogueId + " " + line.SceneId)
            .IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    private static string PhaseName(ENewScenePhase phase) => phase == ENewScenePhase.BarOpen ? "개점" : "2부";
    private void Try(Action action)
    {
        try { error = null; action(); }
        catch (Exception exception) { Report(exception); }
    }
    private void Report(Exception exception)
    {
        if (exception is OperationCanceledException) return;
        error = exception.Message;
        Debug.LogError("[BarTest] " + exception.Message);
    }
}
