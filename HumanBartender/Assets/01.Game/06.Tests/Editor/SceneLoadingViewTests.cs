using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일반 씬 로딩 화면(엔진 미구현 리스트 · 바 출입·리소스 로딩 표시, 웹 campaign.js resource-loading·loading-screen.css).
/// 바 출입은 문 화면, 일반 로딩은 비둘기 + Loading, 집 현관·대화 이동·Day 전환 중에는 페이드만.
/// </summary>
public sealed class SceneLoadingViewTests
{
    const string ProjectScopePrefab = "Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab";
    const string PigeonSheet = "Assets/01.Game/03.Content/01.ArtResources/01.Characters/02.Outside/05.StreetNPC/01.Idle/Pigeon_idle.png";

    sealed class Marker : ISceneTransitionOverlay
    {
        public Cysharp.Threading.Tasks.UniTask ShowAsync(SceneTransitionStyle style, System.Threading.CancellationToken token) => default;
        public Cysharp.Threading.Tasks.UniTask FinishAsync(System.Threading.CancellationToken token) => default;
        public Cysharp.Threading.Tasks.UniTask HideAsync(System.Threading.CancellationToken token) => default;
        public void HideImmediate() { }
    }

    [Test]
    public void EachTransitionStyleUsesTheWebScreen()
    {
        var door = new Marker();
        var loading = new Marker();
        Assert.That(SceneTransitionManager.SelectOverlay(SceneTransitionStyle.BarEntrance, false, false, door, loading), Is.SameAs(door));
        Assert.That(SceneTransitionManager.SelectOverlay(SceneTransitionStyle.BarExit, false, false, door, loading), Is.SameAs(door));
        Assert.That(SceneTransitionManager.SelectOverlay(SceneTransitionStyle.Fade, false, false, door, loading), Is.SameAs(loading));
        Assert.That(SceneTransitionManager.SelectOverlay(SceneTransitionStyle.Fade, true, false, door, loading), Is.Null, "대화 이동·Day 전환은 그 연출이 화면을 덮는다.");
        Assert.That(SceneTransitionManager.SelectOverlay(SceneTransitionStyle.Fade, false, true, door, loading), Is.Null, "씬 내리기는 로딩이 아니다.");
        Assert.That(SceneTransitionManager.SelectOverlay(SceneTransitionStyle.HomeDoor, false, false, door, loading), Is.Null);
        Assert.That(SceneTransitionManager.SelectOverlay(SceneTransitionStyle.Fade, false, false, door, null), Is.Null);
    }

    [TestCase(0f, 0, 0)]
    [TestCase(.17f, 1, 1)]
    [TestCase(1.99f, 11, 4)]
    [TestCase(2.01f, 0, 0)]
    [TestCase(.45f, 2, 4)]
    public void PigeonAndDotsFollowTheWebTiming(float time, int frame, int dots)
    {
        // 웹: 2초 12단계(초당 6프레임), 점은 0.1초마다 '' '.' '..' '...' '....'.
        Assert.That(SceneLoadingView.FrameAt(time, 6f, 12), Is.EqualTo(frame));
        Assert.That(SceneLoadingView.DotsAt(time, .1f, 4), Is.EqualTo(dots));
    }

    [Test]
    public void ProjectScopeHasAWiredLoadingCanvasFollowingTheUiRules()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectScopePrefab);
        var manager = prefab.GetComponentInChildren<SceneTransitionManager>(true);
        using var managerData = new SerializedObject(manager);
        var view = (SceneLoadingView)managerData.FindProperty("loadingOverlay").objectReferenceValue;
        Assert.That(view, Is.Not.Null, "SceneTransitionManager의 Loading Overlay를 연결하세요.");
        Assert.That(view.name, Is.EqualTo("Canvas_Loading"));

        var canvas = view.GetComponent<Canvas>();
        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(canvas.sortingOrder, Is.EqualTo(31999), "바 문 전환(32000) 바로 아래.");
        var scaler = view.GetComponent<CanvasScaler>();
        Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(960, 540)));
        Assert.That(scaler.matchWidthOrHeight, Is.Zero);

        using var viewData = new SerializedObject(view);
        var root = (GameObject)viewData.FindProperty("loadingRoot").objectReferenceValue;
        Assert.That(root, Is.Not.Null);
        Assert.That(root.activeSelf, Is.False, "평소에는 LoadingRoot를 꺼 둔다.");
        foreach (var field in new[] { "rootGroup", "pigeonImage", "loadingLabel", "dotsLabel" })
            Assert.That(viewData.FindProperty(field).objectReferenceValue, Is.Not.Null, field);

        var expected = AssetDatabase.LoadAllAssetsAtPath(PigeonSheet).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        var frames = viewData.FindProperty("pigeonFrames");
        Assert.That(frames.arraySize, Is.EqualTo(12));
        for (int i = 0; i < frames.arraySize; i++)
            Assert.That(frames.GetArrayElementAtIndex(i).objectReferenceValue, Is.SameAs(expected[i]), "프레임 순서 " + i);

        foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
        {
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one), rect.name);
            Assert.That(rect.name.Contains(" "), Is.False, rect.name);
        }
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            Assert.That(text.raycastTarget, Is.False, text.name);
            Assert.That(text.enableAutoSizing, Is.False, text.name);
        }
        Assert.That(((Image)viewData.FindProperty("pigeonImage").objectReferenceValue).raycastTarget, Is.False);
    }
}
