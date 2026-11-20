using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(NpcSequence))]
public sealed class NpcSequenceDrawer : PropertyDrawer
{
    private static IEnumerable<string> Fields(SerializedProperty property)
    {
        var trigger = property.FindPropertyRelative("trigger");
        bool showCommand = !trigger.hasMultipleDifferentValues &&
            (NpcSequenceTrigger)trigger.intValue == NpcSequenceTrigger.WorldAction;
        yield return "id";
        yield return "trigger";
        if (showCommand)
            yield return "worldAction";
        yield return "requiredCondition";
        yield return "actions";
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (property.isExpanded)
            foreach (var field in Fields(property))
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) + EditorGUIUtility.standardVerticalSpacing;
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        int previousIndent = EditorGUI.indentLevel;
        bool previousMixedValue = EditorGUI.showMixedValue;
        EditorGUI.BeginProperty(position, label, property);
        try
        {
            var id = property.FindPropertyRelative("id");
            string title = id.hasMultipleDifferentValues || string.IsNullOrWhiteSpace(id.stringValue) ? label.text : id.stringValue;
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, title, true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (var field in Fields(property))
                {
                    var child = property.FindPropertyRelative(field);
                    row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                    row.height = EditorGUI.GetPropertyHeight(child, true);
                    string fieldTitle = field switch
                    {
                        "id" => "연출 ID",
                        "trigger" => "실행 방식",
                        "worldAction" => "등록된 CSV 명령",
                        "requiredCondition" => "실행 조건",
                        _ => "동작 순서"
                    };
                    if (field == "worldAction")
                        DrawCsvCommand(row, child, new GUIContent(fieldTitle, "코드에 미리 등록된 명령 목록입니다. CSV가 선택한 명령을 요청하면 아래 동작 순서를 실행합니다. 연출 ID는 비교하지 않습니다. 이 메뉴는 CSV에서 가져온 목록이 아니며, 새 명령을 등록하려면 코드 연결이 필요합니다."));
                    else EditorGUI.PropertyField(row, child, new GUIContent(fieldTitle, field == "trigger"
                        ? "CSV · 연출 ID로 호출: CSV에 type=npc-sequence, arg=위 연출 ID를 작성합니다. 새 연출에 권장하며, 기존 공통 동작을 조합할 때 코드 수정이 필요 없습니다.\nCSV · 등록된 명령으로 호출: 아래에서 코드에 미리 등록된 명령을 선택합니다. 기존 대본 연결을 유지할 때 사용하며, 연출 ID는 비교하지 않습니다.\n두 CSV 방식 모두 대본에서 아래 동작 순서를 호출하며, 이동 거리·시간은 Inspector 설정을 사용합니다. 대화 자동/직접 시작은 World Interaction Target에서 정합니다."
                        : child.tooltip), true);
                }
            }
        }
        finally
        {
            try { EditorGUI.EndProperty(); }
            finally
            {
                EditorGUI.indentLevel = previousIndent;
                EditorGUI.showMixedValue = previousMixedValue;
            }
        }
    }

    private static void DrawCsvCommand(Rect row, SerializedProperty property, GUIContent label)
    {
        bool previousMixedValue = EditorGUI.showMixedValue;
        try
        {
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            using var change = new EditorGUI.ChangeCheckScope();
            int command = EditorGUI.IntPopup(row, label, property.intValue, CsvCommands, CsvCommandValues);
            // 여러 NPC를 보기만 했을 때 첫 NPC의 명령으로 나머지 값을 덮어쓰지 않는다.
            if (change.changed) property.intValue = command;
        }
        finally
        {
            EditorGUI.showMixedValue = previousMixedValue;
        }
    }

    private static readonly Dictionary<OutsideStoryWorldAction, string> CsvCommandNames = new()
    {
        { OutsideStoryWorldAction.RepairElevator, "수리공 · 엘리베이터 수리" },
        { OutsideStoryWorldAction.AdoptCat, "부비 · 입양" },
        { OutsideStoryWorldAction.RevealCat, "부비 · 발견" },
        { OutsideStoryWorldAction.PetCat, "부비 · 쓰다듬기" },
        { OutsideStoryWorldAction.FeedCat, "부비 · 먹이" },
        { OutsideStoryWorldAction.NeighborEnter, "이웃 · 등장" },
        { OutsideStoryWorldAction.NeighborExit, "이웃 · 퇴장" },
        { OutsideStoryWorldAction.ChrisEnter, "크리스 · 등장" },
        { OutsideStoryWorldAction.CatMeow, "부비 · 울음" },
        { OutsideStoryWorldAction.SamhoDepart, "출근 삼호 · 퇴장" },
        { OutsideStoryWorldAction.NotionApproach, "계약 삼호 · 루나에게 접근" },
        { OutsideStoryWorldAction.NotionDealerExit, "계약 밀수업자 · 퇴장" },
        { OutsideStoryWorldAction.NotionSamhoExit, "계약 삼호 · 퇴장" },
        { OutsideStoryWorldAction.ThugPunch, "불량배 · 타격" },
        { OutsideStoryWorldAction.ThugDepart, "불량배 · 퇴장" },
        { OutsideStoryWorldAction.DoorCue, "공통 · 문 효과" }
    };
    private static readonly GUIContent[] CsvCommands;
    private static readonly int[] CsvCommandValues;

    static NpcSequenceDrawer()
    {
        var commands = (OutsideStoryWorldAction[])Enum.GetValues(typeof(OutsideStoryWorldAction));
        CsvCommands = new GUIContent[commands.Length];
        CsvCommandValues = new int[commands.Length];
        for (int i = 0; i < commands.Length; i++)
        {
            CsvCommands[i] = new GUIContent(CsvCommandNames.TryGetValue(commands[i], out string label) ? label : commands[i].ToString());
            CsvCommandValues[i] = (int)commands[i];
        }
    }
}

