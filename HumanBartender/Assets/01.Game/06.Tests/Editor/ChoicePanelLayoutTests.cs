using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// QA-48 바 내부 선택지: 웹 bar-views.css(.choices 675×51, 간격 6, 아래 94, 글자 19px)를
/// 960×540 캔버스로 옮긴 크기·여백·위치와, 그림이 원본 비율로 그려지는지를 검사한다.
/// </summary>
public sealed class ChoicePanelLayoutTests
{
    const string PrefabPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/02.Dialogue/ChoicePanel.prefab";
    const string PlayPath = "Assets/01.Game/01.Scenes/Play.unity";
    const string ArtA = "Assets/01.Game/03.Content/01.ArtResources/05.UI/01.Shared/01.MainScreen/IMG_choice_A.png";

    [Test]
    public void ChoiceRowUsesTheWebProportionsOnTheBarCanvas()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var root = (RectTransform)prefab.transform;
        Assert.That(root.sizeDelta, Is.EqualTo(new Vector2(506, 38)));
        // 그림 테두리(2028×156)와 같은 비율이어야 위아래 선이 늘어나 뭉개지지 않는다.
        float art = 2028f / 156f, row = root.sizeDelta.x / root.sizeDelta.y;
        Assert.That(row, Is.EqualTo(art).Within(art * .03f));

        var label = prefab.GetComponentInChildren<TMP_Text>(true);
        Assert.That(label.fontSize, Is.EqualTo(14), "웹 19px(1280) → 14(960)");
        Assert.That(label.rectTransform.offsetMin, Is.EqualTo(new Vector2(45, 9)));
        Assert.That(label.rectTransform.offsetMax, Is.EqualTo(new Vector2(-45, -9)));

        var arrow = (RectTransform)prefab.transform.Find("FocusArrow");
        Assert.That(arrow.anchoredPosition, Is.EqualTo(new Vector2(13, 0)));
        Assert.That(arrow.sizeDelta, Is.EqualTo(new Vector2(25, 27)));

        var hold = (RectTransform)prefab.transform.Find("HoldProgress");
        Assert.That(hold, Is.Not.Null);
        // 웹 .choice-progress: 왼쪽 6%, 오른쪽 5%, 아래 2.56%, 두께 2px.
        Assert.That(hold.offsetMin, Is.EqualTo(new Vector2(30, 1)));
        Assert.That(hold.offsetMax, Is.EqualTo(new Vector2(-26, 3)));

        var importer = (TextureImporter)AssetImporter.GetAtPath(ArtA);
        Assert.That(importer.maxTextureSize, Is.GreaterThanOrEqualTo(4096), "2103px 원본이 줄어들어 들어오지 않게 한다.");
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), "선택 상태 그림(B)과 같은 필터");
        Assert.That(hold.Find("BlueFill"), Is.Not.Null, "이름에 띄어쓰기를 쓰지 않는다.");
    }

    [Test]
    public void TwoChoicesSitLowAboveTheCounterLikeTheWeb()
    {
        var scene = EditorSceneManager.OpenScene(PlayPath, OpenSceneMode.Additive);
        try
        {
            var view = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UIDialogueChoiceView>(true)).Single();
            var list = new SerializedObject(view).FindProperty("choicePanels");
            var rows = Enumerable.Range(0, list.arraySize)
                .Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue as Component)
                .Select(c => c.GetComponent<ChoicePanel>()).ToArray();
            Assert.That(rows.Length, Is.EqualTo(4));
            var layout = rows[0].transform.parent.GetComponent<VerticalLayoutGroup>();
            Assert.That(layout.childAlignment, Is.EqualTo(TextAnchor.LowerCenter), "선택지 수가 늘면 위로 쌓인다(웹 bottom 고정).");
            Assert.That(layout.padding.bottom, Is.EqualTo(64), "웹 화면의 마지막 선택지 아래 끝(약 86px, 1280) → 64(960)");
            Assert.That(layout.spacing, Is.EqualTo(5));
            Assert.That(((RectTransform)layout.transform).anchoredPosition, Is.EqualTo(Vector2.zero));
            foreach (var row in rows)
            {
                var rect = (RectTransform)row.transform;
                Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(506, 38)), row.name);
                Assert.That(rect.localScale, Is.EqualTo(Vector3.one), row.name);
            }

            var canvas = layout.GetComponentInParent<Canvas>(true);
            var container = (RectTransform)layout.transform.parent;
            container.gameObject.SetActive(true);
            for (int i = 0; i < rows.Length; i++) rows[i].gameObject.SetActive(i < 2);
            rows[0].GetComponentInChildren<TMP_Text>(true).text = "기억이 잘 안 나요.";
            rows[1].GetComponentInChildren<TMP_Text>(true).text = "다 기억하고 있어요. (튜토리얼 스킵)";
            var panel = (RectTransform)layout.transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            // 두 번째(마지막) 선택지의 아래 끝이 묶음(화면 전체) 아래에서 64 위.
            var corners = new Vector3[4];
            ((RectTransform)rows[1].transform).GetWorldCorners(corners);
            float bottom = panel.InverseTransformPoint(corners[0]).y - panel.rect.yMin;
            Assert.That(bottom, Is.EqualTo(64).Within(1));
            Capture(canvas, "bar-choices", 1920, 1080);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    static void Capture(Canvas canvas, string name, int width, int height)
    {
        string directory = Environment.GetEnvironmentVariable("HUMAN_BARTENDER_PREVIEW_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        var cameraGO = new GameObject("Choice capture camera", typeof(Camera));
        var camera = cameraGO.GetComponent<Camera>();
        camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.35f, .23f, .15f); camera.enabled = false; camera.scene = canvas.gameObject.scene;
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
        var render = new RenderTexture(width, height, 24);
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        var active = RenderTexture.active;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.targetTexture = render;
            Canvas.ForceUpdateCanvases();
            foreach (var label in canvas.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate(true);
            camera.Render(); RenderTexture.active = render;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; RenderTexture.active = active;
            Object.DestroyImmediate(texture); Object.DestroyImmediate(render); Object.DestroyImmediate(cameraGO);
        }
    }
}
