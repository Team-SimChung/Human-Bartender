using UnityEditor;
using UnityEngine;
using System.Linq;

namespace HumanBartender.CutsceneStudio.Editor
{
    [CustomEditor(typeof(StudioClip))]
    public sealed class StudioClipInspector : UnityEditor.Editor
    {
        [SerializeField]
        private bool settingsOpen = true, animationOpen = true;
        private string timingNotice;
        private string textDraft;
        private bool textFocused;
        private const string TextControl = "StudioDialogueDraft";
        public override void OnInspectorGUI()
        {
            var clip = (StudioClip)target;
            settingsOpen = StudioGUI.Section(settingsOpen, StudioEditorAssets.Label(clip.Kind) + " 설정");
            if (!settingsOpen)
                return;
            serializedObject.Update();
            bool animationHeader = false;
            foreach (var field in StudioClipSchema.Fields(clip))
            {
                if (field.Animation && !animationHeader)
                {
                    animationOpen = StudioGUI.Section(animationOpen, "애니메이션");
                    animationHeader = true;
                }

                if (field.Animation && !animationOpen)
                    continue;
                using (new EditorGUI.DisabledScope(!field.Enabled))
                {
                    var property = StudioSerializedFields.Find(serializedObject, field.Path);
                    if (field.Path == "Text")
                    {
                        if (!textFocused) textDraft = property.stringValue;
                        EditorGUILayout.LabelField(field.Label);
                        GUI.SetNextControlName(TextControl);
                        textDraft = EditorGUILayout.TextArea(textDraft, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 3));
                        bool focused = GUI.GetNameOfFocusedControl() == TextControl;
                        if (textFocused && !focused) property.stringValue = textDraft;
                        textFocused = focused;
                    }
                    else if (field.Path == "CharactersPerSecond") property.floatValue = Mathf.Max(.01f, EditorGUILayout.DelayedFloatField(field.Label, property.floatValue));
                    else StudioGUI.Field(property, field.Label);
                }
            }

            foreach (string message in StudioClipSchema.Help(clip))
                EditorGUILayout.HelpBox(message, MessageType.Info);
            if (serializedObject.hasModifiedProperties && clip.Kind == StudioKind.Dialogue)
            {
                var owner = FindSequence(clip);
                if (owner != null)
                {
                    StudioDialogueEditing.Edit(owner, clip, () => serializedObject.ApplyModifiedPropertiesWithoutUndo(),
                        "대사 설정·시간 변경", out timingNotice);
                    serializedObject.Update();
                }
                else serializedObject.ApplyModifiedProperties();
            }
            else serializedObject.ApplyModifiedProperties();
            if (!string.IsNullOrEmpty(timingNotice)) EditorGUILayout.HelpBox(timingNotice, MessageType.Warning);
        }

        private void OnDisable()
        {
            if (target is not StudioClip clip || !textFocused || textDraft == null || clip.Text == textDraft) return;
            var owner = FindSequence(clip);
            if (owner != null)
                StudioDialogueEditing.Edit(owner, clip, () => clip.Text = textDraft, "대사 내용·시간 변경", out timingNotice);
            else { Undo.RecordObject(clip, "대사 내용 변경"); clip.Text = textDraft; EditorUtility.SetDirty(clip); }
            textFocused = false;
        }

        private static StudioSequence FindSequence(StudioClip clip)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.FindAssets("t:StudioSequence")
                .Select(guid => AssetDatabase.LoadAssetAtPath<StudioSequence>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(s => s != null && s.Timeline != null && AssetDatabase.GetAssetPath(s.Timeline) == path);
        }
    }
}
