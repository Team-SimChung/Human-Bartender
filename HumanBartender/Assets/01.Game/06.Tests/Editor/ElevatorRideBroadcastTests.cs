using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>엘리베이터 일반 탑승은 이동·로고·카메라만 진행하고 라디오 방송 대사를 재생하지 않는다(웹 기준).</summary>
public sealed class ElevatorRideBroadcastTests
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void ElevatorKeepsNoBroadcastSource()
    {
        var sources = typeof(OutsideElevator).GetFields(Fields)
            .Where(field => field.FieldType == typeof(OutsideElevatorRadio) || field.FieldType == typeof(WorldInteractionContent));
        Assert.That(sources, Is.Empty);
    }

    [Test]
    public void RideRequestsNoDialogueBeforeMoving()
    {
        var host = new GameObject("ElevatorBroadcastTest");
        var rider = new GameObject("ElevatorBroadcastRider");
        var data = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var elevator = host.AddComponent<OutsideElevator>();
            var conditions = new ConditionUtil(GameStateManager.Instance, data, data);
            typeof(OutsideElevator).GetField("conditions", Fields).SetValue(elevator, conditions);
            var player = rider.AddComponent<Player>();
            var content = new CsvStoryTestContent();
            int dialogueRequests = 0;
            string[] offered = null;
            var context = new WorldInteractionActionContext(player,
                (steps, anchor, token) =>
                {
                    dialogueRequests++;
                    return UniTask.FromResult(new StoryExecutionResult(StoryExecutionStatus.Completed));
                },
                // 실제 컨트롤러처럼 CSV 메뉴 장면에서 선택지를 읽는다.
                chooseOutsideChoices: (sceneId, message, canSelect, token) =>
                {
                    var options = WorldInteractionController.PrepareOutsideChoices(content, conditions, sceneId, canSelect);
                    offered = options.Select(option => option.Id).ToArray();
                    return UniTask.FromResult(options.Single(option => option.Id == "ride"));
                });
            // 상·하단 지점이 없으면 이동 직전에 멈춘다. 이전 구현은 이 지점보다 먼저 방송 재생을 요청했다.
            var error = Assert.Throws<InvalidOperationException>(
                () => elevator.ExecuteAsync(context, CancellationToken.None).GetAwaiter().GetResult());
            Assert.That(error.Message, Does.Contain("앵커"));
            Assert.That(offered, Does.Contain("ride"));
            Assert.That(dialogueRequests, Is.Zero);
            Assert.That(elevator.CanExecute, Is.True, "실패한 탑승 뒤에도 다시 탈 수 있다.");
            Assert.That(player.State, Is.EqualTo(EInteractorState.None));
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(rider);
            Object.DestroyImmediate(data);
        }
    }

    // 층·수리 여부마다 CSV 메뉴 장면을 고르고, 수리 전에는 빠른 이동을 잠그지 않고 아예 보여 주지 않는다(웹 기준).
    [TestCase(false, false, "outside_elevator_menu_bottom", new[] { "ride", "cancel" }, "올라간다")]
    [TestCase(true, false, "outside_elevator_menu_top", new[] { "ride", "cancel" }, "내려간다")]
    [TestCase(false, true, "outside_elevator_menu_bottom_fast", new[] { "fast", "ride", "cancel" }, "올라간다")]
    [TestCase(true, true, "outside_elevator_menu_top_fast", new[] { "fast", "ride", "cancel" }, "내려간다")]
    public void MenuComesFromTheCsvSceneForTheFloorAndRepair(bool top, bool repaired, string sceneId, string[] ids, string ride)
    {
        var host = new GameObject("ElevatorMenuTest");
        var data = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var elevator = host.AddComponent<OutsideElevator>();
            var conditions = new ConditionUtil(GameStateManager.Instance, data, data);
            typeof(OutsideElevator).GetField("conditions", Fields).SetValue(elevator, conditions);
            typeof(OutsideElevator).GetField("isTop", Fields).SetValue(elevator, top);
            if (repaired) conditions.ApplyRequired("flag.elevator_repaired = true");

            Assert.That(elevator.MenuSceneId, Is.EqualTo(sceneId));
            var menu = WorldInteractionController.PrepareOutsideChoices(new CsvStoryTestContent(), conditions, sceneId, null);
            CollectionAssert.AreEqual(ids, menu.Select(option => option.Id));
            Assert.That(menu.Single(option => option.Id == "ride").Text.Ko, Is.EqualTo(ride));
            Assert.That(menu.Where(option => option.IsCancel == true).Select(option => option.Id), Is.EqualTo(new[] { "cancel" }));
            Assert.That(menu.All(option => option.Selectable == true), Is.True, "엘리베이터 메뉴에는 잠긴 선택지가 없어야 합니다.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(data);
        }
    }

    // 웹(outside.js)처럼 도착하면 루나를 층 높이에 세운다. 상·하단 지점 간격과 층 높이 차이가 달라도 어긋나지 않는다.
    [Test]
    public void ArrivalPutsLunaAtTheFloorHeightLikeTheWeb()
    {
        var host = new GameObject("ElevatorArrivalTest");
        var bottom = new GameObject("BarSpawn");
        var top = new GameObject("HomeSpawn");
        try
        {
            var elevator = host.AddComponent<OutsideElevator>();
            bottom.transform.position = new Vector3(.895f, -.7f, 0);
            top.transform.position = new Vector3(-16.401f, 9.666f, 0);
            const float travel = 10.283f;

            LogAssert.Expect(LogType.Warning, new Regex("하차 기준"));
            Assert.That(elevator.ArrivalY(false, 9.666f, -travel), Is.EqualTo(9.666f - travel).Within(.0001f),
                "기준을 연결하지 않으면 이전처럼 이동량만큼만 옮깁니다.");

            typeof(OutsideElevator).GetField("bottomArrival", Fields).SetValue(elevator, bottom.transform);
            typeof(OutsideElevator).GetField("topArrival", Fields).SetValue(elevator, top.transform);
            Assert.That(elevator.ArrivalY(false, 9.666f, -travel), Is.EqualTo(-.7f).Within(.0001f), "내려오면 바 앞 높이에 섭니다.");
            Assert.That(elevator.ArrivalY(true, -.7f, travel), Is.EqualTo(9.666f).Within(.0001f), "올라가면 집 앞 높이에 섭니다.");
            Assert.That(elevator.ArrivalY(false, 9.583f, -travel), Is.EqualTo(-.7f).Within(.0001f), "이전에 어긋난 높이에서 출발해도 바로잡습니다.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(bottom);
            Object.DestroyImmediate(top);
        }
    }

    // OutSide 씬의 엘리베이터가 웹과 같은 하차 높이(바 앞·집 앞 시작 지점)를 쓰는지 확인한다.
    [Test]
    public void OutsideElevatorArrivalsUseTheBarAndHomeSpawns()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/OutSide.unity");
        try
        {
            var elevator = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<OutsideElevator>(true)).Single();
            var bottom = (Transform)typeof(OutsideElevator).GetField("bottomArrival", Fields).GetValue(elevator);
            var top = (Transform)typeof(OutsideElevator).GetField("topArrival", Fields).GetValue(elevator);
            Assert.That(bottom != null ? bottom.name : null, Is.EqualTo("BarSpawn"),
                "Elevator의 아래층 하차 기준에 BarSpawn을 연결하세요.");
            Assert.That(top != null ? top.name : null, Is.EqualTo("HomeSpawn"),
                "Elevator의 위층 하차 기준에 HomeSpawn을 연결하세요.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void OutsideSceneHasNoOtherPathThatPlaysTheElevatorRadio()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/OutSide.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var elevators = roots.SelectMany(root => root.GetComponentsInChildren<OutsideElevator>(true)).ToArray();
            var targets = roots.SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true)).ToArray();
            Assert.That(elevators, Is.Not.Empty);
            Assert.That(targets.Any(target => target.Action is OutsideElevator elevator && elevators.Contains(elevator)), Is.True);
            Assert.That(targets.Where(target => target.Content != null && target.Content.SourceId == "elevator_radio"), Is.Empty,
                "엘리베이터 방송 데이터가 주변 대화나 직접 상호작용으로 재생되지 않아야 합니다.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
