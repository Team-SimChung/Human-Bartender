using UnityEditor;
using UnityEngine;

/// <summary>CSV owns content. Inspectors display the loaded cache without creating a second editing source.</summary>
public class CContentInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("콘텐츠 원본은 Assets/StreamingAssets/csv입니다. 이 값은 로딩 캐시이며 여기서 편집하지 않습니다. 위치는 씬의 SpotPoint에서 편집합니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(true)) DrawDefaultInspector();
        if (target is NewStreetDataSO street)
            EditorGUILayout.LabelField("로드된 거리 장면", (street.newStreetData.Scenes?.Length ?? 0).ToString());
        if (target is NewExpressionDataSO expressions)
            EditorGUILayout.LabelField("로드된 캐릭터 표정 묶음", (expressions.expressionData?.Count ?? 0).ToString());
        if (GUILayout.Button("C 콘텐츠 검증")) RefactoringCContentValidator.ValidateMenu();
    }
}
[CustomEditor(typeof(NewStreetDataSO))] public class CStreetInspector : CContentInspector { }
[CustomEditor(typeof(NewExpressionDataSO))]
public class CExpressionInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("표정·리소스 연결의 원본은 CSV입니다. 아래 초상 시각 배치만 이 SO에 저장합니다. Y는 원본 픽셀 단위이며 양수이면 위로 이동합니다. 파츠 캐릭터에는 적용하지 않습니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("portraitPlacements"), true);
        serializedObject.ApplyModifiedProperties();
        var expressions = (NewExpressionDataSO)target;
        EditorGUILayout.LabelField("로드된 캐릭터 표정 묶음", (expressions.expressionData?.Count ?? 0).ToString());
        if (GUILayout.Button("C 콘텐츠 검증")) RefactoringCContentValidator.ValidateMenu();
    }
}
[CustomEditor(typeof(NewDayScriptDataSO))] public class CScriptInspector : CContentInspector { }
[CustomEditor(typeof(NewCharacterDataSO))] public class CCharacterInspector : CContentInspector { }
[CustomEditor(typeof(NewCutSceneDataSO))] public class CCutsceneInspector : CContentInspector { }
[CustomEditor(typeof(NewInteractPointDataSO))] public class CInteractPointInspector : CContentInspector { }
[CustomEditor(typeof(NewSpotDataSO))] public class CSpotInspector : CContentInspector { }
