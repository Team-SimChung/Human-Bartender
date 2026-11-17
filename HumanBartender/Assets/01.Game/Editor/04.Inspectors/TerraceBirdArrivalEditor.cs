using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(TerraceBirdArrival))]
public sealed class TerraceBirdArrivalEditor : Editor
{
    private static NewStreetData cachedHome;
    private static DateTime cachedStepsTime, cachedScenesTime;

    public static Step[] GetSteps(HomeTerracePresentation terrace, int day)
    {
        string id = terrace != null ? terrace.GetStoryIdForDay(day) : null;
        if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("[TerraceBird] 해당 날짜의 테라스 대본 ID가 없습니다.");
        string directory = Path.Combine(Application.streamingAssetsPath, "csv");
        var stepsTime = File.GetLastWriteTimeUtc(Path.Combine(directory, "story/steps.csv"));
        var scenesTime = File.GetLastWriteTimeUtc(Path.Combine(directory, "story/scenes.csv"));
        if (cachedHome.Scenes == null || cachedStepsTime != stepsTime || cachedScenesTime != scenesTime)
        {
            var catalog = CsvDataReader.LoadDirectory(directory);
            cachedHome = StoryCsvCompiler.CompileHome(catalog);
            cachedStepsTime = stepsTime;
            cachedScenesTime = scenesTime;
        }
        var story = (cachedHome.Scenes ?? Array.Empty<NewSceneData>()).Single(scene => scene.Id == id);
        return (story.Steps ?? Array.Empty<Step>()).OrderBy(step => step.Seq).ToArray();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "days", "daySettingsVersion");
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("현재 비둘기 배치에 착지점 맞추기"))
            {
                serializedObject.ApplyModifiedProperties();
                MatchLandingToPlacement((TerraceBirdArrival)target);
                serializedObject.Update();
            }
        }
        EditorGUILayout.HelpBox("Pigeon을 기본 위치로 옮긴 뒤 위 버튼을 누르면 착지점과 출발점을 함께 맞춥니다.", MessageType.None);
        var days = serializedObject.FindProperty("days");
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(days, new GUIContent("날짜별 설정"), true);
        var duplicates = Enumerable.Range(0, days.arraySize).Select(index => days.GetArrayElementAtIndex(index)
            .FindPropertyRelative("day").intValue).GroupBy(day => day).Where(group => group.Count() > 1)
            .Select(group => group.Key + "일차").ToArray();
        if (duplicates.Length > 0)
            EditorGUILayout.HelpBox("중복 날짜: " + string.Join(", ", duplicates) + ". 날짜마다 한 항목만 등록하세요.", MessageType.Error);
        EditorGUILayout.HelpBox("각 항목의 날짜·시작 스텝·생성 여부를 설정하세요. 배열 순서는 무관하며 같은 날짜는 한 번만 등록합니다. 0은 처음부터 Idle, 1 이상은 해당 스텝에 날아옵니다. 미등록 날짜와 생성 체크를 끈 날짜에는 나타나지 않습니다.", MessageType.None);
        if (new[] { "bird", "birdAnimator", "landingPoint" }
            .Any(field => serializedObject.FindProperty(field).objectReferenceValue == null))
            EditorGUILayout.HelpBox("비둘기·Animator·Landing Point를 모두 연결하세요.", MessageType.Error);
        if (Enumerable.Range(0, days.arraySize).Select(days.GetArrayElementAtIndex).Any(timing =>
                timing.FindPropertyRelative("spawn").boolValue && timing.FindPropertyRelative("startStep").intValue > 0) &&
            serializedObject.FindProperty("flightStart").objectReferenceValue == null)
            EditorGUILayout.HelpBox("날아오는 연출에는 Flight Start도 연결하세요.", MessageType.Error);
        var animator = serializedObject.FindProperty("birdAnimator").objectReferenceValue as Animator;
        if (animator != null && animator.runtimeAnimatorController is AnimatorController controller &&
            !controller.parameters.Any(parameter => parameter.name == "IsFlying" && parameter.type == AnimatorControllerParameterType.Bool))
            EditorGUILayout.HelpBox("Animator에 IsFlying Bool이 필요합니다. 비행 클립이 있는 Pigeon 컨트롤러를 연결하세요.", MessageType.Error);
        serializedObject.ApplyModifiedProperties();
    }

    public static bool MatchLandingToPlacement(TerraceBirdArrival arrival)
    {
        if (arrival == null || EditorApplication.isPlayingOrWillChangePlaymode) return false;
        using var data = new SerializedObject(arrival);
        var bird = data.FindProperty("bird").objectReferenceValue as Transform;
        var landing = data.FindProperty("landingPoint").objectReferenceValue as Transform;
        var start = data.FindProperty("flightStart").objectReferenceValue as Transform;
        if (bird == null || landing == null || bird == landing || landing.IsChildOf(bird) || bird.IsChildOf(landing) ||
            (start != null && (start == bird || start == landing || start.IsChildOf(bird) || start.IsChildOf(landing) || bird.IsChildOf(start) || landing.IsChildOf(start))))
        {
            Debug.LogWarning("[TerraceBird] 비둘기와 별도의 착지점·출발점을 연결하세요.", arrival);
            return false;
        }
        var offset = bird.position - landing.position;
        if (offset == Vector3.zero) return false;
        Undo.RecordObject(landing, "Match pigeon landing to placement");
        if (start != null) Undo.RecordObject(start, "Match pigeon flight to placement");
        landing.position = bird.position;
        if (start != null) start.position += offset;
        EditorUtility.SetDirty(landing);
        if (start != null) EditorUtility.SetDirty(start);
        EditorSceneManager.MarkSceneDirty(arrival.gameObject.scene);
        return true;
    }
}

