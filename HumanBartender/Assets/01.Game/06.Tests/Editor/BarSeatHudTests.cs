using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>바 좌석 표시가 튜토리얼 오버레이와 상관없이 Play 씬의 바 UI로 저장되어 있는지 검사한다.</summary>
public sealed class BarSeatHudTests
{
    const string ScenePath = "Assets/01.Game/01.Scenes/Play.unity";

    static T Find<T>(UnityEngine.SceneManagement.Scene scene) where T : Component =>
        scene.GetRootGameObjects().Select(root => root.GetComponentInChildren<T>(true)).FirstOrDefault(found => found != null);

    static Canvas[] BarCanvases(Object owner) =>
        (Canvas[])owner.GetType().GetField("barCanvases", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    [Test]
    public void SeatHudIsSavedInThePlaySceneAsBarUi()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var hud = Find<BarSeatStatusView>(scene);
        Assert.That(hud, Is.Not.Null, "Play 씬에 바 좌석 표시가 저장되어 있어야 합니다.");
        Assert.That(hud.GetComponentInParent<StoryTutorialView>(true), Is.Null, "좌석 표시는 튜토리얼 오버레이 밖에 있어야 합니다.");
        Assert.That(hud.Indicators, Is.Not.Null);
        Assert.That(hud.LeftKey, Is.Not.Null);
        Assert.That(hud.RightKey, Is.Not.Null);

        var canvas = hud.GetComponentInParent<Canvas>(true).rootCanvas;
        var scaler = canvas.GetComponent<CanvasScaler>();
        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(960, 540)), "바 UI 기준 해상도");
        Assert.That(scaler.matchWidthOrHeight, Is.Zero);
        Assert.That(canvas.sortingOrder, Is.LessThan(100), "기믹 HUD·서빙·일시정지 메뉴보다 아래여야 합니다.");

        Assert.That(BarCanvases(Find<GimmickRunner>(scene)), Does.Contain(canvas), "기믹 중에 끄는 바 UI 목록");
        Assert.That(BarCanvases(Find<CraftPrepStageHost>(scene)), Does.Contain(canvas), "준비 화면 동안 끄는 바 UI 목록");
    }

    static object Field(object owner, string name) =>
        owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    [Test]
    public void RepairConnectsPresenterAndRestoresLiveSeatRendering()
    {
        // 현재 열린 씬과 저장 파일을 건드리지 않고, 저장된 구형 HUD도 복구할 수 있는지 확인한다.
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var hud = Find<BarSeatStatusView>(scene);
            BarSeatHudRepair.Repair(hud);
            var presenter = hud.GetComponent<BarSeatHudPresenter>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(Field(presenter, "view"), Is.SameAs(hud));
            foreach (string name in new[] { "phases", "characters", "guests", "craft", "preparation", "recipes", "playCamera", "servicePanel" })
                Assert.That((Object)Field(presenter, name) != null, Is.True, name);
            var slots = (GuestSlot[])Field(presenter, "guestSlots");
            Assert.That(slots.Select(s => s.SlotType), Is.EqualTo(new[] { ESlotType.Left, ESlotType.Middle, ESlotType.Right }));
            Assert.That(hud.GetComponentsInChildren<StoryTutorialShape>(true), Is.Empty);
            Assert.That(hud.Indicators.Find("Seats").childCount, Is.EqualTo(3));
            Assert.That((Object)Field(hud, "leftKeyLabel") != null, Is.True);
            Assert.That((Object)Field(hud, "rightKeyLabel") != null, Is.True);

            // 복구된 Image 연결로 실제 색과 선택 링이 바뀌어야 한다.
            hud.SetSeat(0, EBarSeatSignal.Empty, false, 10);
            hud.SetSeat(1, EBarSeatSignal.Occupied, true, 10);
            hud.SetSeat(2, EBarSeatSignal.Waiting, false, 10);
            var dots = (Image[])Field(hud, "dots");
            var rings = (Image[])Field(hud, "rings");
            Assert.That(dots.Select(d => d.color), Is.EqualTo(new[] { BarSeatStatusView.EmptyColor, BarSeatStatusView.SelectedColor, BarSeatStatusView.WaitingColor }));
            Assert.That(rings.Select(r => r.gameObject.activeSelf), Is.EqualTo(new[] { false, true, false }));
            hud.SetExploration(true, ESlotType.Middle, false);
            Assert.That(hud.LeftKey.gameObject.activeSelf && hud.RightKey.gameObject.activeSelf, Is.True);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void RepairCanRunAgainWithoutReplacingEditedLayoutOrReferences()
    {
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var hud = Find<BarSeatStatusView>(scene);
            BarSeatHudRepair.Repair(hud);
            var presenter = hud.GetComponent<BarSeatHudPresenter>();
            var layout = hud.Indicators.Find("Seats").GetComponent<HorizontalLayoutGroup>();
            var dots = (Image[])Field(hud, "dots");
            int count = hud.GetComponentsInChildren<Transform>(true).Length;
            layout.spacing = 5;
            dots[0].color = Color.magenta;
            var position = hud.Indicators.anchoredPosition + new Vector2(3, 7);
            hud.Indicators.anchoredPosition = position;
            BarSeatHudRepair.Repair(hud);
            Assert.That(hud.GetComponents<BarSeatHudPresenter>(), Has.Length.EqualTo(1));
            Assert.That(hud.GetComponent<BarSeatHudPresenter>(), Is.SameAs(presenter));
            Assert.That(hud.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            Assert.That(layout.spacing, Is.EqualTo(5));
            Assert.That(hud.Indicators.anchoredPosition, Is.EqualTo(position));
            Assert.That((Image[])Field(hud, "dots"), Is.EqualTo(dots));
            Assert.That(dots[0].color, Is.EqualTo(Color.magenta));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void SeatHudSceneReferencesAreWired()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var hud = Find<BarSeatStatusView>(scene);
        var presenter = hud.GetComponent<BarSeatHudPresenter>();
        Assert.That(presenter, Is.Not.Null, "좌석 표시와 같은 오브젝트에 BarSeatHudPresenter가 있어야 합니다.");
        Assert.That(Field(presenter, "view"), Is.SameAs(hud));
        foreach (string name in new[] { "phases", "characters", "guests", "craft", "preparation", "recipes", "playCamera", "servicePanel" })
            Assert.That((Object)Field(presenter, name) != null, Is.True, "연결되지 않은 참조: " + name);

        var slots = (GuestSlot[])Field(presenter, "guestSlots");
        Assert.That(slots.Select(slot => slot != null ? slot.SlotType : (ESlotType)(-1)),
            Is.EqualTo(new[] { ESlotType.Left, ESlotType.Middle, ESlotType.Right }), "손님 좌석은 표시 점과 같은 왼쪽·가운데·오른쪽 순서");

        foreach (string name in new[] { "dots", "rings" })
        {
            var images = (Image[])Field(hud, name);
            Assert.That(images.Length, Is.EqualTo(3));
            Assert.That(images.All(image => image != null), Is.True, name);
        }
        foreach (string name in new[] { "leftKeyLabel", "rightKeyLabel" })
            Assert.That((Object)Field(hud, name) != null, Is.True, "연결되지 않은 키 글자: " + name);
    }

    [Test]
    public void SeatHudIsDrawnWithSpritesNotCode()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var hud = Find<BarSeatStatusView>(scene);
        Assert.That(hud.GetComponentsInChildren<StoryTutorialShape>(true), Is.Empty, "좌석 표시는 코드로 그린 도형 대신 Image와 스프라이트를 쓴다.");
        foreach (var image in hud.GetComponentsInChildren<Image>(true))
        {
            Assert.That(image.sprite, Is.Not.Null, image.name + "에 스프라이트가 연결되어 있어야 합니다.");
            Assert.That(image.raycastTarget, Is.False, image.name + "은 클릭을 가로채면 안 됩니다.");
        }
        var frame = hud.Indicators.GetComponent<Image>();
        Assert.That(hud.Indicators.GetComponent<LayoutGroup>(), Is.Null, "프레임에는 Layout Group을 두지 않는다(테두리까지 한 칸으로 밀린다).");
        Assert.That(frame.type, Is.EqualTo(Image.Type.Sliced), "프레임은 9-slice로 늘린다.");
        Assert.That(frame.sprite.border, Is.Not.EqualTo(Vector4.zero), "프레임 스프라이트에 9-slice 테두리가 있어야 합니다.");
    }

    [Test]
    public void SeatsAreLaidOutByOneHorizontalLayoutGroup()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var hud = Find<BarSeatStatusView>(scene);
        var row = hud.Indicators.Find("Seats");
        Assert.That(row, Is.Not.Null, "SeatIndicators/Seats 줄이 있어야 합니다.");
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        Assert.That(layout, Is.Not.Null);
        Assert.That(layout.childAlignment, Is.EqualTo(TextAnchor.MiddleCenter));
        Assert.That(layout.childControlWidth || layout.childControlHeight, Is.False, "칸 크기(22×22)는 칸이 정한다.");
        Assert.That(layout.childForceExpandWidth || layout.childForceExpandHeight, Is.False);

        var dots = (Image[])Field(hud, "dots");
        var rings = (Image[])Field(hud, "rings");
        string[] seats = { "SeatLeft", "SeatMiddle", "SeatRight" };
        Assert.That(row.childCount, Is.EqualTo(3), "줄의 자식은 좌석 칸 3개뿐");
        for (int i = 0; i < 3; i++)
        {
            var cell = row.GetChild(i);
            Assert.That(cell.name, Is.EqualTo(seats[i]), "왼쪽·가운데·오른쪽 순서");
            Assert.That(rings[i].transform.parent, Is.SameAs(cell), seats[i] + " 칸 안에 링");
            Assert.That(dots[i].transform.parent, Is.SameAs(cell), seats[i] + " 칸 안에 점");
            Assert.That(rings[i].transform.GetSiblingIndex(), Is.LessThan(dots[i].transform.GetSiblingIndex()), "링 위에 점을 그린다");
        }

        // 칸 22 + 간격 2 → 중심 간격 24. 예전 수동 배치(-24, 0, 24)와 같은 자리.
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)row);
        float middle = ((RectTransform)row.GetChild(1)).localPosition.x;
        Assert.That(((RectTransform)row.GetChild(0)).localPosition.x - middle, Is.EqualTo(-24).Within(.01f));
        Assert.That(((RectTransform)row.GetChild(2)).localPosition.x - middle, Is.EqualTo(24).Within(.01f));
    }
}
