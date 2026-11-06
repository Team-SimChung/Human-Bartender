using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>실제 Play 씬에 저장한 HUD 연결과 따르기 전환을 함께 검증한다.</summary>
public sealed class PourHudSceneTests
{
    const string PlayScene = "Assets/01.Game/01.Scenes/Play.unity";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Ref<T>(Object target, string field) where T : Object =>
        (T)new SerializedObject(target).FindProperty(field).objectReferenceValue;

    static void WithHud(Action<CraftGimmickHud> check)
    {
        var preview = EditorSceneManager.NewPreviewScene();
        var scene = EditorSceneManager.OpenScene(PlayScene, OpenSceneMode.Additive);
        GameObject clone = null;
        try
        {
            var source = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CraftGimmickHud>(true)).Single();
            clone = Object.Instantiate(source.gameObject);
            SceneManager.MoveGameObjectToScene(clone, preview);
            clone.SetActive(true);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
        try
        {
            var hud = clone.GetComponent<CraftGimmickHud>();
            typeof(CraftGimmickHud).GetMethod("Awake", Private).Invoke(hud, null);
            check(hud);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    sealed class PourDisplay : ICraftGimmickQuantityProgress, ICraftGimmickProgress, ICraftGimmickManualEnd,
        ICraftGimmickHudProvider
    {
        public string TargetValueText => "360 ml";
        public string CurrentValueText { get; set; } = "363.39 ml";
        public string IngredientName => "병맥주";
        public float TiltRatio => .72f;
        public float FlowStartRatio => 95f / 140f;
        public float FullFlowRatio => 110f / 140f;
        public string ProgressText => "363.39 / 360 ml";
        public bool ShowOkMark => false;
        public bool CanEndNow => true;
        public int Ends { get; private set; }
        public CraftGimmickHud GimmickHud { get; set; }
        public void EndNow() => Ends++;
    }

    sealed class OpeningDisplay : ICraftGimmickOwnHud
    {
        public ECraftHudPart OwnedParts => ECraftHudPart.Progress | ECraftHudPart.Subject | ECraftHudPart.NextButton;
    }

    static PourDisplay Bind(CraftGimmickHud hud, ECraftGimmick type = ECraftGimmick.Pour)
    {
        var timer = new CraftTimer(); timer.Start(); timer.Tick(41f);
        hud.BeginCraft(timer, 120f, false);
        var display = new PourDisplay();
        hud.BindGimmick(new GimmickStep(type, "beer", 360f, ENewUnit.Ml, true), display);
        return display;
    }

    static void WithPrefabHud(Action<CraftGimmickHud> check, bool initialize = true)
    {
        var root = PrefabUtility.LoadPrefabContents(PourHudPrefabSetup.PrefabPath);
        try
        {
            var manager = root.GetComponentInChildren<PourManager>(true);
            var hud = manager.GimmickHud;
            Assert.That(hud, Is.Not.Null, "The actual pour prefab must own its editable HUD.");
            if (initialize) typeof(CraftGimmickHud).GetMethod("Awake", Private).Invoke(hud, null);
            check(hud);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void PrefabModeShowsTheSavedNewHudAlongsideWorldArt()
    {
        WithPrefabHud(hud =>
        {
            Assert.That(hud.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            var manager = hud.transform.root.GetComponentInChildren<PourManager>(true);
            Assert.That(Vector3.Distance(hud.transform.position, manager.transform.position), Is.LessThan(.001f),
                "Editor HUD must share the physical Pour Root's frame, including its prefab offset.");
            foreach (string name in new[] { "root", "quantityRoot", "ingredientRoot", "tiltRoot", "timeRoot" })
                Assert.That(Ref<GameObject>(hud, name).activeInHierarchy, Is.True, name);
            Assert.That(Ref<TMP_Text>(hud, "targetValueText").text, Is.EqualTo("360 ml"));
            Assert.That(Ref<TMP_Text>(hud, "currentValueText").text, Is.EqualTo("0.00 ml"));
            Assert.That(Ref<Button>(hud, "nextButton").gameObject.activeInHierarchy, Is.True);
            foreach (var canvas in hud.transform.root.GetComponentsInChildren<Canvas>(true))
                if (canvas != hud.GetComponent<Canvas>())
                    Assert.That(canvas.gameObject.activeInHierarchy, Is.False, canvas.name);
        }, initialize: false);
    }

    [Test]
    public void EditorSetupPreservesAnAlreadyOpenPlayScene()
    {
        var scene = EditorSceneManager.OpenScene(PlayScene, OpenSceneMode.Additive);
        var root = PrefabUtility.LoadPrefabContents(PourHudPrefabSetup.PrefabPath);
        try
        {
            var marker = new GameObject("Unsaved Editor Test Change");
            SceneManager.MoveGameObjectToScene(marker, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            var manager = root.GetComponentInChildren<PourManager>(true);
            Object.DestroyImmediate(manager.GimmickHud.gameObject);
            PourHudPrefabSetup.Attach(root);
            Assert.That(manager.GimmickHud, Is.Not.Null);
            Assert.That(scene.isLoaded && scene.isDirty, Is.True);
            Assert.That(marker != null && marker.scene == scene, Is.True);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void SceneHudUsesPrefabHudForClockButtonAndTutorialWithoutDuplicateDisplay()
    {
        WithHud(common => WithPrefabHud(local =>
        {
            var timer = new CraftTimer(); timer.Start(); timer.Tick(41f);
            common.BeginCraft(timer, 120f, false);
            common.SetSortingOrder(1500);
            var quantity = Ref<GameObject>(local, "quantityRoot").GetComponent<RectTransform>();
            quantity.anchoredPosition += new Vector2(12f, -6f);
            var position = quantity.anchoredPosition;
            int count = local.GetComponentsInChildren<Transform>(true).Length;
            var display = new PourDisplay { GimmickHud = local };
            common.BindGimmick(new GimmickStep(ECraftGimmick.Pour, "beer", 360f, ENewUnit.Ml, true), display);
            Assert.That(Ref<GameObject>(common, "root").activeSelf, Is.False);
            Assert.That(Ref<GameObject>(local, "root").activeInHierarchy, Is.True);
            Assert.That(local.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(local.GetComponent<Canvas>().sortingOrder, Is.EqualTo(1500));
            Assert.That(common.ProgressTarget, Is.SameAs(quantity.transform));
            Assert.That(common.NextControl, Is.SameAs(Ref<Button>(local, "nextButton").transform));
            timer.Tick(2f);
            typeof(CraftGimmickHud).GetMethod("Refresh", Private).Invoke(local, null);
            Assert.That(Ref<TMP_Text>(local, "timeText").text, Is.EqualTo("00:43"));
            Ref<Button>(local, "nextButton").onClick.Invoke();
            Assert.That(display.Ends, Is.EqualTo(1));
            Assert.That(quantity.anchoredPosition, Is.EqualTo(position));
            Assert.That(local.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            common.UnbindGimmick();
            Assert.That(Ref<GameObject>(local, "root").activeSelf, Is.False);
            Assert.That(Ref<GameObject>(common, "root").activeSelf, Is.True);
            common.EndCraft();
        }));
    }

    [TestCase(ECraftGimmick.Pour)]
    [TestCase(ECraftGimmick.FillUp)]
    public void DedicatedHudUsesTheSameFillUpBindingAndHiddenClock(ECraftGimmick type)
    {
        WithHud(common => WithPrefabHud(local =>
        {
            common.BeginCraft(new CraftTimer(), 120f, true);
            var display = new PourDisplay { GimmickHud = local };
            common.BindGimmick(new GimmickStep(type, "beer", 360f, ENewUnit.Ml, true), display);
            Assert.That(Ref<TMP_Text>(local, "targetValueText").text, Is.EqualTo("360 ml"));
            Assert.That(Ref<TMP_Text>(local, "timeText").text, Is.EqualTo("??:??"));
            Assert.That(common.CurrentGimmick, Is.SameAs(display));
            common.BindGimmick(new GimmickStep(ECraftGimmick.Open, "beer"), new OpeningDisplay());
            Assert.That(Ref<GameObject>(local, "root").activeSelf, Is.False);
            Assert.That(Ref<GameObject>(common, "root").activeSelf, Is.True);
            common.EndCraft();
        }));
    }

    [TestCase(1280, 720)]
    [TestCase(1920, 1080)]
    [TestCase(1024, 768)]
    public void PrefabHudKeepsAuthoredLayoutAtRuntimeScreenSizes(int width, int height)
    {
        WithPrefabHud(hud => { Bind(hud); Capture(hud, width, height); });
    }

    [TestCase(ECraftGimmick.Pour)]
    [TestCase(ECraftGimmick.FillUp)]
    public void SavedPlayHudShowsAllQuantityPartsAndFinishes(ECraftGimmick type)
    {
        WithHud(hud =>
        {
            var display = Bind(hud, type);
            foreach (string name in new[] { "quantityRoot", "ingredientRoot", "tiltRoot", "timeRoot" })
                Assert.That(Ref<GameObject>(hud, name).activeInHierarchy, Is.True, name);
            Assert.That(Ref<TMP_Text>(hud, "targetValueText").text, Is.EqualTo("360 ml"));
            Assert.That(Ref<TMP_Text>(hud, "currentValueText").text, Is.EqualTo("363.39 ml"));
            Assert.That(Ref<TMP_Text>(hud, "subjectText").text, Is.EqualTo("병맥주"));
            Assert.That(Ref<TMP_Text>(hud, "timeText").text, Is.EqualTo("00:41"));
            Assert.That(Ref<TMP_Text>(hud, "progressText").gameObject.activeSelf, Is.False);
            Assert.That(Ref<RectTransform>(hud, "tiltMarker").anchorMin.x, Is.EqualTo(display.TiltRatio));
            Assert.That(Ref<RectTransform>(hud, "tiltFlowRange").anchorMin.x, Is.EqualTo(display.FlowStartRatio));
            var button = Ref<Button>(hud, "nextButton");
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True);
            Assert.That(Ref<TMP_Text>(hud, "nextButtonLabel").text, Is.EqualTo("따르기 마치기 →"));
            button.onClick.Invoke();
            Assert.That(display.Ends, Is.EqualTo(1));
        });
    }

    [Test]
    public void OpeningToPourRestoresSavedHudAndKeepsEditedPositions()
    {
        WithHud(hud =>
        {
            Bind(hud);
            var quantity = Ref<GameObject>(hud, "quantityRoot").GetComponent<RectTransform>();
            var timer = Ref<GameObject>(hud, "timeRoot").GetComponent<RectTransform>();
            quantity.anchoredPosition += new Vector2(10f, -5f);
            timer.anchoredPosition += new Vector2(-15f, -7f);
            var qp = quantity.anchoredPosition; var tp = timer.anchoredPosition;
            int count = hud.GetComponentsInChildren<Transform>(true).Length;
            hud.BindGimmick(new GimmickStep(ECraftGimmick.Open, "beer"), new OpeningDisplay());
            Assert.That(quantity.gameObject.activeSelf, Is.False);
            hud.UnbindGimmick();
            var display = Bind(hud);
            display.CurrentValueText = "???";
            typeof(CraftGimmickHud).GetMethod("Refresh", Private).Invoke(hud, null);
            Assert.That(Ref<TMP_Text>(hud, "currentValueText").text, Is.EqualTo("???"));
            Assert.That(quantity.gameObject.activeInHierarchy, Is.True);
            Assert.That(quantity.anchoredPosition, Is.EqualTo(qp));
            Assert.That(timer.anchoredPosition, Is.EqualTo(tp));
            Assert.That(hud.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
        });
    }

    [TestCase(1280, 720)]
    [TestCase(1920, 1080)]
    [TestCase(1024, 768)]
    public void SavedHudRemainsVisibleWithinRenderedScreen(int width, int height)
    {
        WithHud(hud => { Bind(hud); Capture(hud, width, height); });
    }

    public static void Export()
    {
        WithHud(hud => { Bind(hud); Capture(hud, 1280, 720); });
    }

    static void Capture(CraftGimmickHud hud, int width, int height)
    {
        var cameraGo = new GameObject("Pour HUD Verification Camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraGo, hud.gameObject.scene);
        var camera = cameraGo.GetComponent<Camera>();
        camera.scene = hud.gameObject.scene;
        camera.enabled = false;
        camera.orthographic = true; camera.orthographicSize = 3.5f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.025f, .035f, .055f);
        var canvas = hud.GetComponent<Canvas>();
        var render = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = render;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1f;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.enabled = false; scaler.enabled = true;
            foreach (var text in hud.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            foreach (string name in new[] { "quantityRoot", "ingredientRoot", "tiltRoot", "timeRoot" })
            {
                var rect = Ref<GameObject>(hud, name).GetComponent<RectTransform>();
                AssertVisible(rect, camera, name);
            }
            AssertVisible(Ref<Button>(hud, "nextButton").GetComponent<RectTransform>(), camera, "nextButton");
            RenderTexture.active = render;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            Directory.CreateDirectory("Logs/PourHudFix");
            bool dedicated = new SerializedObject(hud).FindProperty("useScreenSpaceDuringCraft").boolValue;
            string prefix = dedicated ? "pour-prefab-hud" : "pour-hud";
            File.WriteAllBytes($"Logs/PourHudFix/{prefix}-{width}x{height}.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(image); Object.DestroyImmediate(render); Object.DestroyImmediate(cameraGo);
        }
    }

    static void AssertVisible(RectTransform rect, Camera camera, string name)
    {
        Assert.That(rect.rect.width, Is.GreaterThan(0f), name);
        Assert.That(rect.rect.height, Is.GreaterThan(0f), name);
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            var viewport = camera.WorldToViewportPoint(corner);
            Assert.That(viewport.x, Is.InRange(-.001f, 1.001f), name);
            Assert.That(viewport.y, Is.InRange(-.001f, 1.001f), name);
        }
    }
}
