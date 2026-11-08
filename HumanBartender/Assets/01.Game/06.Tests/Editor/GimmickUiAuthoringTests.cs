using System;
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

public sealed class GimmickUiAuthoringTests
{
    const string Folder = "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames/";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void PourBackgroundStaysOutsideOverlayHudAndBehindBottleGlassAndLiquid()
    {
        WithPrefab("03.Pour/NewPourGame.prefab", root =>
        {
            var background = root.GetComponentsInChildren<MinigameBackground>(true).Single();
            var renderer = background.GetComponent<SpriteRenderer>();
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(background.GetComponentInParent<Canvas>(), Is.Null,
                "HUD가 Overlay로 전환돼도 배경이 병·잔 위에 그려지면 안 됩니다.");
            foreach (var other in root.GetComponentsInChildren<SpriteRenderer>(true).Where(value => value != renderer))
            {
                Assert.That(renderer.sortingLayerID, Is.EqualTo(other.sortingLayerID));
                Assert.That(renderer.sortingOrder, Is.LessThan(other.sortingOrder), other.name);
            }
            var liquid = root.GetComponentInChildren<SphLiquidRenderer>(true);
            using var data = new SerializedObject(liquid);
            Assert.That(renderer.sortingOrder, Is.LessThan(data.FindProperty("sortingOrder").intValue));
            var originalImage = background.GetComponent<Image>();
            Assert.That(originalImage.enabled, Is.False);
            Assert.That(originalImage.raycastTarget, Is.False);
            int count = root.GetComponentsInChildren<Transform>(true).Length;
            var position = background.transform.localPosition;
            PourHudPrefabSetup.MoveBackgroundBehindGimmick(root);
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            Assert.That(background.transform.localPosition, Is.EqualTo(position));
        });
    }

    static T Ref<T>(Object owner, string name) where T : Object =>
        (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;

    static void WithPrefab(string path, Action<GameObject> check)
    {
        var root = PrefabUtility.LoadPrefabContents(Folder + path);
        try { check(root); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase("04.Cap/NewCapGame.prefab", "Cap")]
    [TestCase("02.Stir/NewStirGame.prefab", "Stir")]
    [TestCase("03.Pour/NewPourGame.prefab", "Pour")]
    [TestCase("01.Shake/NewShakingMinigame.prefab", "Shake")]
    public void ChangedPrefabsLoadWithoutMissingScriptsAndRequiredBindings(string path, string kind)
    {
        WithPrefab(path, root =>
        {
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject), Is.Zero, node.name);
            Object owner = kind == "Pour" ? root.GetComponentInChildren<PourManager>(true)
                : kind == "Cap" ? (Object)root.GetComponentInChildren<CapOpenView>(true)
                : kind == "Stir" ? root.GetComponentInChildren<StirManager>(true)
                : root.GetComponentInChildren<ShakingManagerNew>(true);
            string[] names = kind == "Pour" ? new[] { "gimmickHud", "targetHeightMarker", "ingredientBottle", "bottle", "glassCenter", "liquidProfile", "liquidRenderer", "balanceData" }
                : kind == "Cap" ? new[] { "halo", "body", "carbonationLayer", "mouth", "cap", "perfectBurst", "opener", "overlay", "powerGauge", "rhythmRings", "haloDefault", "haloSuccess", "phaseLabel", "feedback", "timingHint", "gaugeLabel", "hitLabel", "finishLabel", "hitButton", "finishButton", "hitGroup", "finishGroup", "hitKey", "lessonRoot", "lessonShade", "lessonText", "lessonArrow", "powerFocus", "rhythmFocus" }
                : kind == "Shake" ? new[] { "attemptGauge", "shakeLineCreator" } : Array.Empty<string>();
            foreach (string name in names) Assert.That(Ref<Object>(owner, name), Is.Not.Null, kind + "." + name);
        });
    }

