using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class UIFrameLayoutTests
{
    const string Play = "Assets/01.Game/01.Scenes/Play.unity";
    static T Ref<T>(Object owner, string name) where T : Object =>
        (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;

    [Test]
    public void RealDialogueFramesKeepCornerAndTailAspectWhenTheBodyGrows()
    {
        var scene = EditorSceneManager.OpenScene(Play, OpenSceneMode.Additive);
        try
        {
            var view = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UIDialogueTextView>(true)).Single();
            foreach (var bubble in new[] { view.customerSpeechBubble, view.lunaSpeechBubble })
            {
                bubble.gameObject.SetActive(true);
                var effect = bubble.transform.Find("Frame").GetComponent<DialogueFrameMesh>();
                Assert.That(effect, Is.Not.Null);
                var image = effect.GetComponent<Image>();
                Vector2? previousCorner = null; float? previousTail = null;
                foreach (var size in new[] { bubble.minSize, bubble.maxSize })
                {
                    bubble.bubble.sizeDelta = size; Canvas.ForceUpdateCanvases();
                    using (var mesh = new VertexHelper())
                    {
                        effect.ModifyMesh(mesh);
                        var p = new UIVertex[36];
                        for (int i = 0; i < p.Length; i++) mesh.PopulateUIVertex(ref p[i], i);
                        Vector2 corner = new Vector2(p[1].position.x - p[0].position.x, p[12].position.y - p[6].position.y);
                        float tail = p[3].position.x - p[2].position.x;
                        float cornerWidthInSource = (p[1].uv0.x - p[0].uv0.x) * image.sprite.texture.width;
                        float cornerHeightInSource = (p[12].uv0.y - p[6].uv0.y) * image.sprite.texture.height;
                        Assert.That(corner.x / cornerWidthInSource, Is.EqualTo(corner.y / cornerHeightInSource).Within(.0005f), "rounded corners must not become ellipses");
                        if (previousCorner.HasValue) Assert.That(Vector2.Distance(corner, previousCorner.Value), Is.LessThan(.001f));
                        if (previousTail.HasValue) Assert.That(tail, Is.EqualTo(previousTail.Value).Within(.001f));
                        previousCorner = corner; previousTail = tail;
                        float widthInSource = (p[3].uv0.x - p[2].uv0.x) * image.sprite.texture.width;
                        float cornerInSource = (p[12].uv0.y - p[6].uv0.y) * image.sprite.texture.height;
                        Assert.That(tail / widthInSource, Is.EqualTo(corner.y / cornerInSource).Within(.002f), "tail and corner need the same source-pixel scale");
                    }
                }
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void ActualChoiceInstancesUseCurrentFontAndCenterTheVisibleText()
    {
        var scene = EditorSceneManager.OpenScene(Play, OpenSceneMode.Additive);
        try
        {
            var choices = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ChoicePanel>(true)).ToArray();
            Assert.That(choices.Length, Is.EqualTo(4));
            foreach (var choice in choices)
            {
                for (Transform parent = choice.transform.parent; parent != null; parent = parent.parent) parent.gameObject.SetActive(true);
                choice.SetActive(true);
                var label = Ref<TextMeshProUGUI>(choice, "panelTMPText");
                Assert.That(label.font.name, Is.EqualTo("Regular_PFStardust_3_0_SDF"));
                Assert.That(label.fontSharedMaterial, Is.EqualTo(label.font.material));
                foreach (string text in new[] { "다시 한 번 알려주실 수 있나요?", "네. (튜토리얼 대사 스킵)", "두 줄로 표시되는 선택지도\n가운데에 맞춥니다." })
                {
                    choice.SetPanelText(text); label.ForceMeshUpdate(true, true);
                    var center = choice.transform.InverseTransformPoint(label.transform.TransformPoint(label.textBounds.center));
                    Assert.That(center.y, Is.EqualTo(((RectTransform)choice.transform).rect.center.y).Within(.5f), text);
                }
                var frame = choice.GetComponent<ChoiceFrameMesh>();
                Assert.That(frame, Is.Not.Null);
                var image = choice.GetComponent<Image>(); var button = choice.GetButton();
                var previous = image.overrideSprite;
                try
                {
                    foreach (var sprite in new[] { image.sprite, button.spriteState.highlightedSprite })
                    {
                        TestContext.WriteLine(sprite.name + " imported rect=" + sprite.rect + " texture=" + sprite.texture.width + "x" + sprite.texture.height);
                        image.overrideSprite = sprite;
                        using (var mesh = new VertexHelper())
                        {
                            frame.ModifyMesh(mesh); var a = new UIVertex(); var b = new UIVertex();
                            mesh.PopulateUIVertex(ref a, 0); mesh.PopulateUIVertex(ref b, 2);
                            Rect source = sprite.rect; Rect target = image.rectTransform.rect;
                            var padding = UnityEngine.Sprites.DataUtility.GetPadding(sprite);
                            float scaleY = sprite.texture.height / 228f;
                            float top = Mathf.LerpUnclamped(a.position.y, b.position.y, (194 * scaleY - source.yMin - padding.y) / (source.height - padding.y - padding.w));
                            float bottom = Mathf.LerpUnclamped(a.position.y, b.position.y, (38 * scaleY - source.yMin - padding.y) / (source.height - padding.y - padding.w));
                            Assert.That(top, Is.EqualTo(target.yMax).Within(.01f));
                            Assert.That(bottom, Is.EqualTo(target.yMin).Within(.01f));
                        }
                    }
                }
                finally { image.overrideSprite = previous; }
            }
            for (int i = 0; i < choices.Length; i++)
            {
                choices[i].SetActive(i < 2);
                choices[i].SetPanelText(i == 0 ? "다시 한 번 알려주실 수 있나요?" : "네. (튜토리얼 대사 스킵)");
                Ref<GameObject>(choices[i].GetButton(), "holdProgressRoot").SetActive(false);
            }
            choices[0].GetComponent<Image>().overrideSprite = choices[0].GetButton().spriteState.highlightedSprite;
            foreach (var choice in choices)
                typeof(UIChoiceGlitch).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(choice.GetComponent<UIChoiceGlitch>(), null);
            choices[0].GetComponent<UIChoiceGlitch>().OnPointerEnter(null);
            Assert.That((Color32)Ref<TextMeshProUGUI>(choices[0], "panelTMPText").color, Is.EqualTo(new Color32(84, 223, 234, 255)));
            Capture(scene, "choices-centered");
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void TutorialDragArrowEndsInsideTheDestinationEvenAfterMovementAndRotation()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/01.Game/03.Content/08.Prefabs/03.UI/04.Tutorial/StoryTutorialOverlay.prefab");
        var view = StoryTutorialView.Create(prefab.GetComponent<StoryTutorialView>(), null);
        var supply = new GameObject("test supply", typeof(RectTransform)).GetComponent<RectTransform>();
        var target = new GameObject("test tabletop", typeof(RectTransform)).GetComponent<RectTransform>();
        try
        {
            foreach (var rect in new[] { supply, target })
            { rect.SetParent(view.transform, false); rect.sizeDelta = new Vector2(140, 80); }
            supply.anchoredPosition = new Vector2(430, -230);
            foreach (var position in new[] { new Vector2(-320, -100), new Vector2(280, 170) })
            {
                target.anchoredPosition = position; target.localRotation = Quaternion.Euler(0, 0, 25);
                Canvas.ForceUpdateCanvases();
                view.ShowGuide("손님 맞이하기", "코스터를 크리스 앞 책상 위로 드래그하세요.", supply, target, true);
                Assert.That(view.Guide.HasDragRoute, Is.True);
                var to = (Vector2?)typeof(StoryTutorialGuideGraphic).GetField("arrowTo", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view.Guide);
                var point = view.transform.InverseTransformPoint(target.TransformPoint(target.rect.center));
                Assert.That(Vector2.Distance(to.Value, point), Is.LessThan(.1f), "arrow tip must land on the actual highlighted tabletop");
            }
            var instruction = view.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Instruction Text");
            Assert.That(instruction.font.name, Is.EqualTo("Medium_Paperlogy_SDF"));
        }
        finally { Object.DestroyImmediate(view.gameObject); }
    }

    [Test]
    public void AuthoredGameUiHasNoRemainingLegacyPixelFontReferences()
    {
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/01.Game/03.Content/08.Prefabs" })
            .Concat(AssetDatabase.FindAssets("t:Scene", new[] { "Assets/01.Game/01.Scenes" })).Select(AssetDatabase.GUIDToAssetPath);
        foreach (string path in paths)
        {
            string text = System.IO.File.ReadAllText(path);
            foreach (string guid in new[] { "3bc0c61a83ff6c741bda5b2a6e98c7a8", "8110ac63e05f74abeb474eab5197c9f7", "689f1e1350c8b734b9decf88ffe8ab4b", "26c338e070b15644ebb3e8f6be88df6c" })
                Assert.That(text.Contains("guid: " + guid), Is.False, path + " retains a legacy font or material: " + guid);
        }
    }

    static void Capture(UnityEngine.SceneManagement.Scene scene, string name) =>
        typeof(DialoguePresentationRegressionTests).GetMethod("SavePreview", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { scene, name, Vector3.zero });
}
