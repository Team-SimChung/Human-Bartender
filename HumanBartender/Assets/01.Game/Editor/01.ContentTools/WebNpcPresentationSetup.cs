using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>현재 배치된 NPC의 웹 연출을 공통 설정으로 연결한다. CSV·저장 데이터는 수정하지 않는다.</summary>
public static class WebNpcPresentationSetup
{
    [MenuItem("Tools/Story/Apply Web NPC Presentation")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("NPC 연출 설정은 편집 모드에서 실행하세요.");
        var previous = SceneManager.GetActiveScene();
        var opened = new List<Scene>();
        Scene Open(string name)
        {
            string path = "Assets/01.Game/01.Scenes/" + name + ".unity";
            var scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isLoaded)
            {
                if (scene.isDirty) throw new InvalidOperationException(name + "의 편집 내용을 저장한 다음 실행하세요.");
                return scene;
            }
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            opened.Add(scene);
            return scene;
        }
        try
        {
            var home = Open("Home");
            var outside = Open("OutSide");
            WebNpcAnimationSetup.Apply(home, outside);
            WebNpcMotionSetup.Apply(home, outside);
            var reveal = outside.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true))
                .Single(value => value.StorySceneId == "neighborhood-reveal");
            var travel = reveal.GetComponent<WorldDialogueTravelSettings>();
            if (travel == null) travel = Undo.AddComponent<WorldDialogueTravelSettings>(reveal.gameObject);
            travel.Configure(new Vector2(2.02f, -.7f), true, .35f, .05f, .35f, .45f, "neighborhood-homeCat");
            EditorUtility.SetDirty(travel);
            foreach (var scene in new[] { home, outside })
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException(scene.name + " 연출 설정을 저장하지 못했습니다.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("WEB_NPC_PRESENTATION_SETUP_OK: Home·OutSide NPC 웹 연출 설정 완료");
        }
        finally
        {
            foreach (var scene in opened)
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }
}
