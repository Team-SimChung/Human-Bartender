using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>본편 선택지의 ▷ 강조 화살표 (웹 bar-views.css .choices button:after 기준, 슬라이드 없음).</summary>
public sealed class ChoiceFocusArrowTests
{
    const string ChoicePrefabPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/02.Dialogue/ChoicePanel.prefab";
    const string ArrowSpritePath = "Assets/01.Game/03.Content/01.ArtResources/05.UI/01.Shared/01.MainScreen/IMG_choice_arrow.png";

    static void Call(object owner, string method) =>
        owner.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, null);

    static GameObject Arrow(GameObject choice) =>
        (GameObject)new SerializedObject(choice.GetComponent<UIChoiceGlitch>()).FindProperty("focusDecoration").objectReferenceValue;

    [Test]
    public void ChoicePanelHasArrowWiredAsFocusDecoration()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChoicePrefabPath);
        var arrowObject = Arrow(prefab);
        Assert.That(arrowObject, Is.Not.Null, "UIChoiceGlitch의 Focus Decoration에 ▷ 화살표가 연결되어야 합니다.");
        var arrow = (RectTransform)arrowObject.transform;
        Assert.That(arrow.parent, Is.EqualTo(prefab.transform), "화살표는 칸 안에 있어야 칸과 함께 움직입니다.");
        Assert.That(arrow.gameObject.activeSelf, Is.False, "강조되기 전에는 보이지 않아야 합니다.");
        Assert.That(arrow.anchorMin, Is.EqualTo(new Vector2(0, .5f)));
        Assert.That(arrow.anchorMax, Is.EqualTo(new Vector2(0, .5f)));
        Assert.That(arrow.pivot, Is.EqualTo(new Vector2(0, .5f)));
        var image = arrow.GetComponent<Image>();
        Assert.That(image.raycastTarget, Is.False, "화살표가 칸의 호버·홀드 입력을 가로채면 안 됩니다.");
        Assert.That(image.preserveAspect, Is.True);
        Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo(ArrowSpritePath));
        // 기존 선택지 구성은 그대로다.
        Assert.That(prefab.GetComponent<ChoiceFrameMesh>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<ChoiceHoldButton>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<CanvasGroup>(), Is.Null, "웹 바 선택지에는 등장 연출이 없습니다.");
    }

    [Test]
    public void ArrowAppearsOnlyOnTheFocusedSelectableChoice()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ChoicePrefabPath));
        var events = new GameObject("Choice arrow events", typeof(EventSystem));
        try
        {
            var fx = root.GetComponent<UIChoiceGlitch>();
            var button = root.GetComponent<ChoiceHoldButton>();
            var arrow = Arrow(root);
            var label = root.GetComponentInChildren<TMP_Text>(true);
            var resting = (Color)typeof(UIChoiceGlitch).GetField("restingColor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fx);
            var focused = (Color)typeof(UIChoiceGlitch).GetField("focusedColor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fx);
            Call(fx, "OnEnable");
            var pointer = new PointerEventData(events.GetComponent<EventSystem>());

            fx.OnPointerEnter(pointer);
            Assert.That(arrow.activeSelf, Is.True, "마우스를 올리면 바로 켜집니다.");
            Assert.That(label.color, Is.EqualTo(focused));
            fx.OnPointerExit(pointer);
            Assert.That(arrow.activeSelf, Is.False);
            Assert.That(label.color, Is.EqualTo(resting));

            var data = new BaseEventData(events.GetComponent<EventSystem>());
            fx.OnSelect(data);
            Assert.That(arrow.activeSelf, Is.True, "키보드로 선택해도 켜집니다.");
            fx.OnDeselect(data);
            Assert.That(arrow.activeSelf, Is.False);

            button.interactable = false;
            fx.OnPointerEnter(pointer);
            Assert.That(arrow.activeSelf, Is.False, "회색(선택 불가) 칸은 강조하지 않습니다.");
            fx.OnPointerExit(pointer);

            button.interactable = true;
            fx.OnPointerEnter(pointer);
            Call(fx, "OnDisable");
            Assert.That(arrow.activeSelf, Is.False, "선택지가 닫히면 화살표도 꺼집니다.");
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(events); }
    }

    [Test]
    public void MouseSelectionDoesNotKeepTheArrowAfterThePointerLeaves()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ChoicePrefabPath));
        var events = new GameObject("Choice arrow events", typeof(EventSystem)).GetComponent<EventSystem>();
        var previousEvents = EventSystem.current;
        // 편집 모드에서는 OnEnable이 자동으로 불리지 않는다. VisualUIReviewTests와 같은 방식으로 등록한다.
        Call(events, "OnEnable");
        EventSystem.current = events;
        try
        {
            var fx = root.GetComponent<UIChoiceGlitch>();
            var arrow = Arrow(root);
            Call(fx, "OnEnable");
            var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left };

            // 칸 위에서 마우스를 누르면 Selectable이 이 칸을 EventSystem 선택으로 잡는다.
            fx.OnPointerEnter(pointer);
            events.SetSelectedGameObject(root, pointer);
            fx.OnSelect(pointer);
            Assert.That(arrow.activeSelf, Is.True, "누르고 있는 동안은 호버로 강조됩니다.");
            fx.OnPointerExit(pointer);
            Assert.That(arrow.activeSelf, Is.False, "마우스로 생긴 선택은 커서가 떠나면 강조를 남기지 않습니다.");

            // 선택지가 닫혔다가 같은 칸이 다시 켜져도, 남아 있는 마우스 선택으로 강조가 켜지지 않는다.
            Call(fx, "OnDisable");
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(root));
            Call(fx, "OnEnable");
            Assert.That(arrow.activeSelf, Is.False);
            fx.OnPointerEnter(pointer);
            Assert.That(arrow.activeSelf, Is.True);
            fx.OnPointerExit(pointer);
            Assert.That(arrow.activeSelf, Is.False, "다시 뜬 선택지에서도 커서가 떠나면 꺼집니다.");

            // 키보드로 고른 칸은 커서와 관계없이 강조를 유지하고, 다시 켜져도 이어진다.
            var keyboard = new BaseEventData(events);
            fx.OnSelect(keyboard);
            Assert.That(arrow.activeSelf, Is.True);
            Call(fx, "OnDisable"); Call(fx, "OnEnable");
            Assert.That(arrow.activeSelf, Is.True, "키보드 선택은 다시 뜬 선택지에서도 유지됩니다.");
        }
        finally
        {
            Call(events, "OnDisable");
            Object.DestroyImmediate(root); Object.DestroyImmediate(events.gameObject);
            if (previousEvents != null) EventSystem.current = previousEvents;
        }
    }
}
