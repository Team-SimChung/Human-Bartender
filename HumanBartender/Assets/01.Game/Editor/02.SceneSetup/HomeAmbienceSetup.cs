using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class HomeAmbienceSetup
{
    public const string HomePath = "Assets/01.Game/01.Scenes/Home.unity";
    public const string PrefabPath = "Assets/01.Game/03.Content/08.Prefabs/04.Shared/03.Audio/DistanceAmbientAudio.prefab";
    const string RainPath = "Assets/01.Game/03.Content/04.Audio/02.Ambience/Weather/rain_loop.ogg";

    [MenuItem("Tools/Home/Connect Window Rain Audio")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("[HomeAmbience] Play 모드를 종료한 뒤 실행하세요.");
        var rain = AssetDatabase.LoadAssetAtPath<AudioClip>(RainPath);
        var output = Resources.Load<AudioOutputSettings>("AudioOutputSettings");
        if (rain == null || output == null)
        {
            Debug.LogWarning("[HomeAmbience] rain_loop 또는 AudioOutputSettings가 없어 연결하지 않았습니다.");
            return;
        }
        GameObject prefab = EnsurePrefab(output);
        var scene = SceneManager.GetSceneByPath(HomePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (!openedHere && scene.isDirty)
        {
            Debug.LogWarning("[HomeAmbience] Home에 저장하지 않은 변경이 있어 자동 저장하지 않았습니다. 먼저 씬을 저장하세요.");
            return;
        }
        if (openedHere) scene = EditorSceneManager.OpenScene(HomePath, OpenSceneMode.Additive);
        try
        {
            var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var player = all.Select(t => t.GetComponent<Player>()).FirstOrDefault(p => p != null);
            var terrace = all.Select(t => t.GetComponent<HomeTerracePresentation>()).FirstOrDefault(p => p != null);
            var room = all.FirstOrDefault(t => t.name == "RoomBackground")?.GetComponent<SpriteRenderer>();
            var gameplay = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "04.Gameplay");
            if (player == null || terrace == null || room == null || gameplay == null)
            {
                Debug.LogWarning("[HomeAmbience] Home의 플레이어·테라스 담당자·실내 배경·04.Gameplay 연결을 확인하세요.");
                return;
            }
            bool changed = false;
            var group = all.FirstOrDefault(t => t.name == "IndoorAmbience" && t.parent == gameplay.transform);
            if (group == null)
            {
                var go = new GameObject("IndoorAmbience");
                Undo.RegisterCreatedObjectUndo(go, "Create home ambience group");
                go.transform.SetParent(gameplay.transform, false);
                group = go.transform;
                changed = true;
            }
            var ambient = group.GetComponentsInChildren<DistanceAmbientAudio>(true).FirstOrDefault(a => a.name == "WindowRainAudio");
            if (ambient == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                Undo.RegisterCreatedObjectUndo(instance, "Place window rain audio");
                instance.name = "WindowRainAudio";
                Bounds bounds = room.sprite.bounds;
                instance.transform.position = room.transform.TransformPoint(new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, .6f), Mathf.Lerp(bounds.min.y, bounds.max.y, .43f), 0f));
                ambient = instance.GetComponent<DistanceAmbientAudio>();
                var data = new SerializedObject(ambient);
                data.FindProperty("listener").objectReferenceValue = player.transform;
                data.FindProperty("clip").objectReferenceValue = rain;
                var days = data.FindProperty("days");
                days.arraySize = 4;
                for (int day = 0; day < days.arraySize; day++)
                {
                    var entry = days.GetArrayElementAtIndex(day);
                    entry.FindPropertyRelative("day").intValue = day;
                    entry.FindPropertyRelative("play").boolValue = day < 3;
                }
                data.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(ambient);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                changed = true;
            }
            var terraceData = new SerializedObject(terrace);
            var sounds = terraceData.FindProperty("indoorAmbience");
            bool connected = false;
            for (int i = 0; i < sounds.arraySize; i++)
                connected |= sounds.GetArrayElementAtIndex(i).objectReferenceValue == ambient;
            if (!connected)
            {
                sounds.arraySize++;
                sounds.GetArrayElementAtIndex(sounds.arraySize - 1).objectReferenceValue = ambient;
                terraceData.ApplyModifiedProperties();
                EditorUtility.SetDirty(terrace);
                changed = true;
            }
            if (!changed) return;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[HomeAmbience] 창문 빗소리·거리 감쇠·날짜 배열·실내 전환을 Home에 저장했습니다.");
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    static GameObject EnsurePrefab(AudioOutputSettings output)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null) return prefab;
        string folder = Path.GetDirectoryName(PrefabPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
        var root = new GameObject("DistanceAmbientAudio", typeof(AudioSource), typeof(DistanceAmbientAudio));
        Undo.RegisterCreatedObjectUndo(root, "Create reusable distance ambience");
        try
        {
            var source = root.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            output.Route(source, AudioBus.SoundEffects);
            var data = new SerializedObject(root.GetComponent<DistanceAmbientAudio>());
            data.FindProperty("source").objectReferenceValue = source;
            data.ApplyModifiedProperties();
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
