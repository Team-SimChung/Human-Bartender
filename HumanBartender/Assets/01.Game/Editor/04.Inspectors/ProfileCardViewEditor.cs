using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ProfileCardView)), CanEditMultipleObjects]
public sealed class ProfileCardViewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("characterId"));
        var mode = serializedObject.FindProperty("portraitMode");
        EditorGUILayout.PropertyField(mode, new GUIContent("초상 표시 방식", mode.tooltip));
        if (mode.hasMultipleDifferentValues || mode.enumValueIndex == (int)ProfilePortraitView.Mode.SingleImage)
        {
            var sprite = serializedObject.FindProperty("singlePortrait");
            EditorGUILayout.PropertyField(sprite, new GUIContent("단일 초상 이미지", sprite.tooltip));
            if (!mode.hasMultipleDifferentValues && !sprite.hasMultipleDifferentValues && sprite.objectReferenceValue == null)
                EditorGUILayout.HelpBox("단일 초상 이미지가 비어 있습니다. 등록된 인물의 초상은 —로 표시됩니다.", MessageType.Warning);
        }
        if (mode.hasMultipleDifferentValues || mode.enumValueIndex == (int)ProfilePortraitView.Mode.Parts)
        {
            var parts = serializedObject.FindProperty("portraitLayers");
            EditorGUILayout.PropertyField(parts, new GUIContent("파츠 초상 연결", parts.tooltip), true);
            if (!mode.hasMultipleDifferentValues && !parts.hasMultipleDifferentValues && parts.arraySize == 0)
                EditorGUILayout.HelpBox("파츠 초상 연결이 비어 있습니다. 등록된 인물의 초상은 —로 표시됩니다.", MessageType.Warning);
        }
        EditorGUILayout.HelpBox("카드와 상세창에 함께 적용됩니다. 표시 방식을 바꿔도 다른 방식의 리소스 연결은 유지됩니다.", MessageType.Info);
        DrawPropertiesExcluding(serializedObject, "m_Script", "characterId", "portraitMode", "singlePortrait", "portraitLayers");
        serializedObject.ApplyModifiedProperties();
    }
}