    [Test]
    public void MainShakeGaugeCoversAllTwentyConfiguredAttemptsWithoutCreatingUi()
    {
        WithPrefab("01.Shake/NewShakingMinigame.prefab", root =>
        {
            var manager = root.GetComponentInChildren<ShakingManagerNew>(true);
            var gauge = Ref<StirGaugeView>(manager, "attemptGauge");
            var balance = ScriptableObject.CreateInstance<NewBalanceDataSO>();
            try
            {
                balance.balanceData = new NewBalanceDataBase { Config = new NewBalanceConfig { ShakeTargetStacks = 20 } };
                var data = new SerializedObject(manager);
                data.FindProperty("balanceData").objectReferenceValue = balance;
                data.ApplyModifiedPropertiesWithoutUndo();
                var saved = new SerializedObject(gauge);
                var slots = saved.FindProperty("segments");
                Assert.That(slots.arraySize, Is.GreaterThanOrEqualTo(20));
                var first = (Image)slots.GetArrayElementAtIndex(0).objectReferenceValue;
                first.rectTransform.anchoredPosition += new Vector2(5f, 3f);
                var position = first.rectTransform.anchoredPosition;
                int count = root.GetComponentsInChildren<Transform>(true).Length;
                typeof(ShakingManagerNew).GetMethod("ResetJudgement", Private).Invoke(manager, null);
                var last = (Image)slots.GetArrayElementAtIndex(19).objectReferenceValue;
                Assert.That(last.gameObject.activeSelf, Is.True);
                gauge.SetResult(19, true);
                gauge.SetNextSegment(19);
                Assert.That(last.color.a, Is.EqualTo(1f));
                Assert.That(((StirGaugeOutline)saved.FindProperty("nextOutlines").GetArrayElementAtIndex(19).objectReferenceValue).gameObject.activeSelf, Is.True);
                gauge.Configure(15);
                Assert.That(last.gameObject.activeSelf, Is.False);
                Assert.That(first.rectTransform.anchoredPosition, Is.EqualTo(position));
                Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
            finally { Object.DestroyImmediate(balance); }
        });
    }

    [TestCase(ELanguage.Ko, "다음 →")]
    [TestCase(ELanguage.En, "Next →")]
    public void CapFinishLabelUsesTheCurrentLanguage(ELanguage language, string expected)
    {
        var state = GameStateManager.Instance;
        var previous = state.Language;
        try
        {
            state.Language = language;
            WithPrefab("04.Cap/NewCapGame.prefab", root =>
            {
                var view = root.GetComponentInChildren<CapOpenView>(true);
                view.Render(new CapOpenRules(), "beer", 165f, 44f, false);
                Assert.That(Ref<TMP_Text>(view, "finishLabel").text, Is.EqualTo(expected));
            });
        }
        finally { state.Language = previous; }
    }

    [Test]
    public void EditorSetupKeepsAnotherDirtySceneOpenAndActive()
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var marker = new GameObject("Unsaved authoring change");
            EditorSceneManager.MarkSceneDirty(scene);
            GimmickUiPrefabSetup.UpdateStirScene("Assets/02.Dev/01.Scenes/01.Minigames/Stir.unity");
            Assert.That(scene.isLoaded && scene.isDirty, Is.True);
            Assert.That(marker != null && marker.scene == scene, Is.True);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    [Test]
    public void EditorSetupDoesNotSaveOrCloseAnAlreadyOpenStirScene()
    {
        const string path = "Assets/02.Dev/01.Scenes/01.Minigames/Stir.unity";
        string saved = System.IO.File.ReadAllText(path);
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var marker = new GameObject("Unsaved stir change");
            SceneManager.MoveGameObjectToScene(marker, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            GimmickUiPrefabSetup.UpdateStirScene(path);
            Assert.That(scene.isLoaded && scene.isDirty, Is.True);
            Assert.That(marker != null && marker.scene == scene, Is.True);
            Assert.That(System.IO.File.ReadAllText(path), Is.EqualTo(saved));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void RepeatedPourSetupKeepsAuthoredBottleVisibility()
    {
        WithPrefab("03.Pour/NewPourGame.prefab", root =>
        {
            var manager = root.GetComponentInChildren<PourManager>(true);
            var bottle = Ref<SpriteRenderer>(manager, "ingredientBottle");
            bottle.gameObject.SetActive(false);
            typeof(GimmickUiPrefabSetup).GetMethod("BakePour", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { root });
            Assert.That(bottle.gameObject.activeSelf, Is.False);
        });
    }

    [TestCase("04.Cap/NewCapGame.prefab", "Cap")]
    [TestCase("02.Stir/NewStirGame.prefab", "Stir")]
    [TestCase("03.Pour/NewPourGame.prefab", "Pour")]
    [TestCase("01.Shake/NewShakingMinigame.prefab", "Shake")]
    public void RepeatedEditorSetupPreservesEditedLayout(string path, string kind)
    {
        WithPrefab(path, root =>
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            var positions = new Vector3[transforms.Length];
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] == root.transform) continue;
                transforms[i].localPosition += new Vector3(3f, -2f, 0f);
                positions[i] = transforms[i].localPosition;
            }
            if (kind == "Cap") new CapUiAuthoring(root.GetComponentInChildren<CapOpenView>(true)).Build();
            else typeof(GimmickUiPrefabSetup).GetMethod("Bake" + kind, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { root });
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(transforms.Length));
            for (int i = 0; i < transforms.Length; i++)
                if (transforms[i] != root.transform)
                    Assert.That(transforms[i].localPosition, Is.EqualTo(positions[i]), transforms[i].name);
        });
    }

    [Test]
    public void CapChangesStatesWithoutCreatingUiOrResettingEditedRects()
    {
        WithPrefab("04.Cap/NewCapGame.prefab", root =>
        {
            var view = root.GetComponentInChildren<CapOpenView>(true);
            foreach (var name in new[] { "hitButton", "finishButton", "powerGauge", "rhythmRings", "lessonShade" })
                Assert.That(Ref<Object>(view, name), Is.Not.Null, name);
            foreach (var name in new[] { "hitButton", "finishButton" })
            {
                var rect = Ref<Button>(view, name).GetComponent<RectTransform>();
                Assert.That(rect.rect.width, Is.GreaterThan(70f));
                Assert.That(rect.rect.height, Is.GreaterThan(25f));
            }
            var feedback = Ref<TMP_Text>(view, "feedback");
            var gauge = Ref<CapCanvasLayer>(view, "powerGauge").rectTransform;
            feedback.rectTransform.anchoredPosition += new Vector2(33f, 27f);
            feedback.fontSize = 37f;
            gauge.anchoredPosition += new Vector2(-20f, 30f);
            gauge.sizeDelta = new Vector2(72f, 310f);
            var labelPosition = feedback.rectTransform.anchoredPosition;
            var gaugePosition = gauge.anchoredPosition;
            int count = root.GetComponentsInChildren<Transform>(true).Length;
            var rules = new CapOpenRules();
            rules.Press();
            view.SetLesson("test", true);
            view.Render(rules, "beer", 165f, 44f, false);
            rules.Tick(.2f); rules.Tick(.2f); rules.Tick(.2f); rules.Tick(.11f);
            rules.Press();
            rules.Tick(.2f); rules.Tick(.2f); rules.Tick(.2f);
            view.SetLesson("test", false);
            view.Render(rules, "beer", 165f, 44f, false);
            rules.Tick(.2f); rules.Press();
            view.Render(rules, "beer", 165f, 44f, true);
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            Assert.That(feedback.rectTransform.anchoredPosition, Is.EqualTo(labelPosition));
            Assert.That(feedback.fontSize, Is.EqualTo(37f));
            Assert.That(gauge.anchoredPosition, Is.EqualTo(gaugePosition));
            Assert.That(gauge.sizeDelta, Is.EqualTo(new Vector2(72f, 310f)));
            Assert.That(root.GetComponentsInChildren<LayoutGroup>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<ContentSizeFitter>(true), Is.Empty);
        });
    }

    [Test]
    public void CapEditorPreviewLeavesLessonVisibleForManualEditing()
    {
        WithPrefab("04.Cap/NewCapGame.prefab", root =>
        {
            var view = root.GetComponentInChildren<CapOpenView>(true);
            var lesson = Ref<GameObject>(view, "lessonRoot");
            var arrow = Ref<RectTransform>(view, "lessonArrow");
            lesson.SetActive(true);
            arrow.anchoredPosition = new Vector2(25f, 16f);
            foreach (bool timing in new[] { false, true })
            {
                var data = new SerializedObject(view);
                data.FindProperty("previewTiming").boolValue = timing;
                data.ApplyModifiedPropertiesWithoutUndo();
                typeof(CapOpenView).GetMethod("Preview", Private).Invoke(view, null);
                Assert.That(lesson.activeSelf, Is.True);
                Assert.That(arrow.anchoredPosition, Is.EqualTo(new Vector2(25f, 16f)));
            }
        });
    }

    [TestCase("02.Stir/NewStirGame.prefab")]
    [TestCase("01.Shake/NewShakingMinigame.prefab")]
    public void ProgressGaugeUsesAuthoredSegmentsAndOutlines(string path)
    {
        WithPrefab(path, root =>
        {
            foreach (var gauge in root.GetComponentsInChildren<StirGaugeView>(true))
            {
                var data = new SerializedObject(gauge);
                var segments = data.FindProperty("segments");
                Assert.That(segments.arraySize, Is.GreaterThan(0));
                Assert.That(data.FindProperty("nextOutlines").arraySize, Is.EqualTo(segments.arraySize));
                var segment = (Image)segments.GetArrayElementAtIndex(0).objectReferenceValue;
                segment.rectTransform.anchoredPosition += new Vector2(17f, -9f);
                var position = segment.rectTransform.anchoredPosition;
                int count = root.GetComponentsInChildren<Transform>(true).Length;
                gauge.Configure(segments.arraySize);
                for (int i = 0; i < segments.arraySize; i++)
                {
                    gauge.SetNextSegment(i);
                    gauge.SetResult(i, i % 2 == 0);
                }
                gauge.ResetAll();
                Assert.That(segment.rectTransform.anchoredPosition, Is.EqualTo(position));
                Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
        });
    }

    [Test]
    public void StirBadgeStateKeepsEditedSize()
    {
        WithPrefab("02.Stir/NewStirGame.prefab", root =>
        {
            foreach (var badge in root.GetComponentsInChildren<StirKeyBadge>(true))
            {
                var rect = Ref<Image>(badge, "frame").rectTransform;
                rect.sizeDelta = new Vector2(88f, 72f);
                foreach (StirKeyBadge.EState state in Enum.GetValues(typeof(StirKeyBadge.EState))) badge.SetState(state);
                Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(88f, 72f)));
            }
        });
    }

    [Test]
    public void CommonHudKeepsTimerAndPanelPlacementWhenCraftBegins()
    {
        var root = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CraftGimmickHud));
        try
        {
            var hud = root.GetComponent<CraftGimmickHud>();
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            var timer = new GameObject("Timer", typeof(RectTransform));
            timer.transform.SetParent(panel.transform, false);
            var rect = (RectTransform)timer.transform;
            rect.anchoredPosition = new Vector2(-123f, -89f);
            rect.localScale = Vector3.one * .73f;
            var data = new SerializedObject(hud);
            data.FindProperty("root").objectReferenceValue = panel;
            data.FindProperty("timeRoot").objectReferenceValue = timer;
            data.ApplyModifiedPropertiesWithoutUndo();
            hud.BeginCraft(null, 0, false);
            typeof(CraftGimmickHud).GetMethod("Refresh", Private).Invoke(hud, null);
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(-123f, -89f)));
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one * .73f));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void PourGuideHasEditableChildrenAndDoesNotGenerateOnSetup()
    {
        WithPrefab("03.Pour/NewPourGame.prefab", root =>
        {
            var manager = root.GetComponentInChildren<PourManager>(true);
            var guide = Ref<Transform>(manager, "targetHeightMarker");
            Assert.That(guide, Is.Not.Null);
            var layout = guide.Find("Marker Layout");
            Assert.That(layout.Find("Arrow"), Is.Not.Null);
            Assert.That(layout.Find("Guide Dashes").childCount, Is.GreaterThan(0));
            layout.localPosition = new Vector3(.1f, .08f, 0f);
            int count = root.GetComponentsInChildren<Transform>(true).Length;
            typeof(PourManager).GetMethod("CreateTargetHeightMarker", Private).Invoke(manager, null);
            Assert.That(layout.localPosition, Is.EqualTo(new Vector3(.1f, .08f, 0f)));
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            Assert.That(Ref<SpriteRenderer>(manager, "ingredientBottle"), Is.Not.Null);
        });
    }
}
