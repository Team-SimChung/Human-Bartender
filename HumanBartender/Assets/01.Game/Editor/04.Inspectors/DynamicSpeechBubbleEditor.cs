using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DynamicSpeechBubble)), CanEditMultipleObjects]
public sealed class DynamicSpeechBubbleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        foreach (Object item in targets)
        {
            var bubble = (DynamicSpeechBubble)item;
            var visual = bubble.GetComponent<SpeechBubbleVisual>();
            if (visual != null) visual.Refresh();
            if (bubble.visualMode != SpeechBubbleVisualMode.Parts) continue;
            if (visual != null && visual.enabled && visual.IsConfigured) continue;
            EditorGUILayout.HelpBox("Parts 외형 구성이 필요합니다. 구성 전에는 기존 이미지를 표시합니다.", MessageType.Warning);
            if (targets.Length == 1 && GUILayout.Button("프리팹에 맞는 기본 파츠 구성"))
                SpeechBubblePartsSetup.Configure(bubble);
            break;
        }
    }
}

[CustomEditor(typeof(SpeechBubblePartGraphic)), CanEditMultipleObjects]
public sealed class SpeechBubblePartGraphicEditor : UnityEditor.UI.GraphicEditor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sprite"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sourceInsetPixels"));
        serializedObject.ApplyModifiedProperties();
        base.OnInspectorGUI();
    }
}
