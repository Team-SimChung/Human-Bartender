using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// QA-49 코스터 공급함: 웹 serving-hud.css(.stack.service-popup 166×50, IMG_box_coaster, 글자 #99b6ce 12px)를
/// 960×540 캔버스(×0.75)로 옮긴 크기·위치·글자와, Play 씬이 프리팹 원본 값을 그대로 쓰는지 검사한다.
/// </summary>
public sealed class CoasterSupplyTrayLayoutTests
{
    const string PrefabPath = "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/03.Serving/CoasterSupplyTray.prefab";
    const string PlayPath = "Assets/01.Game/01.Scenes/Play.unity";

    static GameObject LoadTray() => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

    [Test]
    public void TrayUsesTheCoasterBoxArtAtWebProportions()
    {
        var tray = LoadTray();
        var root = (RectTransform)tray.transform;
        Assert.That(root.anchorMin, Is.EqualTo(new Vector2(1, 0)), "오른쪽 아래 HUD");
        Assert.That(root.pivot, Is.EqualTo(new Vector2(1, 0)));
        Assert.That(root.anchoredPosition, Is.EqualTo(new Vector2(-17, 15)));
        Assert.That(root.sizeDelta, Is.EqualTo(new Vector2(125, 38)));

        var background = tray.GetComponent<Image>();
        Assert.That(background.sprite.texture.name, Is.EqualTo("IMG_box_coaster"));
        Assert.That(background.preserveAspect, Is.False, "웹처럼 칸에 맞춰 늘려 채운다.");
        Assert.That(background.raycastTarget, Is.False);

        var label = tray.GetComponentInChildren<TextMeshProUGUI>(true);
        Assert.That(label.text, Is.EqualTo("코스터 드래그"));
        Assert.That((Color32)label.color, Is.EqualTo(new Color32(0x99, 0xB6, 0xCE, 0xFF)));
        Assert.That(label.fontSize, Is.EqualTo(9), "웹 12px × 0.75");
        Assert.That(label.horizontalAlignment, Is.EqualTo(HorizontalAlignmentOptions.Left));
        Assert.That(label.enableAutoSizing, Is.False);
        Assert.That(label.raycastTarget, Is.False);
        // 아이콘 오른쪽: 왼쪽 여백 9 + 아이콘 39 + 간격 7.5 → 칸 왼쪽에서 약 56에서 시작한다.
        Assert.That(label.rectTransform.anchoredPosition.x - label.rectTransform.sizeDelta.x / 2 + root.sizeDelta.x / 2,
            Is.EqualTo(55.5f).Within(1f));
    }

    [Test]
    public void CoastersSitOnTheLeftAndStayDraggable()
    {
        var tray = LoadTray();
        var coasters = tray.GetComponentsInChildren<CoasterDragItem>(true);
        Assert.That(coasters.Select(c => c.name), Is.EqualTo(new[] { "CoasterBottom", "CoasterMiddle", "CoasterTop" }));
        foreach (var coaster in coasters)
        {
            var rect = (RectTransform)coaster.transform;
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(40, 40)), "웹 52×28 contain × 0.75 → 폭 약 39");
            Assert.That(rect.anchoredPosition.x, Is.EqualTo(-34));
            var image = coaster.GetComponent<Image>();
            Assert.That(image.raycastTarget, Is.True, "공급함 전체가 드래그 판정이 되도록 런타임에 넓힌다.");
            Assert.That(image.preserveAspect, Is.True);
            var held = new SerializedObject(coaster).FindProperty("heldSize").vector2Value;
            Assert.That(held, Is.EqualTo(new Vector2(72, 72)), "드래그 중인 코스터는 아이콘보다 크게 보인다.");
        }

        var arrow = (RectTransform)tray.GetComponentsInChildren<Transform>(true).Single(t => t.name == "SupplyArrow");
        Assert.That(arrow.anchoredPosition, Is.EqualTo(new Vector2(-34, 6)), "화살표는 코스터 아이콘 바로 위");
        Assert.That(arrow.sizeDelta, Is.EqualTo(new Vector2(23, 27)));
    }

    [Test]
    public void TrayFollowsUnityUiRules()
    {
        foreach (var rect in LoadTray().GetComponentsInChildren<RectTransform>(true))
        {
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one), rect.name);
            Assert.That(rect.name, Does.Not.Contain(" "), "이름에 띄어쓰기를 쓰지 않는다.");
            Assert.That(rect.sizeDelta.x % 1, Is.Zero, rect.name);
            Assert.That(rect.sizeDelta.y % 1, Is.Zero, rect.name);
            Assert.That(rect.anchoredPosition.x % 1, Is.Zero, rect.name);
            Assert.That(rect.anchoredPosition.y % 1, Is.Zero, rect.name);
        }
    }

    [Test]
    public void PlaySceneUsesThePrefabLayout()
    {
        var scene = EditorSceneManager.OpenScene(PlayPath, OpenSceneMode.Additive);
        try
        {
            var tray = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Select(t => t.gameObject)
                .Single(g => PrefabUtility.IsAnyPrefabInstanceRoot(g) &&
                             AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(g)) == PrefabPath);
            var overridden = PrefabUtility.GetPropertyModifications(tray)
                .Where(m => m.propertyPath.StartsWith("m_SizeDelta") || m.propertyPath.StartsWith("m_AnchoredPosition") ||
                            m.propertyPath == "m_Sprite")
                .Select(m => $"{m.target.name}.{m.propertyPath}");
            Assert.That(overridden, Is.Empty, "배경·크기·위치는 프리팹 원본에서 고친다.");
            foreach (var coaster in tray.GetComponentsInChildren<CoasterDragItem>(true))
                Assert.That(new SerializedObject(coaster).FindProperty("rootCanvas").objectReferenceValue, Is.Not.Null);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
}
