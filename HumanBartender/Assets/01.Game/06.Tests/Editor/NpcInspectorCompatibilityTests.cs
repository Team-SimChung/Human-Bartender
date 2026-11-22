using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>기존 Inspector 표시 계약과 여러 NPC를 보기만 했을 때의 설정 보존을 검사한다.</summary>
public sealed class NpcInspectorCompatibilityTests
{
    private NpcInspectorTestData first;
    private NpcInspectorTestData second;
    private SerializedObject data;
    private NpcInspectorTestWindow window;

    [TearDown]
    public void TearDown()
    {
        if (window != null) window.Close();
        window = null;
        data?.Dispose();
        data = null;
        if (first != null) Object.DestroyImmediate(first);
        if (second != null) Object.DestroyImmediate(second);
    }

    [Test]
    public void ActionTitlesAndRepresentativeLayoutsKeepTheirExistingInspectorContract()
    {
        CreateTargets(false);
        var step = FirstStep();
        var kind = step.FindPropertyRelative("kind");
        var title = typeof(NpcActionStepDrawer).GetMethod("Title", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(SerializedProperty) }, null);
        Assert.That(title, Is.Not.Null);
        string[] originalTitles =
        {
            "상대 이동", "대상에게 접근", "대상 바라보기", "애니메이션 재생", "대기", "화면 암전", "화면 복귀",
            "이동 후 퇴장", "목표 완료 알림", "NPC 표시", "NPC 숨김", "기준 위치로 이동", "기준 위치에 배치",
            "페이드 후 숨김", "효과음", "이동하며 페이드 퇴장", "목적지로 이동하며 퇴장", "포물선 이동",
            "플레이어 위치 맞추기", "플레이어 위치 설정", "플레이어 바라보기 방향", "카메라 구도 맞추기",
            "카메라 이동", "카메라 추적 복귀", "카메라 구도 즉시 설정"
        };
        var kinds = (NpcActionKind[])Enum.GetValues(typeof(NpcActionKind));
        Assert.That(kinds.Length, Is.EqualTo(originalTitles.Length));
        for (int i = 0; i < kinds.Length; i++)
        {
            kind.intValue = (int)kinds[i];
            Assert.That((string)title.Invoke(null, new object[] { kind }), Is.EqualTo(originalTitles[i]), kinds[i].ToString());
        }

