using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>실제 선택지 프리팹과 CSV를 사용해 거부 표시가 대화 결과를 실행하지 않는지 확인한다.</summary>
public sealed class ChoiceRejectionTests
{
    private const string ViewPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/03.Outside/InteractionChoices.prefab";
    private const string ItemPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/03.Outside/InteractionChoiceItem.prefab";
    private readonly HashSet<WorldInteractionChoiceItem> initializedRows = new();
    private GameObject canvasObject;
    private EventSystem events;
    private EventSystem previousEvents;
    private WorldInteractionChoiceView view;
    private int resultCount;
    private NewStreetOptionData result;

    [SetUp]
    public void CreateView()
    {
        previousEvents = EventSystem.current;
        events = new GameObject("선택지 검사 입력", typeof(EventSystem)).GetComponent<EventSystem>();
        InvokeLifecycle(events, "OnEnable");
        EventSystem.current = events;
        canvasObject = new GameObject("선택지 검사 캔버스", typeof(RectTransform), typeof(Canvas));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ViewPath), canvasObject.transform);
        view = instance.GetComponent<WorldInteractionChoiceView>();
        Assert.That(view.IsConfigured, Is.True, view.ConfigurationError);
        using var data = new SerializedObject(view);
        data.FindProperty("revealDuration").floatValue = 0;
        data.FindProperty("cursorMoveDuration").floatValue = 0;
        data.ApplyModifiedPropertiesWithoutUndo();
        resultCount = 0;
        initializedRows.Clear();
    }

    [TearDown]
    public void DestroyView()
    {
        if (view != null) view.Hide();
        if (canvasObject != null) Object.DestroyImmediate(canvasObject);
        if (events != null)
        {
            InvokeLifecycle(events, "OnDisable");
            Object.DestroyImmediate(events.gameObject);
        }
        if (previousEvents != null) EventSystem.current = previousEvents;
    }

    [Test]
    public void AuthoredItemUsesOneTextAndTheExistingGlitchSettings()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPath);
        var item = prefab.GetComponent<WorldInteractionChoiceItem>();
        Assert.That(item.IsConfigured, Is.True, item.ConfigurationError);
        Assert.That(prefab.GetComponentsInChildren<TMP_Text>(true), Has.Length.EqualTo(1),
            "거부 사유는 별도 줄이 아니라 기존 Text 한 곳에서 보여야 합니다.");
        Assert.That(prefab.GetComponentsInChildren<Transform>(true).Any(IsOldReasonObject), Is.False);
        using var data = new SerializedObject(item);
        var settings = data.FindProperty("rejectionGlitchSettings").objectReferenceValue as UIChoiceGlitchSettings;
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings.TextGlitchMaterial, Is.Not.Null);
        Assert.That(settings.TextGlitchMaterial.shader.name, Is.EqualTo("Custom/TMPGlitch"));
        Assert.That(data.FindProperty("selectedBackgroundColor"), Is.Not.Null);
        Assert.That(data.FindProperty("rejectedBackgroundColor"), Is.Not.Null);
        Assert.That(data.FindProperty("rejectedTextColor"), Is.Not.Null);
    }

    [Test]
    public void LockedChoiceShowsInspectorColorsAndReasonWithoutSelectingOrClosing()
    {
        Show(LockedChoice(), CancelChoice());
        var row = view.Rows[0];
        Color normalBackground = new(.2f, .8f, .7f, 1);
        Color rejectionBackground = new(.9f, .12f, .15f, 1);
        Color rejectionText = new(.3f, .04f, .02f, 1);
        SetColor(row, "selectedBackgroundColor", normalBackground);
        SetColor(row, "rejectedBackgroundColor", rejectionBackground);
        SetColor(row, "rejectedTextColor", rejectionText);
        row.SetFocused(true);

        Assert.That(row.IsFocused, Is.True, "첫 번째 항목의 조건이 부족해도 커서가 건너뛰면 안 됩니다.");
        Assert.That(row.CanSelect, Is.True);
        Assert.That(row.CanExecute, Is.False);
        Assert.That(row.Label.text, Is.EqualTo("비용을 건넨다."));
        Assert.That(row.Background.color, Is.EqualTo(normalBackground));
        view.Confirm();
        Assert.That(resultCount, Is.Zero);
        Assert.That(view.IsOpen, Is.True);
        Assert.That(row.IsRejected, Is.True);
        Assert.That(row.Label.text, Is.EqualTo("돈이 부족합니다."));
        Assert.That(row.Background.color, Is.EqualTo(rejectionBackground));
        Assert.That(row.Label.color, Is.EqualTo(rejectionText));

        view.Confirm();
        Assert.That(resultCount, Is.Zero, "다시 확정해도 잠긴 결과는 실행되지 않아야 합니다.");
        Assert.That(row.Label.text, Is.EqualTo("돈이 부족합니다."));
        view.MoveSelection(1);
        Assert.That(row.IsRejected, Is.False);
        Assert.That(row.Label.text, Is.EqualTo("비용을 건넨다."));
        Assert.That(row.Background.gameObject.activeSelf, Is.False);
        view.MoveSelection(-1);
        Assert.That(row.Background.color, Is.EqualTo(normalBackground));
        Assert.That(row.IsRejected, Is.False, "다시 가리키기만 했을 때는 거부 상태가 되살아나면 안 됩니다.");

        view.MoveSelection(1);
        view.Confirm();
        view.Confirm();
        Assert.That(resultCount, Is.EqualTo(1));
        Assert.That(result.Id, Is.EqualTo("cancel"));
        Assert.That(view.IsOpen, Is.False);
    }

    [Test]
    public void PointerCanFocusLockedRowsAndChangingRowsRestoresTheOriginalText()
    {
        Show(CancelChoice(), LockedChoice());
        var locked = view.Rows[1];
        locked.OnPointerEnter(new PointerEventData(events));
        Assert.That(locked.IsFocused, Is.True);
        view.Confirm();
        Assert.That(locked.IsRejected, Is.True);
        view.Rows[0].OnPointerEnter(new PointerEventData(events));
        Assert.That(locked.IsRejected, Is.False);
        Assert.That(locked.Label.text, Is.EqualTo("비용을 건넨다."));
        Assert.That(resultCount, Is.Zero);
    }

    [Test]
    public void RepeatedRejectionRestartsOnlyItsTextGlitchAndKeepsTheReasonAfterTheEffectEnds()
    {
        Show(LockedChoice(), CancelChoice());
        var row = view.Rows[0];
        var originalMaterial = row.Label.fontSharedMaterial;
        var otherMaterial = view.Rows[1].Label.fontSharedMaterial;
        var effect = (ChoiceTextGlitch)typeof(WorldInteractionChoiceItem)
            .GetField("rejectionGlitch", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(row);
        view.Confirm();
        Assert.That(effect.IsPlaying, Is.True);
        SetElapsed(effect, .08);
        InvokeLifecycle(row, "LateUpdate");
        Assert.That(row.Label.fontSharedMaterial.shader.name, Is.EqualTo("Custom/TMPGlitch"));
        Assert.That(row.Label.fontSharedMaterial, Is.Not.SameAs(originalMaterial));
        Assert.That(row.Label.fontSharedMaterial.mainTexture, Is.EqualTo(originalMaterial.mainTexture));
        Assert.That(view.Rows[1].Label.fontSharedMaterial, Is.SameAs(otherMaterial));

        SetElapsed(effect, 1);
        InvokeLifecycle(row, "LateUpdate");
        Assert.That(effect.IsPlaying, Is.False);
        Assert.That(row.IsRejected, Is.True, "짧은 글리치 종료는 거부 상태의 종료가 아닙니다.");
        Assert.That(row.Label.text, Is.EqualTo("돈이 부족합니다."));
        Assert.That(row.Label.fontSharedMaterial, Is.SameAs(originalMaterial));
        view.Confirm();
        Assert.That(effect.IsPlaying, Is.True, "다시 확정하면 거부 효과를 다시 재생해야 합니다.");
        view.MoveSelection(1);
        Assert.That(effect.IsPlaying, Is.False);
        Assert.That(row.Label.fontSharedMaterial, Is.SameAs(originalMaterial));
        Assert.That(resultCount, Is.Zero);
    }

    [Test]
    public void RejectedStateDoesNotLeakIntoTheNextMenuUsingTheSameRows()
    {
        Show(LockedChoice(), CancelChoice());
        var reused = view.Rows[0];
        view.Confirm();
        Assert.That(reused.IsRejected, Is.True);
        view.Hide();
        Show(AllowedChoice("next", "다음 대화로"));
        Assert.That(view.Rows[0], Is.SameAs(reused));
        Assert.That(reused.IsRejected, Is.False);
        Assert.That(reused.CanExecute, Is.True);
        Assert.That(reused.Label.text, Is.EqualTo("다음 대화로"));
        view.Confirm();
        Assert.That(resultCount, Is.EqualTo(1));
        Assert.That(result.Id, Is.EqualTo("next"));
    }

    [Test]
    public void CallbackFromAClosedMenuCannotRejectOrSelectTheReplacementMenu()
    {
        Show(LockedChoice(), CancelChoice());
        var oldCallback = (Action)typeof(WorldInteractionChoiceItem)
            .GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view.Rows[0]);
        Show(AllowedChoice("replacement", "새 선택지"));
        oldCallback();
        Assert.That(resultCount, Is.Zero);
        Assert.That(view.IsOpen, Is.True);
        Assert.That(view.Rows[0].IsRejected, Is.False);
        Assert.That(view.Rows[0].Label.text, Is.EqualTo("새 선택지"));
        view.Confirm();
        Assert.That(resultCount, Is.EqualTo(1));
        Assert.That(result.Id, Is.EqualTo("replacement"));
    }

    [Test]
    public void AllLockedRowsRemainNavigableAndNeverReturnAChoice()
    {
        var first = LockedChoice();
        var second = LockedChoice();
        second.Id = "part";
        second.LockReason = new Texts { Ko = "부품이 없습니다." };
        Show(first, second);
        view.Confirm();
        Assert.That(view.Rows[0].IsRejected, Is.True);
        view.MoveSelection(1);
        view.Confirm();
        Assert.That(view.Rows[1].IsRejected, Is.True);
        Assert.That(view.Rows[1].Label.text, Is.EqualTo("부품이 없습니다."));
        view.MoveSelection(1);
        Assert.That(view.Rows[0].IsFocused, Is.True);
        Assert.That(resultCount, Is.Zero);
        Assert.That(view.IsOpen, Is.True);
    }

    [Test]
    public void LongerLocalizedReasonFitsTheSameTextWithoutAddingASecondLine()
    {
        var option = LockedChoice();
        option.Text.En = "Give part";
        option.LockReason.En = "A repair part is required before this choice.";
        Show(ELanguage.En, option, CancelChoice());
        var row = view.Rows[0];
        var text = row.Label;
        view.Confirm();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(view.Rect);
        text.ForceMeshUpdate(true);
        Assert.That(row.Label, Is.SameAs(text));
        Assert.That(text.text, Is.EqualTo(option.LockReason.En));
        Assert.That(text.enableAutoSizing, Is.True);
        Assert.That(text.isTextTruncated, Is.False, "거부 사유가 기존 최대 너비를 넘으면 글자 크기로 맞춰야 합니다.");
        Assert.That(text.textInfo.lineCount, Is.EqualTo(1));
        view.MoveSelection(1);
        Assert.That(text.text, Is.EqualTo("Give part"));
    }

    [TestCase(ELanguage.Ko, "선택할 수 없습니다.")]
    [TestCase(ELanguage.En, "Unavailable.")]
    public void MissingCsvReasonUsesTheExistingLocalizedFallback(ELanguage language, string expected)
    {
        var option = LockedChoice();
        option.LockReason = null;
        Show(language, option, CancelChoice());
        view.Confirm();
        Assert.That(view.Rows[0].Label.text, Is.EqualTo(expected));
        Assert.That(resultCount, Is.Zero);
    }

    [TestCase(ELanguage.Ko)]
    [TestCase(ELanguage.En)]
    public void RealCsvReasonIsDisplayedWithoutChangingMoneyFlagsOrResultSteps(ELanguage language)
    {
        var content = new CsvStoryTestContent();
        var choiceStep = content.GetSceneSteps("day1-corridor-thug").Single(HasThugPayment);
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            player.SetMoney(300);
            player.AddFlag("choice_test_saved", true);
            var flagsBefore = player.ReadFlags();
            var conditions = new ConditionUtil(GameStateManager.Instance, player, player);
            var options = choiceStep.Options.ToArray();
            for (int i = 0; i < options.Length; i++)
                options[i].Selectable = conditions.CheckRequired(options[i].When);
            var payment = options.Single(IsThugPayment);
            string titleBefore = payment.Text.Ko;
            Step[] continuationBefore = payment.ResultSteps;
            Show(language, options);
            view.Confirm();
            var row = view.Rows[0];
            Assert.That(row.IsRejected, Is.True);
            Assert.That(row.Label.text, Is.EqualTo(language == ELanguage.En ? payment.LockReason.En : payment.LockReason.Ko));
            Assert.That(resultCount, Is.Zero);
            Assert.That(player.HasMoney(), Is.EqualTo(300));
            Assert.That(player.ReadFlags(), Is.EquivalentTo(flagsBefore));
            Assert.That(payment.Text.Ko, Is.EqualTo(titleBefore), "표시 문구를 바꿀 때 원본 CSV 데이터까지 바꾸면 안 됩니다.");
            Assert.That(payment.ResultSteps, Is.SameAs(continuationBefore));
            view.MoveSelection(1);
            view.Confirm();
            Assert.That(result.Id, Is.EqualTo("thug-refuse"));
            Assert.That(resultCount, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(player); }
    }

    private void Show(params NewStreetOptionData[] options) => Show(ELanguage.Ko, options);

    private void Show(ELanguage language, params NewStreetOptionData[] options)
    {
        view.Show(options, ReceiveChoice, language, 300);
        foreach (var row in view.Rows)
        {
            // 편집 모드 검사에서는 MonoBehaviour의 Awake를 직접 호출한다.
            if (initializedRows.Add(row)) InvokeLifecycle(row, "Awake");
        }
        // 선택창을 열었던 입력의 중복 확정 방지만 해제하고 실제 Confirm 경로를 사용한다.
        typeof(WorldInteractionChoiceView).GetField("openedFrame", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(view, -1);
    }

    private void ReceiveChoice(NewStreetOptionData selected)
    {
        result = selected;
        resultCount++;
    }

    private static NewStreetOptionData LockedChoice() => new()
    {
        Id = "pay",
        Selectable = false,
        Text = new Texts { Ko = "비용을 건넨다.", En = "Pay the fee." },
        LockReason = new Texts { Ko = "돈이 부족합니다.", En = "Not enough money." }
    };

    private static NewStreetOptionData CancelChoice() => AllowedChoice("cancel", "취소");

    private static NewStreetOptionData AllowedChoice(string id, string text) => new()
    {
        Id = id,
        Selectable = true,
        Text = new Texts { Ko = text, En = text }
    };

    private static bool IsOldReasonObject(Transform child) => child.name == "LockReason";
    private static bool IsThugPayment(NewStreetOptionData option) => option.Id == "thug-pay";
    private static bool HasThugPayment(Step step) => step.Options != null && step.Options.Any(IsThugPayment);

    private static void SetColor(WorldInteractionChoiceItem item, string name, Color color)
    {
        using var data = new SerializedObject(item);
        var property = data.FindProperty(name);
        Assert.That(property, Is.Not.Null, name);
        property.colorValue = color;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetElapsed(ChoiceTextGlitch effect, double seconds)
    {
        typeof(ChoiceTextGlitch).GetField("startedAt", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(effect, Time.unscaledTimeAsDouble - seconds);
    }

    private static void InvokeLifecycle(object instance, string name)
    {
        var method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, name);
        method.Invoke(instance, null);
    }
}
