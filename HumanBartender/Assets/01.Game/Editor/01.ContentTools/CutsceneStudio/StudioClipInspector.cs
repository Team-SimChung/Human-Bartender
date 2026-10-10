using UnityEditor;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    [CustomEditor(typeof(StudioClip))]
    public sealed class StudioClipInspector : UnityEditor.Editor
    {
        [SerializeField]
        private bool settingsOpen = true, animationOpen = true;
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
                    StudioGUI.Field(StudioSerializedFields.Find(serializedObject, field.Path), field.Label);
            }

            foreach (string message in StudioClipSchema.Help(clip))
                EditorGUILayout.HelpBox(message, MessageType.Info);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
