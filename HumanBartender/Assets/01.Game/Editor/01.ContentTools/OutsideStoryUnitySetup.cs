using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Unity 씬 배치를 작성하고 기존 Studio 원본 연결을 검사한다. 런타임에는 에셋을 생성하지 않는다.</summary>
public static class OutsideStoryUnitySetup
{
    [MenuItem("Tools/Story/Set Up Native Outside Targets")]
    public static void SetUpTargets()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            SetUpScene("OutSide", new[]
            {
                ("neighborhood-reveal", new Vector2(-15.59f, 10.07f), "고양이"),
                ("neighborhood-repairGreeting", new Vector2(-11.675f, -.974f), "수리공"),
                ("neighborhood-guardBlocked", new Vector2(-14.125f, -.974f), "경비원"),
                ("neighborhood-bossIntro", new Vector2(-15.5f, -.974f), "갱단 두목")
            });
            SetUpScene("Home", new[] { ("neighborhood-homeCat", new Vector2(2.65f, -.46f), "고양이") });
        }
        finally
        {
            if (setup.Any(scene => scene.isLoaded && scene.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    static void SetUpScene(string name, (string id, Vector2 position, string label)[] entries)
    {
        var scene = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/" + name + ".unity", OpenSceneMode.Single);
        var targets = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true)).ToArray();
        Transform storyEvents = null;
        if (name == "OutSide")
        {
            var gameplay = scene.GetRootGameObjects().SingleOrDefault(root => root.name == "04.Gameplay");
            storyEvents = gameplay != null ? gameplay.transform.Find("InteractionObjects/StoryEvents") : null;
            if (storyEvents == null) throw new InvalidOperationException("OutSide의 04.Gameplay/InteractionObjects/StoryEvents가 없습니다.");
        }
        foreach (var entry in entries)
        {
            var found = targets.Where(target => target.StorySceneId == entry.id).ToArray();
            if (found.Length > 1) throw new InvalidOperationException("중복 사건 대상: " + entry.id);
            if (found.Length == 1) continue; // 기존 Inspector 편집과 컷신 연결은 보존한다.
            var go = new GameObject(entry.id);
            SceneManager.MoveGameObjectToScene(go, scene);
            if (storyEvents != null) go.transform.SetParent(storyEvents, false);
            go.transform.position = new Vector3(entry.position.x, entry.position.y, 0);
            go.AddComponent<WorldInteractionTarget>().ConfigureStory(entry.id, 1.4f, entry.label);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    const string Art = "Assets/01.Game/03.Content/01.ArtResources/01.Characters/02.Outside/";
    sealed class Role
    {
        internal string Id, File;
        internal Vector2 Offset;
        internal bool Visible;
        internal string[] Aliases;
        internal Role(string id, string file, Vector2 offset = default, bool visible = true, params string[] aliases)
        { Id = id; File = file; Offset = offset; Visible = visible; Aliases = aliases; }
    }
    static string Npc(string file) => "05.StreetNPC/01.Idle/" + file + ".png";
    static Role Cat() => new("boobi", "04.Bubi/01.Idle/bubi_sleep.png");
    static Role Chris() => new("chris", "02.Chris/Chris_idle.png", new Vector2(-.9f, -.51f), false);

    [MenuItem("Tools/Story/Set Up World Story Actors")]
    public static void SetUpWorldActors()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string name in new[] { "Home", "OutSide" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/" + name + ".unity", OpenSceneMode.Single);
                foreach (var target in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true)))
                {
                    string id = target.StorySceneId;
                    Role[] roles = id switch
                    {
                        "neighborhood-reveal" => new[] { new Role("boobi", "04.Bubi/01.Idle/bubi_sleep.png", new Vector2(.25f, -.25f)),
                            new Role("neighbor", Npc("Neighbor_idle"), new Vector2(1.33f, -.344f), false) },
                        "neighborhood-homeCat" => new[] { Cat(), Chris() },
                        "neighborhood-repairGreeting" => new[] { new Role("repair", Npc("Repairman_idle")) },
                        "neighborhood-guardBlocked" => new[] { new Role("guard", Npc("GangNPC1_idle")) },
                        "neighborhood-bossIntro" => new[] { new Role("boss", Npc("GangNPC5_idle")), new Role("gangmate", Npc("GangNPC3_idle"), new Vector2(.78f, 0)) },
                        "day1-corridor-thug" or "day2-corridor-thug" or "day2-thug-information" => new[] {
                            new Role("thug", Npc("Neighbor_idle"), default, true, "day1-corridor-thug", "day2-corridor-thug", "day2-thug-information") },
                        "commute2" or "commute3" => new[] { new Role("samho", "03.Samho/01.Idle/samho_idle.png") },
                        "outside_day1_samho_smuggler" or "day3-samho-contract" => new[] {
                            new Role("samho", "03.Samho/01.Idle/samho_idle.png", new Vector2(-.6f, 0), true, "trade-samho", "contract-samho"),
                            new Role("dealer", Npc("npc_m2_idle"), new Vector2(1.4f, 0), true, "trade-smuggler", "contract-smuggler") },
                        "shiba-shop" or "shiba-shop-revisit" => new[] { new Role("shiba", Npc("shiba_idle")) },
                        _ => null
                    };
                    if (roles != null) AddActors(target, roles);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            if (setup.Any(scene => scene.isLoaded && scene.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    static void AddActors(WorldInteractionTarget target, Role[] roles)
    {
        if (target.GetComponent<WorldStoryActors>() is { } existing)
        {
            UpgradeGeneratedAnchors(existing, roles);
            return;
        }
        var group = target.gameObject.AddComponent<WorldStoryActors>();
        var speakers = target.GetComponent<WorldDialogueSpeakers>() ?? target.gameObject.AddComponent<WorldDialogueSpeakers>();
        var bindings = new List<(string, Transform)>();
        var views = new List<WorldStoryActors.Actor>();
        foreach (var role in roles)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Art + role.File).OfType<Sprite>().OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
            if (sprites.Length == 0) throw new InvalidOperationException("월드 화자 Sprite가 없습니다: " + Art + role.File);
            var go = new GameObject("Story actor · " + role.Id);
            go.transform.SetParent(target.transform, false);
            go.transform.position = target.transform.position + (Vector3)role.Offset;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites[0];
            renderer.sortingLayerName = "Character";
            renderer.sortingOrder = 5;
            renderer.enabled = role.Visible;
            // 원본 Sprite의 PPU와 캐릭터 비율을 유지한다. 이후 크기와 위치는 씬에서 조정한다.
            go.transform.localScale = Vector3.one;
            views.Add(new WorldStoryActors.Actor { Id = role.Id, Root = go.transform, Renderer = renderer, VisibleOutsideSession = role.Visible });
            bindings.Add((role.Id, go.transform));
            foreach (string alias in role.Aliases) bindings.Add((alias, go.transform));
        }
        speakers.Configure(bindings.ToArray());
        group.Configure(views.ToArray(), speakers);
        UpgradeGeneratedAnchors(group, roles);
    }

    static void UpgradeGeneratedAnchors(WorldStoryActors group, Role[] roles)
    {
        using var serialized = new SerializedObject(group);
        var entries = serialized.FindProperty("actors");
        var bindings = new List<(string, Transform)>();
        for (int i = 0; i < entries.arraySize; i++)
        {
            var item = entries.GetArrayElementAtIndex(i);
            var root = (Transform)item.FindPropertyRelative("Root").objectReferenceValue;
            var rendererProperty = item.FindPropertyRelative("Renderer");
            var renderer = (SpriteRenderer)rendererProperty.objectReferenceValue;
            string id = item.FindPropertyRelative("Id").stringValue;
            if (root == null || renderer == null || !root.name.StartsWith("Story actor · ", StringComparison.Ordinal)) return;
            // 초기 배치한 Sprite만 발 기준 자식으로 옮긴다. 사용자 위치와 새로 편집한 시각 설정은 보존한다.
            if (renderer.transform == root)
            {
                var visual = new GameObject("Sprite").AddComponent<SpriteRenderer>();
                visual.transform.SetParent(root, false);
                visual.sprite = renderer.sprite; visual.color = renderer.color; visual.flipX = renderer.flipX;
                visual.sortingLayerID = renderer.sortingLayerID; visual.sortingOrder = renderer.sortingOrder;
                visual.enabled = renderer.enabled;
                visual.transform.localPosition = Vector3.up * -visual.sprite.bounds.min.y;
                UnityEngine.Object.DestroyImmediate(renderer);
                renderer = visual;
                rendererProperty.objectReferenceValue = visual;
            }
            var head = root.Find("Speech anchor");
            if (head == null)
            {
                head = new GameObject("Speech anchor").transform;
                head.SetParent(root, false);
                head.localPosition = Vector3.up * renderer.sprite.bounds.size.y;
            }
            bindings.Add((id, head));
            var role = roles.Single(value => value.Id == id);
            foreach (string alias in role.Aliases) bindings.Add((alias, head));
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        group.GetComponent<WorldDialogueSpeakers>().Configure(bindings.ToArray());
    }

    // 첫 출근 배치의 초기값(월드 좌표). 이후 위치 조정은 씬에서 하며, 이 도구는 이미 있는 오브젝트를 옮기지 않는다.
    static readonly Vector2 FirstCommuteSpawnPosition = new(-1.55f, -.7f);
    static readonly Vector2 FirstCommuteWalkTarget = new(0f, -.7f);
    const float FirstCommuteLeftLimit = -.9f, FirstCommuteRightLimit = 2.15f, WallWidth = .2f, WallHeight = 4f;

    /// <summary>
    /// OutSide 씬에 0일차 첫 출근의 시작 위치·도착 지점·이동 경계를 만들고 Player와 씬 컨텍스트에 연결한다.
    /// 경계 벽은 플레이어 중심이 [-0.9, 2.15] 안에 머물도록 충돌 박스 폭만큼 바깥에 둔다.
    /// </summary>
    [MenuItem("Tools/Story/Set Up First Commute Arrival")]
    public static void SetUpFirstCommute()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/OutSide.unity", OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var player = roots.SelectMany(root => root.GetComponentsInChildren<Player>(true)).Single();
            var context = roots.SelectMany(root => root.GetComponentsInChildren<WorldInteractionSceneContext>(true)).Single();

            using var playerObject = new SerializedObject(player);
            var barSpawn = (Transform)playerObject.FindProperty("barspawn").objectReferenceValue;
            if (barSpawn == null) throw new InvalidOperationException("Player의 Bar Spawn이 연결되어 있어야 같은 부모에 첫 출근 위치를 만들 수 있습니다.");
            var spawn = FindOrCreate(barSpawn.parent, scene, "FirstCommuteSpawn", FirstCommuteSpawnPosition);
            playerObject.FindProperty("firstCommuteSpawn").objectReferenceValue = spawn;
            playerObject.ApplyModifiedPropertiesWithoutUndo();

            var arrival = roots.SelectMany(root => root.GetComponentsInChildren<FirstCommuteArrival>(true)).SingleOrDefault();
            if (arrival == null)
            {
                var go = new GameObject("FirstCommuteArrival");
                SceneManager.MoveGameObjectToScene(go, scene);
                arrival = go.AddComponent<FirstCommuteArrival>();
            }
            var target = FindOrCreate(arrival.transform, scene, "WalkTarget", FirstCommuteWalkTarget);
            var bounds = arrival.transform.Find("Bounds")?.gameObject;
            if (bounds == null)
            {
                bounds = new GameObject("Bounds");
                bounds.transform.SetParent(arrival.transform, false);
                var movement = player.GetComponent<PlayerMovement2D>();
                var collider = player.GetComponent<BoxCollider2D>();
                using var movementObject = new SerializedObject(movement);
                int mask = movementObject.FindProperty("groundLayer").intValue;
                if (mask == 0) throw new InvalidOperationException("PlayerMovement2D의 Ground Layer가 비어 있습니다.");
                int layer = 0;
                while ((mask & (1 << layer)) == 0) layer++;
                float half = collider.size.x * Mathf.Abs(player.transform.lossyScale.x) * .5f;
                float centerY = FirstCommuteWalkTarget.y + WallHeight * .5f;
                AddWall(bounds.transform, "LeftWall", new Vector2(FirstCommuteLeftLimit - half - WallWidth * .5f, centerY), layer);
                AddWall(bounds.transform, "RightWall", new Vector2(FirstCommuteRightLimit + half + WallWidth * .5f, centerY), layer);
                bounds.SetActive(false);
            }

            using var arrivalObject = new SerializedObject(arrival);
            arrivalObject.FindProperty("walkTarget").objectReferenceValue = target;
            arrivalObject.FindProperty("bounds").objectReferenceValue = bounds;
            arrivalObject.ApplyModifiedPropertiesWithoutUndo();

            using var contextObject = new SerializedObject(context);
            contextObject.FindProperty("firstCommuteArrival").objectReferenceValue = arrival;
            contextObject.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (setup.Any(scene => scene.isLoaded && scene.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    static Transform FindOrCreate(Transform parent, Scene scene, string name, Vector2 worldPosition)
    {
        var found = parent != null ? parent.Find(name) : scene.GetRootGameObjects().FirstOrDefault(root => root.name == name)?.transform;
        if (found != null) return found; // 씬에서 옮긴 위치를 보존한다.
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        else SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.position = new Vector3(worldPosition.x, worldPosition.y, 0);
        return go.transform;
    }

    static void AddWall(Transform parent, string name, Vector2 worldCenter, int layer)
    {
        var wall = new GameObject(name) { layer = layer };
        wall.transform.SetParent(parent, false);
        wall.transform.position = new Vector3(worldCenter.x, worldCenter.y, 0);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(WallWidth, WallHeight);
    }

    const string TutorialOverlayPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/04.Tutorial/StoryTutorialOverlay.prefab";

    /// <summary>Home 씬에 새 게임 오프닝을 만들고 씬 컨텍스트에 연결한다. 이미 있으면 인스펙터 편집값을 보존한다.</summary>
    [MenuItem("Tools/Story/Set Up New Game Opening")]
    public static void SetUpNewGameOpening()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Home.unity", OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var context = roots.SelectMany(root => root.GetComponentsInChildren<WorldInteractionSceneContext>(true)).Single();
            var opening = roots.SelectMany(root => root.GetComponentsInChildren<NewGameOpening>(true)).SingleOrDefault();
            if (opening == null)
            {
                var go = new GameObject("NewGameOpening");
                SceneManager.MoveGameObjectToScene(go, scene);
                opening = go.AddComponent<NewGameOpening>();
            }
            using (var openingObject = new SerializedObject(opening))
            {
                var prefab = openingObject.FindProperty("viewPrefab");
                if (prefab.objectReferenceValue == null)
                    prefab.objectReferenceValue = AssetDatabase.LoadAssetAtPath<StoryTutorialView>(TutorialOverlayPath)
                        ?? throw new InvalidOperationException("튜토리얼 오버레이 프리팹이 없습니다: " + TutorialOverlayPath);
                openingObject.ApplyModifiedPropertiesWithoutUndo();
            }
            using (var contextObject = new SerializedObject(context))
            {
                contextObject.FindProperty("newGameOpening").objectReferenceValue = opening;
                contextObject.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (setup.Any(scene => scene.isLoaded && scene.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    /// <summary>batchmode용: 첫 출근·새 게임 오프닝 배치를 실행하고 종료 코드로 결과를 알린다.</summary>
    public static void SetUpFirstCommuteBatch()
    {
        try { SetUpFirstCommute(); SetUpNewGameOpening(); Debug.Log("FIRST_COMMUTE_SETUP_OK"); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static void SetUpBatch()
    {
        try { SetUpTargets(); SetUpWorldActors(); Debug.Log("WORLD_STORY_SETUP_OK"); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