[CustomPropertyDrawer(typeof(NpcActionStep))]
public sealed class NpcActionStepDrawer : PropertyDrawer
{
    private static IEnumerable<string> Fields(SerializedProperty property)
    {
        var kind = property.FindPropertyRelative("kind");
        return kind.hasMultipleDifferentValues ? new[] { "kind" } : Fields((NpcActionKind)kind.intValue);
    }

    private static IEnumerable<string> Fields(NpcActionKind kind)
    {
        yield return "kind";
        if (kind != NpcActionKind.Wait && kind != NpcActionKind.ScreenFadeOut && kind != NpcActionKind.ScreenFadeIn &&
            kind != NpcActionKind.ObjectivePopup && kind != NpcActionKind.Sound && kind != NpcActionKind.MovePlayer &&
            kind != NpcActionKind.SetPlayerPosition && kind != NpcActionKind.FacePlayer && kind != NpcActionKind.CameraRestore)
            yield return "performer";
        switch (kind)
        {
            case NpcActionKind.Approach: yield return "target"; yield return "distance"; break;
            case NpcActionKind.Face: yield return "target"; break;
            case NpcActionKind.MoveBy: yield return "offset"; break;
            case NpcActionKind.Animation: yield return "stateName"; yield return "returnState"; yield return "duration"; break;
            case NpcActionKind.Depart: yield return "offset"; yield return "offsetMode"; yield return "target"; yield return "duration"; break;
            case NpcActionKind.MoveTo:
            case NpcActionKind.SetPosition:
            case NpcActionKind.ArcMove:
            case NpcActionKind.MoveToAndFade:
            case NpcActionKind.MovePlayer:
            case NpcActionKind.SetPlayerPosition:
            case NpcActionKind.CameraFocus:
            case NpcActionKind.CameraPan:
            case NpcActionKind.CameraSet:
                yield return "positionOrigin";
                yield return "target";
                yield return "offset";
                yield return "offsetMode";
                yield return "horizontalOnly";
                if (kind is NpcActionKind.MoveTo or NpcActionKind.MovePlayer or NpcActionKind.ArcMove)
                {
                    yield return "duration"; yield return "movementEase";
                    if (kind == NpcActionKind.MoveTo)
                    {
                        yield return "unitsPerSecond"; yield return "keepFacing"; yield return "cameraWithMovement";
                        yield return "cameraMidpoint"; yield return "cameraOffset"; yield return "cameraWidth";
                    }
                    if (kind == NpcActionKind.ArcMove) yield return "arcHeight";
                    if (kind == NpcActionKind.MovePlayer)
                    {
                        yield return "keepFacing";
                        yield return "clampPlayerLeft"; yield return "cameraWithMovement"; yield return "cameraMidpoint";
                        yield return "cameraOffset"; yield return "cameraWidth";
                    }
                }
                if (kind == NpcActionKind.MoveToAndFade)
                {
                    yield return "unitsPerSecond"; yield return "walkDuration"; yield return "duration"; yield return "fadeEase";
                }
                if (kind is NpcActionKind.CameraFocus or NpcActionKind.CameraPan or NpcActionKind.CameraSet)
                {
                    yield return "cameraMidpoint"; yield return "cameraWidth";
                    if (kind != NpcActionKind.CameraSet) yield return "duration";
                    if (kind == NpcActionKind.CameraPan) { yield return "movementEase"; yield return "fadeInTogether"; }
                }
                break;
            case NpcActionKind.FacePlayer: yield return "target"; yield return "offset"; break;
            case NpcActionKind.DepartWhileMoving:
                yield return "offset";
                yield return "offsetMode";
                yield return "target";
                yield return "walkDuration";
                yield return "duration";
                yield return "fadeEase";
                break;
            case NpcActionKind.Wait:
            case NpcActionKind.FadeOut:
            case NpcActionKind.ScreenFadeOut:
            case NpcActionKind.ScreenFadeIn:
            case NpcActionKind.CameraRestore: yield return "duration"; break;
            case NpcActionKind.ObjectivePopup:
            case NpcActionKind.Sound: yield return "text"; break;
        }
    }

