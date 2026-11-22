using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

/// <summary>바 선택지의 거부 입력은 진행하지 않고, 원문·색상·홀드 동작을 복원하는지 확인한다.</summary>
public sealed class DialogueChoiceRejectionTests
{
    private const string PrefabPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/02.Dialogue/ChoicePanel.prefab";
    private GameObject root;
    private GameObject panelRoot;
    private GameObject eventRoot;
    private UIDialogueChoiceView view;
    private readonly List<ChoicePanel> panels = new List<ChoicePanel>();
    private int selectedIndex;

    [SetUp]
    public void CreateView()
    {
        root = new GameObject("거부 표시 검사");
        panelRoot = new GameObject("선택지", typeof(RectTransform));
        panelRoot.transform.SetParent(root.transform);
        view = root.AddComponent<UIDialogueChoiceView>();
        eventRoot = new GameObject("입력 검사", typeof(EventSystem));
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        for (int i = 0; i < 3; i++)
            panels.Add(Object.Instantiate(prefab, panelRoot.transform).GetComponent<ChoicePanel>());
        using var data = new SerializedObject(view);
        data.FindProperty("choicesPanel").objectReferenceValue = panelRoot;
        var slots = data.FindProperty("choicePanels");
        slots.arraySize = panels.Count;
        for (int i = 0; i < panels.Count; i++) slots.GetArrayElementAtIndex(i).objectReferenceValue = panels[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        selectedIndex = -1;
    }

    [TearDown]
    public void DestroyView()
    {
        if (view != null) view.CloseChoices();
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(eventRoot);
        panels.Clear();
    }

    private void Selected(int index) => selectedIndex = index;

    private PointerEventData Pointer() => new PointerEventData(eventRoot.GetComponent<EventSystem>())
    {
        button = PointerEventData.InputButton.Left
    };

    [Test]
    public void LockedPointerConfirmationShowsReasonAndInspectorColorsWithoutExecuting()
    {
        using (var data = new SerializedObject(panels[0]))
        {
            data.FindProperty("rejectedBackgroundColor").colorValue = Color.red;
            data.FindProperty("rejectedTextColor").colorValue = Color.yellow;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        view.ShowChoice(new[] { "비용을 낸다", "취소" }, new[] { false, true }, Selected,
            new[] { "돈이 부족합니다.", "" });
        Assert.That(panels[0].Label.text, Is.EqualTo("비용을 낸다"));
        Assert.That(panels[0].GetButton().interactable, Is.True);

        var hold = (ChoiceHoldButton)panels[0].GetButton();
        hold.OnPointerDown(Pointer());
        Assert.That(hold.IsHolding, Is.False, "거부 사유는 정상 선택의 홀드 시간을 기다리지 않습니다.");
        Assert.That(selectedIndex, Is.EqualTo(-1));
        Assert.That(panelRoot.activeSelf, Is.True);
        Assert.That(panels[0].IsRejected, Is.True);
        Assert.That(panels[0].Label.text, Is.EqualTo("돈이 부족합니다."));
        Assert.That(panels[0].Background.color, Is.EqualTo(Color.red));
        Assert.That(panels[0].Label.color, Is.EqualTo(Color.yellow));
        var normalGlitch = panels[0].GetComponent<UIChoiceGlitch>();
        Assert.That(normalGlitch.enabled, Is.True, "포커스 화살표와 입력 추적은 계속 동작해야 합니다.");
        Assert.That(normalGlitch.HasExternalFeedback, Is.True,
            "거부 표시 도중 기존 홀드 효과가 텍스트 색과 재질을 덮어쓰면 안 됩니다.");
    }

    [Test]
    public void MovingToAnotherChoiceRestoresOriginalLabelAndNormalEffect()
    {
        view.ShowChoice(new[] { "첫 선택", "다른 선택" }, new[] { false, true }, Selected,
            new[] { "재료가 없습니다.", "" });
        panels[0].GetButton().onClick.Invoke();
        panels[0].GetButton().onClick.Invoke();
        Assert.That(selectedIndex, Is.EqualTo(-1));
        panels[1].OnSelect(new BaseEventData(eventRoot.GetComponent<EventSystem>()));
        Assert.That(panels[0].IsRejected, Is.False);
        Assert.That(panels[0].Label.text, Is.EqualTo("첫 선택"));
        Assert.That(panels[0].Background.color, Is.EqualTo(Color.white));
        Assert.That(panels[0].GetComponent<UIChoiceGlitch>().enabled, Is.True);
        Assert.That(panels[0].GetComponent<UIChoiceGlitch>().HasExternalFeedback, Is.False);
        panels[1].GetButton().onClick.Invoke();
        Assert.That(selectedIndex, Is.EqualTo(1));
        Assert.That(panelRoot.activeSelf, Is.False);
    }

    [Test]
    public void ReusingRejectedSlotRestoresTheNormalHoldRequirement()
    {
        view.ShowChoice(new[] { "잠긴 선택" }, new[] { false }, Selected, new[] { "부품이 없습니다." });
        panels[0].GetButton().onClick.Invoke();
        view.CloseChoices();
        view.ShowChoice(new[] { "정상 선택" }, new[] { true }, Selected);
        var hold = (ChoiceHoldButton)panels[0].GetButton();
        var pointer = Pointer();
        hold.OnPointerDown(pointer);
        Assert.That(hold.IsHolding, Is.True);
        Assert.That(selectedIndex, Is.EqualTo(-1), "정상 선택은 누르는 순간 실행하지 않습니다.");
        Assert.That(panels[0].Label.text, Is.EqualTo("정상 선택"));
        Assert.That(panels[0].IsRejected, Is.False);
        hold.OnPointerUp(pointer);
        Assert.That(hold.IsHolding, Is.False);
    }

    [Test]
    public void PagingKeepsReasonsAndOriginalResultIndices()
    {
        view.ShowChoice(new[] { "하나", "둘", "셋", "넷" }, new[] { true, true, false, true }, Selected,
            new[] { "", "", "세 번째 사유", "" });
        panels[2].GetButton().onClick.Invoke();
        Assert.That(selectedIndex, Is.EqualTo(-1), "다음 페이지는 대화 분기를 실행하지 않습니다.");
        Assert.That(panels[0].Label.text, Is.EqualTo("셋"));
        panels[0].GetButton().onClick.Invoke();
        Assert.That(panels[0].Label.text, Is.EqualTo("세 번째 사유"));
        Assert.That(selectedIndex, Is.EqualTo(-1));
        panels[1].GetButton().onClick.Invoke();
        Assert.That(selectedIndex, Is.EqualTo(3));
    }

    [Test]
    public void OutsideCompatibilityPathAlsoBlocksUnavailableResults()
    {
        var options = new[]
        {
            new NewStreetOptionData
            {
                Id = "locked", Text = new Texts { Ko = "원문", En = "Original" }, Selectable = false,
                LockReason = new Texts { Ko = "사유", En = "Reason" }
            },
            new NewStreetOptionData { Id = "allowed", Text = new Texts { Ko = "진행", En = "Continue" } }
        };
        string result = null;
        view.ShowOutsideChoice(options, option => result = option.Id);
        panels[0].GetButton().onClick.Invoke();
        Assert.That(result, Is.Null);
        Assert.That(panels[0].Label.text, Is.EqualTo(view.CurrentLanguage == ELanguage.En ? "Reason" : "사유"));
        panels[1].GetButton().onClick.Invoke();
        Assert.That(result, Is.EqualTo("allowed"));
    }

    [Test]
    public void RejectedChoiceKeepsItsArrowAndRestoresTheColorOfItsCurrentFocus()
    {
        view.ShowChoice(new[] { "잠긴 선택", "다른 선택" }, new[] { false, true }, Selected,
            new[] { "재료가 없습니다.", "" });
        var firstGlitch = panels[0].GetComponent<UIChoiceGlitch>();
        var secondGlitch = panels[1].GetComponent<UIChoiceGlitch>();
        // 편집 모드 검사는 실제 선택지 컴포넌트의 활성화 초기화를 명시적으로 호출한다.
        typeof(UIChoiceGlitch).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(firstGlitch, null);
        typeof(UIChoiceGlitch).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(secondGlitch, null);
        using var firstSettings = new SerializedObject(firstGlitch);
        using var secondSettings = new SerializedObject(secondGlitch);
        var firstArrow = (GameObject)firstSettings.FindProperty("focusDecoration").objectReferenceValue;
        var secondArrow = (GameObject)secondSettings.FindProperty("focusDecoration").objectReferenceValue;
        Color resting = firstSettings.FindProperty("restingColor").colorValue;
        var keyboard = new BaseEventData(eventRoot.GetComponent<EventSystem>());

        firstGlitch.OnSelect(keyboard);
        panels[0].OnSelect(keyboard);
        Assert.That(firstArrow.activeSelf, Is.True);
        panels[0].GetButton().onClick.Invoke();
        Assert.That(firstGlitch.HasExternalFeedback, Is.True);
        Assert.That(firstArrow.activeSelf, Is.True, "거부 표시 중에도 선택 중인 항목의 화살표를 유지해야 합니다.");

        firstGlitch.OnDeselect(keyboard);
        secondGlitch.OnSelect(keyboard);
        panels[1].OnSelect(keyboard);
        Assert.That(firstArrow.activeSelf, Is.False);
        Assert.That(secondArrow.activeSelf, Is.True);
        Assert.That(panels[0].IsRejected, Is.False);
        Assert.That(firstGlitch.HasExternalFeedback, Is.False);
        Assert.That(panels[0].Label.text, Is.EqualTo("잠긴 선택"));
        Assert.That(panels[0].Label.color, Is.EqualTo(resting),
            "거부 전에 저장한 선택 색이 아니라 현재 비선택 상태의 색으로 돌아가야 합니다.");
    }
}
