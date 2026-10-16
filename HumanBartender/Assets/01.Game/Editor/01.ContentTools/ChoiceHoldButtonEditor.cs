using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

[CustomEditor(typeof(ChoiceHoldButton))]
[CanEditMultipleObjects]
public sealed class ChoiceHoldButtonEditor : ButtonEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("프리팹에는 실제 크기와 완전히 채워진 진행 바가 기본 모형으로 저장됩니다. " +
                "게임을 실행하면 진행 바는 자동으로 숨겨지고, 누를 때만 표시됩니다.", MessageType.Info);
        serializedObject.Update();
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("holdDuration"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("resetDuration"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("holdProgressRoot"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("holdProgressFill"));
        serializedObject.ApplyModifiedProperties();
    }
}
