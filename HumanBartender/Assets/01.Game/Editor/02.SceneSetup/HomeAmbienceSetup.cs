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
    const string AmbiencePath = "Assets/01.Game/03.Content/04.Audio/02.Ambience/";

    [MenuItem("Tools/Home/Connect Home and Terrace Ambience")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("[HomeAmbience] Play 모드를 종료한 뒤 실행하세요.");
        var houseRain = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbiencePath + "Weather/rain_house.wav");
        var terraceRain = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbiencePath + "Weather/rain_loop.ogg");
        var city = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbiencePath + "Urban/street_loop.ogg");
        var output = Resources.Load<AudioOutputSettings>("AudioOutputSettings");
        if (houseRain == null || terraceRain == null || city == null || output == null)
        {
            Debug.LogWarning("[HomeAmbience] 실내 빗소리·실외 빗소리·도시음과 AudioOutputSettings를 확인하세요.");
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
            var indoorGroup = EnsureGroup("IndoorAmbience");
            var terraceGroup = EnsureGroup("TerraceAmbience");
            Connect(indoorGroup, "WindowRainAudio", houseRain, true, .4675f, .65f, player.transform);
            Connect(terraceGroup, "TerraceCityAudio", city, false, .3025f, .45f);
            Connect(terraceGroup, "TerraceRainAudio", terraceRain, true, .257125f, .7f);

            if (!changed) return;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[HomeAmbience] Home 창가 빗소리와 테라스 도시음·빗소리를 연결했습니다.");

            Transform EnsureGroup(string name)
            {
                var group = all.FirstOrDefault(t => t.name == name && t.parent == gameplay.transform);
                if (group != null) return group;
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create ambience group");
                go.transform.SetParent(gameplay.transform, false);
                changed = true;
                return go.transform;
            }

            void Connect(Transform group, string name, AudioClip clip, bool rainOnly, float volume, float fadeSeconds,
                Transform listener = null)
            {
                bool indoors = listener != null;
                var ambient = group.GetComponentsInChildren<DistanceAmbientAudio>(true).FirstOrDefault(a => a.name == name);
                if (ambient == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                    Undo.RegisterCreatedObjectUndo(instance, "Place ambience");
                    instance.name = name;
                    ambient = instance.GetComponent<DistanceAmbientAudio>();
                    var data = new SerializedObject(ambient);
                    data.FindProperty("clip").objectReferenceValue = clip;
                    data.FindProperty("listener").objectReferenceValue = listener;
                    data.FindProperty("baseVolume").floatValue = volume;
                    data.FindProperty("useDistanceAttenuation").boolValue = indoors;
                    data.FindProperty("duckDuringDialogue").boolValue = false;
                    data.FindProperty("fadeSeconds").floatValue = fadeSeconds;
                    if (indoors)
                    {
                        Bounds bounds = room.sprite.bounds;
                        instance.transform.position = room.transform.TransformPoint(new Vector3(
                            Mathf.Lerp(bounds.min.x, bounds.max.x, .6f), Mathf.Lerp(bounds.min.y, bounds.max.y, .43f), 0f));
                        data.FindProperty("fullVolumeDistance").floatValue = .2f;
                        data.FindProperty("silentDistance").floatValue = 5.1f;
                        data.FindProperty("distantGain").floatValue = .42f / .85f;
                    }
                    var days = data.FindProperty("days");
                    days.arraySize = terrace.StoryDayCount;
                    for (int day = 0; day < days.arraySize; day++)
                    {
                        var entry = days.GetArrayElementAtIndex(day);
                        bool commuteIn = !rainOnly || GameAudioRules.IsRainy(day, EGameFlow.CommuteIn);
                        bool commuteOut = !rainOnly || GameAudioRules.IsRainy(day, EGameFlow.CommuteOut);
                        entry.FindPropertyRelative("day").intValue = day;
                        entry.FindPropertyRelative("play").boolValue = commuteIn;
                        entry.FindPropertyRelative("commuteOut").enumValueIndex = (int)(commuteIn == commuteOut
                            ? DistanceAmbientAudio.CommuteOutPlayback.SameAsCommuteIn
                            : commuteOut ? DistanceAmbientAudio.CommuteOutPlayback.Play : DistanceAmbientAudio.CommuteOutPlayback.Mute);
                    }
                    data.ApplyModifiedProperties();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(ambient);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    changed = true;
                }

                var terraceData = new SerializedObject(terrace);
                var sounds = terraceData.FindProperty(indoors ? "indoorAmbience" : "terraceAmbience");
                for (int i = 0; i < sounds.arraySize; i++)
                    if (sounds.GetArrayElementAtIndex(i).objectReferenceValue == ambient) return;
                Undo.RecordObject(terrace, "Connect ambience");
                sounds.arraySize++;
                sounds.GetArrayElementAtIndex(sounds.arraySize - 1).objectReferenceValue = ambient;
                terraceData.ApplyModifiedProperties();
                EditorUtility.SetDirty(terrace);
                changed = true;
            }
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
