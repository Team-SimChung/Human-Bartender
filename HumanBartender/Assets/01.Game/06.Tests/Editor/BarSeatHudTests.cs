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

    [Test]
    public void SeatHudSceneReferencesAreWired()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var hud = Find<BarSeatStatusView>(scene);
        foreach (string name in new[] { "phases", "characters", "guests", "craft", "preparation", "recipes", "playCamera", "servicePanel" })
        {
            var value = (Object)typeof(BarSeatStatusView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
            Assert.That(value != null, Is.True, "연결되지 않은 참조: " + name);
        }
        foreach (string name in new[] { "dots", "rings" })
        {
            var shapes = (StoryTutorialShape[])typeof(BarSeatStatusView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
            Assert.That(shapes.Length, Is.EqualTo(3));
            Assert.That(shapes.All(shape => shape != null), Is.True, name);
        }
    }
}
