using UnityEditor;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 프리팹 선택에 따른 편집 설정 전환을 하나의 직렬화 / Undo 작업으로 적용함.
    internal static class StudioDialoguePrefabEditing
    {
        internal static bool IsValid(GameObject prefab) => prefab == null ||
            PrefabUtility.IsPartOfPrefabAsset(prefab) && StudioDialoguePrefabPresentation.IsCompatible(prefab);

        internal static bool Assign(SerializedObject data, GameObject prefab)
        {
            if (data.targetObject is not StudioClip { Kind: StudioKind.Dialogue } || !IsValid(prefab)) return false;
            data.UpdateIfRequiredOrScript();
            var reference = StudioSerializedFields.Find(data, "DialoguePrefab");
            if (reference.objectReferenceValue == prefab) return false;
            var saved = StudioSerializedFields.Find(data, "HasDialogueSettingsBeforePrefab");
            var style = StudioSerializedFields.Find(data, "UseDialogueStyle");
            var layout = StudioSerializedFields.Find(data, "UseBubbleLayout");
            var font = StudioSerializedFields.Find(data, "Font");
            var previousStyle = StudioSerializedFields.Find(data, "DialogueStyleBeforePrefab");
            var previousLayout = StudioSerializedFields.Find(data, "BubbleLayoutBeforePrefab");
            var previousFont = StudioSerializedFields.Find(data, "DialogueFontBeforePrefab");
            if (prefab != null)
            {
                if (!saved.boolValue)
                {
                    previousStyle.boolValue = style.boolValue;
                    previousLayout.boolValue = layout.boolValue;
                    previousFont.objectReferenceValue = font.objectReferenceValue;
                    saved.boolValue = true;
                }
                style.boolValue = false;
                layout.boolValue = false;
                font.objectReferenceValue = null;
            }
            else if (saved.boolValue)
            {
                style.boolValue = previousStyle.boolValue;
                layout.boolValue = previousLayout.boolValue;
                font.objectReferenceValue = previousFont.objectReferenceValue;
                saved.boolValue = previousStyle.boolValue = previousLayout.boolValue = false;
                previousFont.objectReferenceValue = null;
            }
            reference.objectReferenceValue = prefab;
            return data.ApplyModifiedProperties();
        }
    }
}