[CustomPropertyDrawer(typeof(TerraceBirdArrival.DayTiming))]
public sealed class TerraceBirdDayTimingDrawer : PropertyDrawer
{
    public static string Preview(SerializedProperty timing, out Step[] steps, out MessageType type)
    {
        steps = Array.Empty<Step>();
        type = MessageType.Info;
        int day = timing.FindPropertyRelative("day").intValue;
        int start = timing.FindPropertyRelative("startStep").intValue;
        var days = timing.serializedObject.FindProperty("days");
        if (Enumerable.Range(0, days.arraySize).Count(index => days.GetArrayElementAtIndex(index)
                .FindPropertyRelative("day").intValue == day) > 1)
        {
            type = MessageType.Error;
            return day + "일차가 중복되어 있습니다. 해당 날짜는 한 항목만 남겨야 나타납니다.";
        }
        try
        {
            var terrace = timing.serializedObject.FindProperty("terrace").objectReferenceValue as HomeTerracePresentation;
            steps = TerraceBirdArrivalEditor.GetSteps(terrace, day);
            if (steps.Length == 0) throw new InvalidOperationException("[TerraceBird] 대본에 스텝이 없습니다.");
            string range = "CSV 스텝 범위 " + steps.First().Seq + " ~ " + steps.Last().Seq;
            string state = timing.FindPropertyRelative("spawn").boolValue ? "생성 켜짐" : "생성 꺼짐";
            if (start == 0) return state + " · " + range + "\n처음부터 착지 위치에서 Idle을 재생합니다.";
            var matches = steps.Where(step => step.Seq == start).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("[TerraceBird] 선택한 시작 스텝이 현재 CSV에 없습니다.");
            var step = matches[0];
            return state + " · " + range + "\nseq " + step.Seq + " · " + step.Type + " · " + step.Actor + "\n" +
                (step.Text?.Ko ?? step.Arg ?? "") + (string.IsNullOrEmpty(step.When) ? "" : "\n조건: " + step.When);
        }
        catch (Exception error) { type = MessageType.Error; return error.Message; }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight, gap = EditorGUIUtility.standardVerticalSpacing;
        if (!property.isExpanded) return line;
        string text = Preview(property, out _, out _);
        float message = EditorStyles.helpBox.CalcHeight(new GUIContent(text), Mathf.Max(80, EditorGUIUtility.currentViewWidth - 80));
        return line * 4 + gap * 4 + Mathf.Max(line * 2, message);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var scope = new EditorGUI.PropertyScope(position, label, property);
        float line = EditorGUIUtility.singleLineHeight, gap = EditorGUIUtility.standardVerticalSpacing;
        var row = new Rect(position.x, position.y, position.width, line);
        var day = property.FindPropertyRelative("day");
        var start = property.FindPropertyRelative("startStep");
        var spawn = property.FindPropertyRelative("spawn");
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded,
            day.intValue + "일차 · 시작 " + start.intValue + " · " + (spawn.boolValue ? "생성" : "미생성"), true);
        if (!property.isExpanded) return;
        using var indent = new EditorGUI.IndentLevelScope();
        row.y += line + gap;
        EditorGUI.PropertyField(row, day, new GUIContent("날짜"));
        string text = Preview(property, out var steps, out var type);
        row.y += line + gap;
        if (steps.Length > 0)
        {
            EditorGUI.BeginChangeCheck();
            int value = EditorGUI.IntSlider(row, new GUIContent("시작 스텝 (0: 처음부터)"), start.intValue, 0, steps.Last().Seq);
            if (EditorGUI.EndChangeCheck()) start.intValue = value;
        }
        else EditorGUI.PropertyField(row, start, new GUIContent("시작 스텝 (0: 처음부터)"));
        row.y += line + gap;
        EditorGUI.PropertyField(row, spawn, new GUIContent("생성 여부"));
        row.y += line + gap;
        row.height = Mathf.Max(line, position.yMax - row.y);
        EditorGUI.HelpBox(EditorGUI.IndentedRect(row), text, type);
    }
}
