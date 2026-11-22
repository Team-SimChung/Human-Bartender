// 메뉴: Tools > Story > Sync Tutorial Copy

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Play 씬 StoryTutorialController의 문구 표(copy)를 코드의 단계 목록(DefaultCopy)에 맞춘다.
///
/// 인스펙터에서 고친 문구는 그대로 두고, 진행 규칙에서 빠진 단계는 지우고, 새로 생긴 단계는 기본 문구로
/// 채운다. 순서는 DefaultCopy를 따른다. 다시 실행해도 같은 결과가 나온다.
/// 배치 모드: -executeMethod StoryTutorialCopySync.Run
/// </summary>
public static class StoryTutorialCopySync
{
    const string ScenePath = "Assets/01.Game/01.Scenes/Play.unity";

    [MenuItem("Tools/Story/Sync Tutorial Copy")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        StoryTutorialController controller = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            controller = root.GetComponentInChildren<StoryTutorialController>(true);
            if (controller != null) break;
        }
        if (controller == null) throw new System.InvalidOperationException("[TutorialCopy] Play 씬에 StoryTutorialController가 없습니다.");

        var serialized = new SerializedObject(controller);
        var table = serialized.FindProperty("copy");
        var existing = new Dictionary<string, (string title, string text)>();
        for (int i = 0; i < table.arraySize; i++)
        {
            var entry = table.GetArrayElementAtIndex(i);
            string key = entry.FindPropertyRelative("Key").stringValue;
            if (!string.IsNullOrEmpty(key) && !existing.ContainsKey(key))
                existing[key] = (entry.FindPropertyRelative("Title").stringValue, entry.FindPropertyRelative("Text").stringValue);
        }

        var defaults = StoryTutorialController.DefaultCopy();
        var removed = new List<string>(existing.Keys);
        var added = new List<string>();
        table.arraySize = defaults.Length;
        for (int i = 0; i < defaults.Length; i++)
        {
            var source = defaults[i];
            removed.Remove(source.Key);
            bool keep = existing.TryGetValue(source.Key, out var current) && !string.IsNullOrWhiteSpace(current.text);
            if (!keep) added.Add(source.Key);
            var entry = table.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("Key").stringValue = source.Key;
            entry.FindPropertyRelative("Title").stringValue = keep ? current.title : source.Title;
            entry.FindPropertyRelative("Text").stringValue = keep ? current.text : source.Text;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[TutorialCopy] 문구 표를 맞췄습니다. 추가: {string.Join(", ", added)} / 제거: {string.Join(", ", removed)}");
    }
}
