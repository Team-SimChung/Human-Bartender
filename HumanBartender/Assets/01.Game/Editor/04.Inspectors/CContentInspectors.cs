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
[CustomEditor(typeof(NewExpressionDataSO))] public class CExpressionInspector : CContentInspector { }
[CustomEditor(typeof(NewDayScriptDataSO))] public class CScriptInspector : CContentInspector { }
[CustomEditor(typeof(NewCharacterDataSO))] public class CCharacterInspector : CContentInspector { }
[CustomEditor(typeof(NewCutSceneDataSO))] public class CCutsceneInspector : CContentInspector { }
[CustomEditor(typeof(NewInteractPointDataSO))] public class CInteractPointInspector : CContentInspector { }
[CustomEditor(typeof(NewSpotDataSO))] public class CSpotInspector : CContentInspector { }
