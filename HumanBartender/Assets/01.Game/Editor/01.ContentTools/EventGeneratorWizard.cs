using UnityEditor;
using System.IO;
using UnityEngine;

public class EventGeneratorWizard : ScriptableWizard
{
    [Tooltip("이벤트로 전달할 타입의 이름을 정확히 입력하세요. (예: int, string, MyCustomData)")]
    public string typeName = "int";

    [Tooltip("생성될 파일의 접두사입니다. (예: Int, String, MyCustomData)")]
    public string fileNamePrefix = "Int";

    [Tooltip("CreateAssetMenu에 표시될 경로입니다. (예: Events/Int Event)")]
    public string menuPath = "Events/Int Event";

    [MenuItem("Assets/Create/Event Channel Generator")]
    private static void CreateWizard()
    {
        ScriptableWizard.DisplayWizard<EventGeneratorWizard>("Create Event Channel", "Create");
    }

    private void OnWizardCreate()
    {
        string path = EditorUtility.SaveFolderPanel("Generated Event Scripts 저장 위치 선택", "Assets", "");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        // 상대 경로로 변환
        path = "Assets" + path.Substring(Application.dataPath.Length);;

        // 1. GameEvent<T> 상속 파일 생성
        string eventFileContent = GenerateEventFile();
        File.WriteAllText($"{path}/{fileNamePrefix}Event.cs", eventFileContent);

        // 2. GameEventListener<T> 상속 파일 생성
        string listenerFileContent = GenerateListenerFile();
        File.WriteAllText($"{path}/{fileNamePrefix}EventListener.cs", listenerFileContent);

        AssetDatabase.Refresh();
        Debug.Log($"이벤트 스크립트 2개 생성 완료: {path}");
    }

    private string GenerateEventFile()
    {
        return
$@"using UnityEngine;

[CreateAssetMenu(fileName = ""{fileNamePrefix}Event"", menuName = ""Game Events/{menuPath}"")]
public class {fileNamePrefix}Event : GameEvent<{typeName}>
{{
}}
";
    }

    private string GenerateListenerFile()
    {
        return
$@"public class {fileNamePrefix}EventListener : GameEventListener<{typeName}>
{{
}}
";
    }
}