    private static string Title(SerializedProperty kind)
    {
        int index = kind.enumValueIndex;
        return index >= 0 && index < kind.enumDisplayNames.Length ? kind.enumDisplayNames[index] : kind.intValue.ToString();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (property.isExpanded)
            foreach (var field in Fields(property))
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) + EditorGUIUtility.standardVerticalSpacing;
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        int previousIndent = EditorGUI.indentLevel;
        bool previousMixedValue = EditorGUI.showMixedValue;
        EditorGUI.BeginProperty(position, label, property);
        try
        {
            var kindProperty = property.FindPropertyRelative("kind");
            var kind = (NpcActionKind)kindProperty.intValue;
            var fields = Fields(property);
            string title = kindProperty.hasMultipleDifferentValues ? label.text : label.text + " · " + Title(kindProperty);
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, title, true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (var field in fields)
                {
                    var child = property.FindPropertyRelative(field);
                    row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                    row.height = EditorGUI.GetPropertyHeight(child, true);
                    string fieldTitle = field switch
                    {
                        "kind" => "동작",
                        "performer" => "실행 NPC (비우면 대표 Actor)",
                        "positionOrigin" => "위치 기준",
                        "horizontalOnly" => "X만 이동 (Y/Z 유지)",
                        "movementEase" => "이동 변화",
                        "fadeEase" => "페이드 변화",
                        "offsetMode" => "X 이동 방향 기준",
                        "unitsPerSecond" => "이동 속도 (0이면 시간 사용)",
                        "arcHeight" => "포물선 높이",
                        "keepFacing" => "이동 중 바라보는 방향 유지",
                        "clampPlayerLeft" => "이미 왼쪽이면 위치 유지",
                        "cameraWithMovement" => "이동과 함께 카메라 맞추기",
                        "cameraMidpoint" => "대상과 플레이어 중간 구도",
                        "cameraOffset" => "카메라 중심 보정 X/Y",
                        "cameraWidth" => "카메라 가로 범위",
                        "fadeInTogether" => "동시에 화면 복귀 (0이면 사용 안 함)",
                        "walkDuration" => "페이드 전 이동 시간 (초)",
                        "target" => "대상 (비우면 루나)",
                        "distance" => "멈출 X 간격",
                        "offset" => kind == NpcActionKind.DepartWhileMoving ? "전체 월드 X/Y 이동량" : "월드 X/Y 이동량",
                        "stateName" => "재생 상태",
                        "returnState" => "복귀 상태",
                        "text" => kind == NpcActionKind.Sound ? "효과음 이름" : "알림 문구",
                        "duration" when kind is NpcActionKind.MoveTo or NpcActionKind.ArcMove or NpcActionKind.MovePlayer => "이동 시간 (초)",
                        "duration" when kind == NpcActionKind.Depart => "퇴장 페이드 시간",
                        "duration" when kind is NpcActionKind.DepartWhileMoving or NpcActionKind.MoveToAndFade => "이동 중 페이드 시간 (초)",
                        _ => "시간 (초)"
                    };
                    EditorGUI.PropertyField(row, child, new GUIContent(fieldTitle, child.tooltip), true);
                }
            }
        }
        finally
        {
            try { EditorGUI.EndProperty(); }
            finally
            {
                EditorGUI.indentLevel = previousIndent;
                EditorGUI.showMixedValue = previousMixedValue;
            }
        }
    }
}
