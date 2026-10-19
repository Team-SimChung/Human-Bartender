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
        else Draw("interactionRadius");
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
