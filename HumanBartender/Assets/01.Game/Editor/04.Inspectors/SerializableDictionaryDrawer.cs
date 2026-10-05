using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomPropertyDrawer(typeof(SerializableDictionary<,>))]
public class SerializableDictionaryDrawer : PropertyDrawer
{
    private const float ButtonWidth = 25f; // 버튼 넓이

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            Rect contentPosition = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);

            SerializedProperty keysProp = property.FindPropertyRelative("keys");
            SerializedProperty valuesProp = property.FindPropertyRelative("values");

            var keyPaths = new HashSet<string>();
            string duplicateKeyWarning = null;

            for (int i = 0; i < keysProp.arraySize; i++)
            {
                SerializedProperty keyProp = keysProp.GetArrayElementAtIndex(i);
                SerializedProperty valueProp = valuesProp.GetArrayElementAtIndex(i);

                contentPosition.height = EditorGUI.GetPropertyHeight(keyProp) > EditorGUI.GetPropertyHeight(valueProp) ? EditorGUI.GetPropertyHeight(keyProp) : EditorGUI.GetPropertyHeight(valueProp);

                float keyWidth = contentPosition.width * 0.45f - ButtonWidth / 2;
                float valueWidth = contentPosition.width * 0.55f - ButtonWidth / 2;

                Rect keyRect = new Rect(contentPosition.x, contentPosition.y, keyWidth, contentPosition.height);
                Rect valueRect = new Rect(contentPosition.x + keyWidth + 5, contentPosition.y, valueWidth, contentPosition.height);
                Rect removeButtonRect = new Rect(contentPosition.x + contentPosition.width - ButtonWidth, contentPosition.y, ButtonWidth, contentPosition.height);

                EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none);
                EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none);

                if (GUI.Button(removeButtonRect, "-"))
                {
                    keysProp.DeleteArrayElementAtIndex(i);
                    valuesProp.DeleteArrayElementAtIndex(i);
                    break;
                }

                if (keyProp.propertyType == SerializedPropertyType.String)
                {
                    if (!keyPaths.Add(keyProp.stringValue))
                    {
                        duplicateKeyWarning = $"중복된 키가 존재합니다: '{keyProp.stringValue}'";
                    }
                }

                contentPosition.y += contentPosition.height + EditorGUIUtility.standardVerticalSpacing;
            }

            Rect addButtonRect = new Rect(position.x + (position.width - 60) / 2, contentPosition.y, 60, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(addButtonRect, "Add New"))
            {
                int newIndex = keysProp.arraySize;
                keysProp.arraySize++;
                valuesProp.arraySize++;

                SerializedProperty newKey = keysProp.GetArrayElementAtIndex(newIndex);
                SerializedProperty newValue = valuesProp.GetArrayElementAtIndex(newIndex);

                // 새로 추가된 항목의 값을 타입에 맞는 기본값으로 초기화합니다.
                ResetToDefault(newKey);
                ResetToDefault(newValue);
            }
            contentPosition.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            if (duplicateKeyWarning != null)
            {
                Rect warningRect = new Rect(position.x, contentPosition.y, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.HelpBox(warningRect, duplicateKeyWarning, MessageType.Warning);
            }
        }

        EditorGUI.EndProperty();
    }

    // 이 함수가 새로 추가되었습니다.
    private void ResetToDefault(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                property.intValue = 0;
                break;
            case SerializedPropertyType.Float:
                property.floatValue = 0f;
                break;
            case SerializedPropertyType.String:
                property.stringValue = "";
                break;
            case SerializedPropertyType.Boolean:
                property.boolValue = false;
                break;
            case SerializedPropertyType.ObjectReference:
                property.objectReferenceValue = null;
                break;
            case SerializedPropertyType.Enum:
                property.enumValueIndex = 0;
                break;
            // 다른 타입들에 대해서도 필요하다면 추가할 수 있습니다.
            default:
                // 기본적으로 메모리를 0으로 채우는 시도를 할 수 있지만, 복잡한 타입에서는 위험할 수 있습니다.
                break;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        float totalHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        SerializedProperty keysProp = property.FindPropertyRelative("keys");
        for (int i = 0; i < keysProp.arraySize; i++)
        {
            SerializedProperty keyProp = keysProp.GetArrayElementAtIndex(i);
            SerializedProperty valueProp = keysProp.GetArrayElementAtIndex(i);
            float keyHeight = EditorGUI.GetPropertyHeight(keyProp);
            float valueHeight = EditorGUI.GetPropertyHeight(valueProp);
            totalHeight += (keyHeight > valueHeight ? keyHeight : valueHeight) + EditorGUIUtility.standardVerticalSpacing;
        }

        totalHeight += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        var keyPaths = new HashSet<string>();
        bool hasDuplicate = false;
        for (int i = 0; i < keysProp.arraySize; i++)
        {
            SerializedProperty keyProp = keysProp.GetArrayElementAtIndex(i);
            if (keyProp.propertyType == SerializedPropertyType.String && !keyPaths.Add(keyProp.stringValue))
            {
                hasDuplicate = true;
                break;
            }
        }
        if (hasDuplicate)
        {
            totalHeight += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        return totalHeight;
    }
}