        AssertLayout(step, NpcActionKind.DepartWhileMoving,
            "kind", "performer", "offset", "offsetMode", "target", "walkDuration", "duration", "fadeEase");
        AssertLayout(step, NpcActionKind.Approach, "kind", "performer", "target", "distance");
        AssertLayout(step, NpcActionKind.CameraPan, "kind", "performer", "positionOrigin", "target", "offset", "offsetMode",
            "horizontalOnly", "cameraMidpoint", "cameraWidth", "duration", "movementEase", "fadeInTogether");
    }

    [Test]
    public void CsvMenuKeepsEveryExistingLabelPairedWithItsCommandValue()
    {
        var labels = (GUIContent[])ReadStaticField(typeof(NpcSequenceDrawer), "CsvCommands");
        var values = (int[])ReadStaticField(typeof(NpcSequenceDrawer), "CsvCommandValues");
        var expected = new (OutsideStoryWorldAction command, string label)[]
        {
            (OutsideStoryWorldAction.RepairElevator, "수리공 · 엘리베이터 수리"),
            (OutsideStoryWorldAction.AdoptCat, "부비 · 입양"),
            (OutsideStoryWorldAction.RevealCat, "부비 · 발견"),
            (OutsideStoryWorldAction.PetCat, "부비 · 쓰다듬기"),
            (OutsideStoryWorldAction.FeedCat, "부비 · 먹이"),
            (OutsideStoryWorldAction.NeighborEnter, "이웃 · 등장"),
            (OutsideStoryWorldAction.NeighborExit, "이웃 · 퇴장"),
            (OutsideStoryWorldAction.ChrisEnter, "크리스 · 등장"),
            (OutsideStoryWorldAction.CatMeow, "부비 · 울음"),
            (OutsideStoryWorldAction.SamhoDepart, "출근 삼호 · 퇴장"),
            (OutsideStoryWorldAction.NotionApproach, "계약 삼호 · 루나에게 접근"),
            (OutsideStoryWorldAction.NotionDealerExit, "계약 밀수업자 · 퇴장"),
            (OutsideStoryWorldAction.NotionSamhoExit, "계약 삼호 · 퇴장"),
            (OutsideStoryWorldAction.ThugPunch, "불량배 · 타격"),
            (OutsideStoryWorldAction.ThugDepart, "불량배 · 퇴장"),
            (OutsideStoryWorldAction.DoorCue, "공통 · 문 효과")
        };
        Assert.That(labels.Length, Is.EqualTo(expected.Length));
        Assert.That(values.Length, Is.EqualTo(expected.Length));
        Assert.That(values, Is.Unique);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.That(labels[i].text, Is.EqualTo(expected[i].label));
            Assert.That(values[i], Is.EqualTo((int)expected[i].command), expected[i].label);
        }
    }

    [UnityTest]
    public IEnumerator ViewingDifferentCommandsDoesNotCopyTheFirstNpcSettingsToOthers()
    {
        CreateTargets(true);
        Assert.That(data.FindProperty("sequence.worldAction").hasMultipleDifferentValues, Is.True);
        Assert.That(data.FindProperty("sequence.trigger").hasMultipleDifferentValues, Is.False);
        string originalFirst = JsonUtility.ToJson(first);
        string originalSecond = JsonUtility.ToJson(second);

        OpenWindow();
        yield return WaitForRepaints();

        AssertReadonlyDrawing(originalFirst, originalSecond);
    }

    [UnityTest]
    public IEnumerator MixedTriggersAndActionKindsOnlyShowCommonFieldsWithoutChangingEitherNpc()
    {
        CreateTargets(true);
        second.sequence.trigger = NpcSequenceTrigger.Named;
        second.sequence.actions[0].kind = NpcActionKind.Animation;
        second.sequence.actions[0].stateName = "사용자 애니메이션";
        // 두 대상의 작성 상태를 확정한 뒤 새 선택을 만들어 차이 비교 캐시를 갱신한다.
        data.Dispose();
        data = new SerializedObject(new Object[] { first, second });
        var sequence = data.FindProperty("sequence");
        var step = FirstStep();
        sequence.isExpanded = true;
        step.isExpanded = true;
        Assert.That(sequence.FindPropertyRelative("trigger").hasMultipleDifferentValues, Is.True);
        Assert.That(step.FindPropertyRelative("kind").hasMultipleDifferentValues, Is.True);
        Assert.That(new NpcActionStepDrawer().GetPropertyHeight(step, GUIContent.none),
            Is.EqualTo(EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing));
        Assert.That(new NpcSequenceDrawer().GetPropertyHeight(sequence, GUIContent.none),
            Is.EqualTo(ExpectedHeight(sequence, "id", "trigger", "requiredCondition", "actions")));
        string originalFirst = JsonUtility.ToJson(first);
        string originalSecond = JsonUtility.ToJson(second);

        OpenWindow();
        yield return WaitForRepaints();

        AssertReadonlyDrawing(originalFirst, originalSecond);
    }

    private void CreateTargets(bool multiple)
    {
        first = ScriptableObject.CreateInstance<NpcInspectorTestData>();
        first.hideFlags = HideFlags.HideAndDontSave;
        first.sequence = CreateSequence("첫 NPC", OutsideStoryWorldAction.RepairElevator, new Vector2(.525f, 0), 1.1f, .5f);
        if (multiple)
        {
            second = ScriptableObject.CreateInstance<NpcInspectorTestData>();
            second.hideFlags = HideFlags.HideAndDontSave;
            second.sequence = CreateSequence("둘째 NPC", OutsideStoryWorldAction.NotionDealerExit, new Vector2(-4, 2), .8f, .3f);
            data = new SerializedObject(new Object[] { first, second });
        }
        else data = new SerializedObject(first);
    }

    private static NpcSequence CreateSequence(string id, OutsideStoryWorldAction command, Vector2 offset, float walk, float fade)
    {
        return new NpcSequence
        {
            id = id, trigger = NpcSequenceTrigger.WorldAction, worldAction = command, requiredCondition = "flag." + id,
            actions = new[]
            {
                new NpcActionStep
                {
                    kind = NpcActionKind.DepartWhileMoving, offset = offset, walkDuration = walk, duration = fade,
                    text = id + " 알림", stateName = id + " 동작", returnState = id + " 복귀"
                }
            }
        };
    }

    private SerializedProperty FirstStep()
    {
        return data.FindProperty("sequence.actions").GetArrayElementAtIndex(0);
    }

    private void OpenWindow()
    {
        var sequence = data.FindProperty("sequence");
        sequence.isExpanded = true;
        sequence.FindPropertyRelative("actions").isExpanded = true;
        FirstStep().isExpanded = true;
        window = ScriptableObject.CreateInstance<NpcInspectorTestWindow>();
        window.hideFlags = HideFlags.HideAndDontSave;
        window.Initialize(data);
        window.titleContent = new GUIContent("NPC Inspector 검사");
        window.position = new Rect(200, 150, 620, 760);
        window.ShowUtility();
        window.Repaint();
    }

    private IEnumerator WaitForRepaints()
    {
        double deadline = EditorApplication.timeSinceStartup + 15;
        while (window.Repaints < 2 && window.Failure == null && EditorApplication.timeSinceStartup < deadline)
        {
            window.Repaint();
            yield return null;
        }
        Assert.That(window.Failure, Is.Null);
        Assert.That(window.Repaints, Is.GreaterThanOrEqualTo(2), "실제 Inspector 그리기를 두 번 확인해야 합니다.");
    }

    private void AssertReadonlyDrawing(string originalFirst, string originalSecond)
    {
        Assert.That(window.AppliedAnyChanges, Is.False, "보기만 한 Inspector가 직렬화 값을 기록하면 안 됩니다.");
        Assert.That(window.RestoredGuiState, Is.True, window.GuiStateFailure);
        Assert.That(JsonUtility.ToJson(first), Is.EqualTo(originalFirst));
        Assert.That(JsonUtility.ToJson(second), Is.EqualTo(originalSecond));
    }

    private static void AssertLayout(SerializedProperty step, NpcActionKind kind, params string[] fields)
    {
        step.FindPropertyRelative("kind").intValue = (int)kind;
        step.isExpanded = true;
        Assert.That(new NpcActionStepDrawer().GetPropertyHeight(step, GUIContent.none), Is.EqualTo(ExpectedHeight(step, fields)), kind.ToString());
    }

    private static float ExpectedHeight(SerializedProperty property, params string[] fields)
    {
        float height = EditorGUIUtility.singleLineHeight;
        foreach (string field in fields)
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) + EditorGUIUtility.standardVerticalSpacing;
        return height;
    }

    private static object ReadStaticField(Type owner, string name)
    {
        var field = owner.GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(field, Is.Not.Null, name);
        return field.GetValue(null);
    }
}

