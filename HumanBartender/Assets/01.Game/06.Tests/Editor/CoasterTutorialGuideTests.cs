using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 0일차 코스터 수업(웹 tutorial-view.js coaster): 화면을 어둡게 하고 공급함과 손님 앞 자리만 비워 강조하며,
/// 공급함에서 자리로 흐르는 곡선 점선 화살표와 공급함 옆 안내 상자를 띄우는지 검사한다.
/// </summary>
public sealed class CoasterTutorialGuideTests
{
    const string OverlayPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/04.Tutorial/StoryTutorialOverlay.prefab";
    const string TrayPath = "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/03.Serving/CoasterSupplyTray.prefab";
    const string PlayPath = "Assets/01.Game/01.Scenes/Play.unity";

    [Test]
    public void ServeSpotHighlightsTheBottomOfTheWebDropZone()
    {
        var spot = AssetDatabase.LoadAssetAtPath<TableServeSpot>(CoasterUISetup.ServeSpotPrefabPath);
        var root = (RectTransform)spot.transform;
        var area = spot.GuideArea;
        Assert.That(area, Is.Not.Null, "Tools/Tycoon/Connect Coaster Tutorial Guide로 연결한다.");
        Assert.That(area.parent, Is.EqualTo(root));
        Assert.That(root.sizeDelta, Is.EqualTo(new Vector2(120, 140)), "웹 .native-coaster 자리");
        Assert.That(area.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(area.anchorMax, Is.EqualTo(new Vector2(1, 0)));
        Assert.That(area.pivot, Is.EqualTo(new Vector2(.5f, 0)));
        Assert.That(area.sizeDelta, Is.EqualTo(new Vector2(0, 49)), "자리 높이 140의 아래 35%");
        Assert.That(area.localScale, Is.EqualTo(Vector3.one));
        Assert.That(area.GetComponent<Graphic>(), Is.Null, "강조 영역은 그림 없는 묶음이다.");
        Assert.That(area.name, Does.Not.Contain(" "));
    }

    [Test]
    public void PlaySceneConnectsTheSupplyTrayToTheServingView()
    {
        var scene = EditorSceneManager.OpenScene(PlayPath, OpenSceneMode.Additive);
        try
        {
            var serving = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CraftServingView>(true)).Single();
            Assert.That(serving.SupplyTray, Is.Not.Null, "Tools/Tycoon/Connect Coaster Tutorial Guide로 연결한다.");
            Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(serving.SupplyTray.gameObject)),
                Is.EqualTo(TrayPath));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void DragRouteLeavesTheTrayUpwardAndEntersTheSpotFromTheRight()
    {
        var view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<StoryTutorialView>(OverlayPath));
        try
        {
            // 1280×720 기준: 공급함은 오른쪽 아래, 놓을 자리는 가운데 아래쪽(웹 캡처와 같은 배치).
            var tray = Rect.MinMaxRect(420, -340, 595, -265);
            var spot = Rect.MinMaxRect(-77, -175, 77, -112);
            var route = view.DragRoute(tray, spot);
            Assert.That(route.from.x, Is.EqualTo(tray.xMin - 12).Within(.01f), "공급함 왼쪽 12px 밖에서 출발");
            Assert.That(route.from.y, Is.EqualTo(tray.yMax - tray.height * .4f).Within(.01f));
            Assert.That(route.to.x, Is.EqualTo(spot.xMax + 10).Within(.01f), "자리 오른쪽 10px 밖으로 들어온다");
            Assert.That(route.to.y, Is.EqualTo(spot.yMax - spot.height * .75f).Within(.01f));
            Assert.That(route.fromBend - route.from, Is.EqualTo(new Vector2(-120, 75)), "웹 C x-120 y-75(위로 솟음)");
            Assert.That(route.toBend - route.to, Is.EqualTo(new Vector2(125, -50)), "웹 tx+125 ty+50");
            Vector2 tip = route.to - route.toBend;
            Assert.That(tip.x < 0 && tip.y > 0, Is.True, "화살촉은 왼쪽 위를 향한다.");
            Vector2 middle = StoryTutorialGuideGraphic.Bezier(route.from, route.fromBend, route.toBend, route.to, .5f);
            Assert.That(middle.x, Is.InRange(spot.xMax, tray.xMin), "곡선은 두 강조 영역 사이를 지난다.");
        }
        finally { Object.DestroyImmediate(view.gameObject); }
    }

    [Test]
    public void CopyHidesWhileDraggingButTheDimAndRouteStay()
    {
        var view = StoryTutorialView.Create(AssetDatabase.LoadAssetAtPath<StoryTutorialView>(OverlayPath), null);
        bool dragging = false;
        try
        {
            view.ShowGuide("손님 맞이하기", "코스터를 크리스 앞으로 드래그하세요.", null, null, drag: true,
                hideCopyWhile: () => dragging);
            view.UpdateGuide();
            Assert.That(view.InstructionPanel.gameObject.activeSelf, Is.True);
            Assert.That(view.Instruction, Is.EqualTo("코스터를 크리스 앞으로 드래그하세요."));
            Assert.That(view.Heading, Is.EqualTo("손님 맞이하기"));
            dragging = true;
            view.UpdateGuide();
            Assert.That(view.InstructionPanel.gameObject.activeSelf, Is.False, "웹 data-dragging: 끄는 동안 안내 상자만 숨긴다.");
            dragging = false;
            view.UpdateGuide();
            Assert.That(view.InstructionPanel.gameObject.activeSelf, Is.True);
            view.Hide();
            Assert.That(view.InstructionPanel.gameObject.activeSelf, Is.False);
            // 다음 안내는 이전 수업의 숨기기 조건을 물려받지 않는다.
            dragging = true;
            view.ShowGuide("레시피 선택", "칵테일 레시피를 눌러 주세요.", null);
            view.UpdateGuide();
            Assert.That(view.InstructionPanel.gameObject.activeSelf, Is.True);
        }
        finally { Object.DestroyImmediate(view.gameObject); }
    }

    [Test]
    public void HeldCoasterDrawsAboveTheTutorialDim()
    {
        var overlay = AssetDatabase.LoadAssetAtPath<StoryTutorialView>(OverlayPath).GetComponent<Canvas>();
        var coaster = AssetDatabase.LoadAssetAtPath<GameObject>(TrayPath).GetComponentInChildren<CoasterDragItem>(true);
        int held = new SerializedObject(coaster).FindProperty("heldSortingOrder").intValue;
        Assert.That(held, Is.GreaterThan(overlay.sortingOrder), "웹 #drag-ghost는 튜토리얼 안내 위에 그려진다.");
    }
}
