using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WorldInteractionContentTests
{
    private GameObject host;
    private WorldInteractionSceneContext context;
    private WorldInteractionContent content;
    private NewInteractPointDataSO definitions;
    private PlayerDataSO wallet;
    private Scene preview;
    private EGameFlow previousFlow;

    [SetUp]
    public void SetUp()
    {
        previousFlow = GameStateManager.Instance.GameFlow;
        preview = EditorSceneManager.NewPreviewScene();
        host = new GameObject("표시와 상호작용 분리 테스트");
        SceneManager.MoveGameObjectToScene(host, preview);
        context = host.AddComponent<WorldInteractionSceneContext>();
        content = host.AddComponent<WorldInteractionContent>();
        host.AddComponent<SpriteRenderer>();
        host.AddComponent<BoxCollider2D>();
        definitions = ScriptableObject.CreateInstance<NewInteractPointDataSO>();
        wallet = ScriptableObject.CreateInstance<PlayerDataSO>();
        Set(context, "interactPointData", definitions);
        Set(context, "conditions", new ConditionUtil(GameStateManager.Instance, wallet, wallet));
        Set(content, "sourceId", "door");
        content.Initialize(context);
    }

    [TearDown]
    public void TearDown()
    {
        GameStateManager.Instance.GameFlow = previousFlow;
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(definitions);
        Object.DestroyImmediate(wallet);
        EditorSceneManager.ClosePreviewScene(preview);
    }

    [TestCase(true), TestCase(false)]
    public void SpawnConditionsHideOnlyObjectsWithVisibilityControl(bool controlVisibility)
    {
        Set(content, "controlObjectVisibility", controlVisibility);
        Define("money >= 500", null);
        wallet.SetMoney(500);
        content.Refresh();
        Assert.IsTrue(content.IsAvailable);
        Assert.IsTrue(host.activeSelf);

        wallet.SetMoney(0);
        content.Refresh();
        Assert.IsFalse(content.IsAvailable);
        Assert.IsFalse(content.TryResolve(1.5f, out _));
        Assert.AreEqual(!controlVisibility, host.activeSelf);
        Assert.IsTrue(host.GetComponent<SpriteRenderer>().enabled);
        Assert.IsTrue(host.GetComponent<BoxCollider2D>().enabled);

        wallet.SetMoney(500);
        content.Refresh();
        Assert.IsTrue(content.IsAvailable);
        Assert.IsTrue(host.activeSelf);
    }

    [TestCase(true), TestCase(false)]
    public void InteractionConditionLocksInteractionWithoutHidingObject(bool controlVisibility)
    {
        Set(content, "controlObjectVisibility", controlVisibility);
        Define(null, "money >= 500");
        wallet.SetMoney(0);
        content.Refresh();
        Assert.IsTrue(host.activeSelf);
        Assert.IsFalse(content.IsAvailable);
        Assert.IsFalse(content.TryResolve(1.5f, out _));
        wallet.SetMoney(500);
        content.Refresh();
        Assert.IsTrue(content.TryResolve(1.5f, out _));
    }

    [Test]
    public void UnmanagedObjectKeepsItsAuthoredInactiveState()
    {
        Set(content, "controlObjectVisibility", false);
        Define(null, null);
        host.SetActive(false);
        content.Refresh();
        Assert.IsFalse(host.activeSelf, "표시를 다른 기능이 관리할 때 CSV가 임의로 켜면 안 됨.");
    }

    [Test]
    public void DefaultVisibilityControlDefersHidingUntilActiveSessionEnds()
    {
        Define("money >= 500", null);
        wallet.SetMoney(0);
        content.Refresh(keepActiveDuringSession: true);
        Assert.IsTrue(host.activeSelf);
        Assert.IsFalse(content.IsAvailable);
        content.Refresh();
        Assert.IsFalse(host.activeSelf, "기존 대상의 기본 등장·퇴장 동작을 유지해야 함.");
    }

    [Test]
    public void HomeUsesVisibleRoomEntranceAndLocksOnlyInteractionOnReturnHome()
    {
        var scene = EditorSceneManager.OpenScene(WorldInteractionCutsceneTests.HomePath, OpenSceneMode.Additive);
        try
        {
            var contents = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionContent>(true));
            var door = contents.Single(candidate => candidate.SourceId == "home_exit_door");
            Assert.AreEqual("Room_Entrance", door.name);
            Assert.IsNotNull(door.GetComponent<SpriteRenderer>().sprite);
            Assert.AreSame(door.GetComponent<WorldEntranceAction>(), door.GetComponent<WorldInteractionTarget>().Action);
            Assert.IsFalse(new SerializedObject(door).FindProperty("useSpotPosition").boolValue);
            definitions.interactPointData = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"))
                .Read<NewInteractPointData[]>("interact_points_home");
            door.Initialize(context);
            Vector3 position = door.transform.position;
            foreach (var flow in new[] { EGameFlow.CommuteIn, EGameFlow.CommuteOut, EGameFlow.CommuteIn })
            {
                GameStateManager.Instance.GameFlow = flow;
                door.Refresh();
                Assert.IsTrue(door.gameObject.activeSelf, flow.ToString());
                Assert.IsTrue(door.GetComponent<SpriteRenderer>().enabled);
                Assert.IsTrue(door.GetComponent<BoxCollider2D>().enabled);
                Assert.AreEqual(position, door.transform.position);
                Assert.AreEqual(flow == EGameFlow.CommuteIn, door.TryResolve(1.5f, out _));
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void ExistingStreetNpcsKeepVisibilityControlEnabled()
    {
        var scene = EditorSceneManager.OpenScene(WorldInteractionCutsceneTests.OutsidePath, OpenSceneMode.Additive);
        try
        {
            var contents = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionContent>(true)).ToArray();
            foreach (string source in new[] { "bubi", "shiba" })
            {
                var npc = contents.Single(candidate => candidate.SourceId == source);
                Assert.IsTrue(new SerializedObject(npc).FindProperty("controlObjectVisibility").boolValue, source);
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    private void Define(string spawnWhen, string interactWhen)
    {
        definitions.interactPointData = new[] { new NewInteractPointData
        {
            Id = "door", SourceId = "door", Phase = EGameFlow.Both,
            ActionType = EActionType.Transition, ActionRef = "exit_home",
            ActivationMode = EActivationMode.Interact, SpawnWhen = spawnWhen, InteractWhen = interactWhen
        } };
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
