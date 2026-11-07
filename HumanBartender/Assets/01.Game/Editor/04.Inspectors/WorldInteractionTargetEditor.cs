using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorldInteractionTarget))]
public sealed class WorldInteractionTargetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        Draw("content");
        if (serializedObject.FindProperty("content").objectReferenceValue == null) Draw("storySceneId");
        Draw("startMode");
        Draw("interactionRadius");
        if (serializedObject.FindProperty("content").objectReferenceValue != null) Draw("contentStartOverrides");
        EditorGUILayout.HelpBox("시작 방식과 범위는 여기서 설정합니다. 날짜·출퇴근·등장 조건·완료 기록은 기존 CSV를 따릅니다. Scene 뷰의 청록색 원이 시작 범위입니다.", MessageType.Info);
        if (((WorldInteractionTarget)target).Participant != null && HasAmbientStart())
            EditorGUILayout.HelpBox("NPC 연출이 연결된 대상은 직접 상호작용 또는 범위 진입 시 자동 시작을 사용하세요. 주변 대화는 대사 전용이며 NPC 동작 목록을 실행하지 않습니다.", MessageType.Error);
        Draw("interactionLabel");
        Draw("output");
        Draw("highlight");
        var output = (WorldInteractionOutput)serializedObject.FindProperty("output").enumValueIndex;
        Draw("speechAnchor");
        Draw("bubbleOffset");
        if (output == WorldInteractionOutput.Image) Draw("inspectionImage");
        if (output == WorldInteractionOutput.Action) DrawAction();
        if (output == WorldInteractionOutput.Cutscene) Draw("cutsceneSequence");
        Draw("timelines");
        if (serializedObject.FindProperty("optionalTimelineIds").arraySize > 0)
        {
            Draw("optionalTimelineIds");
            EditorGUILayout.HelpBox("미제작 컷씬 ID는 경고 후 건너뜁니다. 제작한 Timeline을 위 목록에 연결하면 자동으로 재생됩니다.", MessageType.Warning);
        }
        EditorGUILayout.HelpBox("CSV timeline 단계의 arg를 Timelines의 ID와 연결합니다. 날짜·재시청 조건은 CSV에서 관리합니다.", MessageType.Info);
        serializedObject.ApplyModifiedProperties();
    }

    private bool HasAmbientStart()
    {
        if ((WorldInteractionStartMode)serializedObject.FindProperty("startMode").enumValueIndex == WorldInteractionStartMode.Ambient) return true;
        if (serializedObject.FindProperty("content").objectReferenceValue == null) return false;
        var overrides = serializedObject.FindProperty("contentStartOverrides");
        for (int i = 0; i < overrides.arraySize; i++)
            if ((WorldInteractionStartMode)overrides.GetArrayElementAtIndex(i).FindPropertyRelative("startMode").enumValueIndex == WorldInteractionStartMode.Ambient)
                return true;
        return false;
    }

    private void DrawAction()
    {
        var property = serializedObject.FindProperty("action");
        EditorGUILayout.PropertyField(property);
        if (property.objectReferenceValue is IWorldInteractionAction) return;

        string current = property.objectReferenceValue == null ? "비어 있음" : property.objectReferenceValue.GetType().Name;
        EditorGUILayout.HelpBox("Action에는 IWorldInteractionAction을 구현한 실행 컴포넌트를 연결해야 함.\n" +
            "현재 연결: " + current + "\n문은 WorldEntranceAction 컴포넌트의 제목 부분을 끌어다 연결하거나 아래 버튼으로 연결할 수 있음.", MessageType.Error);
        foreach (var component in ((WorldInteractionTarget)target).GetComponents<MonoBehaviour>())
        {
            if (component is not IWorldInteractionAction) continue;
            if (GUILayout.Button(component.GetType().Name + " 연결")) property.objectReferenceValue = component;
        }
    }

    private void Draw(string name) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), true);
}