internal sealed class NpcInspectorTestData : ScriptableObject
{
    public NpcSequence sequence;
}

internal sealed class NpcInspectorTestWindow : EditorWindow
{
    private SerializedObject data;
    private readonly NpcSequenceDrawer drawer = new();
    internal Exception Failure { get; private set; }
    internal int Repaints { get; private set; }
    internal bool AppliedAnyChanges { get; private set; }
    internal bool RestoredGuiState { get; private set; } = true;
    internal string GuiStateFailure { get; private set; }

    internal void Initialize(SerializedObject targets)
    {
        data = targets;
    }

    private void OnGUI()
    {
        if (data == null || Failure != null) return;
        int priorIndent = EditorGUI.indentLevel;
        bool priorMixed = EditorGUI.showMixedValue;
        try
        {
            data.Update();
            var sequence = data.FindProperty("sequence");
            EditorGUI.indentLevel = 2;
            EditorGUI.showMixedValue = true;
            drawer.OnGUI(new Rect(4, 4, position.width - 8, drawer.GetPropertyHeight(sequence, GUIContent.none)),
                sequence, new GUIContent("Element 0"));
            bool restored = EditorGUI.indentLevel == 2 && EditorGUI.showMixedValue;
            if (!restored && GuiStateFailure == null)
                GuiStateFailure = $"{Event.current.type}: 들여쓰기={EditorGUI.indentLevel} (기대 2), 혼합 표시={EditorGUI.showMixedValue} (기대 True)";
            RestoredGuiState &= restored;
            AppliedAnyChanges |= data.ApplyModifiedProperties();
            if (Event.current.type == EventType.Repaint) Repaints++;
        }
        catch (Exception error)
        {
            Failure = error;
        }
        finally
        {
            EditorGUI.indentLevel = priorIndent;
            EditorGUI.showMixedValue = priorMixed;
        }
    }
}
