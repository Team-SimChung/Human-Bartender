using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Object = UnityEngine.Object;

/// <summary>
/// 외부·홈 선택지 표시를 실제 프리팹으로 검사한다.
/// 16 프리팹 크기 그대로·폭 규칙 통일(줄 폭·소지금·키 그림·화살표는 프리팹 배치 기준), 17 선택지 행 질문 유지,
/// 19 대화 중 하단 안내 숨김, 20 등장 직후 확정 보호 시간.
/// </summary>
public sealed class OutsideChoicePresentationTests
{
    private const string Folder = "Assets/01.Game/03.Content/08.Prefabs/03.UI/";
    private const string ChoicesPath = Folder + "03.Outside/InteractionChoices.prefab";
    private const string ItemPath = Folder + "03.Outside/InteractionChoiceItem.prefab";
    private const string HintPath = Folder + "03.Outside/InteractionHint.prefab";
    private const string ImagePath = Folder + "03.Outside/ImageViewer.prefab";
    private const string BubblePath = Folder + "02.Dialogue/OutsideDialogueTextBox.prefab";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const float Tolerance = .01f;
    private SceneSetup[] sceneSetup;
    private ELanguage previousLanguage;
    private readonly List<Object> owned = new();
    private readonly HashSet<WorldInteractionChoiceItem> awakenedRows = new();
    private NewStreetOptionData result;
    private int resultCount;

    [SetUp]
    public void SetUp()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        // 카메라·EventSystem이 없는 빈 씬에서 화면 배치 없이 상태만 검사한다.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        previousLanguage = GameStateManager.Instance.Language;
        GameStateManager.Instance.Language = ELanguage.Ko;
        result = default;
        resultCount = 0;
        awakenedRows.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var value in owned.AsEnumerable().Reverse())
            if (value != null) Object.DestroyImmediate(value);
        owned.Clear();
        GameStateManager.Instance.Language = previousLanguage;
        if (sceneSetup != null && sceneSetup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
    }

    // ── 16. 모든 외부 선택지가 프리팹 크기 그대로, 같은 폭 규칙 ──────────────────

    [Test]
    public void DialogueChoicesAndObjectMenusKeepThePrefabScaleAndShareTheWidthRule()
    {
        var view = MakeView(() => 300);
        var target = MakeTarget("수리공");
        var player = MakePlayer();
        var options = new[] { Allowed("gear", "기어 부품 건네주기"), Allowed("cancel", "취소") };
        // 크기는 프리팹에서 정한다. 코드는 선택창 Scale을 바꾸지 않는다.
        Vector3 authored = Load<WorldInteractionChoiceView>(ChoicesPath).transform.localScale;

        view.Begin(target, player);
        view.ShowOutsideChoices(options, _ => { });
        Vector3 dialogueScale = view.Choices.Rect.localScale;
        float[] dialogueWidths = RowWidths(view.Choices);
        bool dialogueMoney = view.Choices.MoneyLabel.gameObject.activeSelf;

        view.Begin(target, player, player);
        using var menuCancellation = new CancellationTokenSource();
        var menu = view.ChooseMenuAsync(options, null, menuCancellation.Token);
        Assert.That(view.IsChoosing, Is.True);
        Vector3 objectMenuScale = view.Choices.Rect.localScale;
        float[] menuWidths = RowWidths(view.Choices);
        bool menuMoney = view.Choices.MoneyLabel.gameObject.activeSelf;
        menuCancellation.Cancel();
        menu.Forget();

        Assert.That(dialogueScale, Is.EqualTo(authored), "대화 선택지는 프리팹에 저장된 Scale 그대로여야 합니다.");
        Assert.That(objectMenuScale, Is.EqualTo(authored), "엘리베이터·소파 같은 동작 메뉴도 프리팹에 저장된 Scale 그대로여야 합니다.");
        Assert.That(menuWidths, Is.EqualTo(dialogueWidths).Within(.01f), "같은 문구면 대화·메뉴의 항목 폭이 같아야 합니다.");
        var item = Load<WorldInteractionChoiceItem>(ItemPath);
        float maximum = ReadFloat(view.Choices, "maximumItemWidth");
        Assert.That(dialogueWidths[0], Is.EqualTo(ExpectedWidth(item, maximum, "기어 부품 건네주기")).Within(.01f));
        Assert.That(dialogueWidths[1], Is.EqualTo(item.Rect.rect.width).Within(.01f),
            "짧은 선택지는 줄 프리팹(InteractionChoiceItem)에 저장된 Width로 나와야 합니다.");
        Assert.That(dialogueMoney, Is.True, "대화 선택지의 소지금 줄 규칙은 그대로입니다.");
        Assert.That(menuMoney, Is.False, "동작 메뉴의 소지금 줄 규칙은 그대로입니다.");
    }

    // ── 16 후속. 줄 폭·소지금·키 그림·화살표는 프리팹 배치 기준 ──────────────

    [Test]
    public void ShortChoicesUseTheItemPrefabWidthInDialogueAndObjectMenus()
    {
        var view = MakeView(() => 500);
        var target = MakeTarget("TV");
        var player = MakePlayer();
        var options = new[] { Allowed("watch", "본다"), Allowed("off", "끈다"), Allowed("cancel", "취소") };
        float authored = Load<WorldInteractionChoiceItem>(ItemPath).Rect.rect.width;

        view.Begin(target, player);
        view.ShowOutsideChoices(options, _ => { });
        float[] dialogueWidths = RowWidths(view.Choices);
        AssertMoneyOnOneLine(view.Choices);

        view.Begin(target, player, player);
        using var menuCancellation = new CancellationTokenSource();
        var menu = view.ChooseMenuAsync(options, null, menuCancellation.Token);
        Assert.That(view.IsChoosing, Is.True);
        float[] menuWidths = RowWidths(view.Choices);
        menuCancellation.Cancel();
        menu.Forget();

        Assert.That(menuWidths, Is.EqualTo(new[] { authored, authored, authored }).Within(Tolerance),
            "TV 메뉴처럼 짧은 동작 메뉴는 줄 프리팹 Width 그대로 나와야 합니다.");
        Assert.That(dialogueWidths, Is.EqualTo(menuWidths).Within(Tolerance), "대화 선택지도 같은 폭 규칙입니다.");
    }

    [Test]
    public void LongerChoicesGrowOnlyUntilTheMaximumWidth()
    {
        var choices = MakeChoices(0, 0);
        var item = Load<WorldInteractionChoiceItem>(ItemPath);
        float authored = item.Rect.rect.width;
        float maximum = ReadFloat(choices, "maximumItemWidth");
        const string medium = "기어 부품 하나 건네주기";
        const string longest = "선택지 문구가 아무리 길어도 최대 폭에서 멈추고 글자를 줄여 맞춥니다";
        Assert.That(item.MeasureWidth(medium), Is.GreaterThan(authored).And.LessThan(maximum), "검사 문구의 전제가 바뀌었습니다.");
        Assert.That(item.MeasureWidth(longest), Is.GreaterThan(maximum), "검사 문구의 전제가 바뀌었습니다.");

        choices.Show(new[]
        {
            Allowed("gear", "기어 부품 건네주기"), Allowed("medium", medium), Allowed("long", longest), Allowed("cancel", "취소")
        }, Receive, ELanguage.Ko);
        float[] widths = RowWidths(choices);

        Assert.That(widths[0], Is.EqualTo(ExpectedWidth(item, maximum, "기어 부품 건네주기")).Within(.01f));
        Assert.That(widths[1], Is.EqualTo(item.MeasureWidth(medium)).Within(.01f), "프리팹 폭보다 긴 문구는 글자 폭만큼 늘어납니다.");
        Assert.That(widths[2], Is.EqualTo(maximum).Within(.01f), "최대 폭(Maximum Item Width)을 넘지 않습니다.");
        Assert.That(widths[3], Is.EqualTo(authored).Within(.01f), "같은 목록의 짧은 문구는 프리팹 폭 그대로입니다.");
    }

    [Test]
    public void ItemWidthFollowsTheWidthSavedOnTheItemPrefab()
    {
        var choices = MakeChoices(0, 0);
        UseItemWidth(choices, 90);
        choices.Show(new[] { Allowed("watch", "본다"), Allowed("cancel", "취소") }, Receive, ELanguage.Ko);
        Assert.That(RowWidths(choices), Is.EqualTo(new[] { 90f, 90f }).Within(.01f),
            "줄 프리팹 루트의 Width를 바꾸면 실행 중 기준 폭도 따라 바뀌어야 합니다.");
    }

    [Test]
    public void MoneyKeepsThePrefabWidthOnOneLineEvenWhenTheListIsNarrower()
    {
        var choices = MakeChoices(0, 0);
        UseItemWidth(choices, 20);
        choices.Show(new[] { Allowed("watch", "본다"), Allowed("cancel", "취소") }, Receive, ELanguage.Ko, 500);
        float authoredMoney = Load<WorldInteractionChoiceView>(ChoicesPath).MoneyLabel.rectTransform.rect.width;
        Assert.That(RowWidths(choices).Max(), Is.LessThan(authoredMoney), "검사 전제: 목록이 소지금 칸보다 좁아야 합니다.");
        AssertMoneyOnOneLine(choices);
    }

    [Test]
    public void KeyIconAndPointerUseThePrefabLayoutAndMirrorItWithoutOverlappingRows()
    {
        var choices = MakeChoices(0, 0);
        var authored = Load<WorldInteractionChoiceView>(ChoicesPath);
        AssertSidesFollowTheAuthoredLayout(choices, authored.KeyIcon.rectTransform.anchoredPosition,
            authored.Pointer.rectTransform.anchoredPosition);
    }

    [Test]
    public void EditedKeyIconAndPointerPositionsAreUsedOnBothSides()
    {
        var choices = MakeChoices(0, 0);
        // 프리팹에서 위치를 바꾼 것과 같은 상태. 코드에 숫자가 남아 있으면 결과가 따라오지 않는다.
        var key = new Vector2(1, 0);
        var pointer = new Vector2(16, 1);
        choices.KeyIcon.rectTransform.anchoredPosition = key;
        choices.Pointer.rectTransform.anchoredPosition = pointer;
        AssertSidesFollowTheAuthoredLayout(choices, key, pointer);
    }

    // ── 위치: 대화 선택지·사물 메뉴 모두 웹처럼 루나가 바라보는 반대쪽(등 뒤) ─────────

    [TestCase(true, 5f, false)]
    [TestCase(true, -5f, false)]
    [TestCase(false, 5f, true)]
    [TestCase(false, -5f, true)]
    public void ChoicesOpenBehindLunaWhereverTheTargetIs(bool facingRight, float targetX, bool right) =>
        Assert.That(WorldInteractionView.ChoicesOnRight(facingRight, 0, targetX), Is.EqualTo(right),
            "사물·NPC 위치와 상관없이 바라보는 방향의 반대쪽이어야 합니다.");

    [TestCase(5f, false)]
    [TestCase(-5f, true)]
    [TestCase(0f, true)]
    public void WithoutAFacingDirectionChoicesOpenAwayFromTheTarget(float targetX, bool right) =>
        Assert.That(WorldInteractionView.ChoicesOnRight(null, 0, targetX), Is.EqualTo(right),
            "이동 컴포넌트가 없으면 대상을 바라본다고 보고 그 반대쪽에 띄웁니다.");

    // 웹(world-choices.js)처럼 첫 항목 중심을 루나 머리 기준점(head)에 둔다. 줄 수·프리팹 Scale과 상관없고, 소지금 줄은 그 위에 들어간다.
    [Test]
    public void FirstRowCentersOnTheHeadPointWhateverTheRowCount()
    {
        var choices = MakeChoices(0, 0);
        var parent = (RectTransform)choices.Rect.parent;
        const float head = 40;
        float scale = Mathf.Abs(choices.Rect.localScale.y);
        float FirstRowCenter(int? money, params NewStreetOptionData[] options)
        {
            choices.Show(options, Receive, ELanguage.Ko, money);
            var rect = choices.Rect;
            // Follow()와 같은 계산: 위쪽 끝 = head + 첫 항목 높이의 절반, 높이는 Scale을 반영한 화면 단위.
            float top = WorldInteractionView.ChoiceTop(head, choices.FirstRowHeight * scale);
            rect.anchoredPosition = new Vector2(0, WorldInteractionView.ChoicePivotY(top, rect.rect.height * scale, rect.pivot.y));
            var corners = new Vector3[4];
            VisibleRows(choices)[0].Rect.GetWorldCorners(corners);
            return (parent.InverseTransformPoint(corners[0]).y + parent.InverseTransformPoint(corners[1]).y) * .5f;
        }

        float two = FirstRowCenter(null, Allowed("ride", "올라간다"), Allowed("cancel", "취소"));
        float three = FirstRowCenter(null, Allowed("watch", "본다"), Allowed("off", "끈다"), Allowed("cancel", "취소"));
        float withMoney = FirstRowCenter(500, Allowed("watch", "본다"), Allowed("off", "끈다"), Allowed("cancel", "취소"));

        Assert.That(two, Is.EqualTo(head).Within(Tolerance), "첫 항목 중심이 head(루나 + Choice Head Height)에 와야 합니다.");
        Assert.That(three, Is.EqualTo(head).Within(Tolerance), "줄 수가 달라도 첫 항목 자리가 같아야 합니다.");
        Assert.That(withMoney, Is.EqualTo(head - ReadFloat(choices, "moneyHeaderHeight") * scale).Within(Tolerance),
            "소지금 줄은 위쪽 끝에 들어가고 첫 항목은 그만큼 아래입니다(웹과 같음).");
    }

    // 웹([data-side="left"] .bd-balance)처럼 소지금 줄은 루나 쪽 끝에 붙는다. 정렬은 프리팹 값을 쓰고 왼쪽 배치에서 좌우를 바꾼다.
    [Test]
    public void MoneyLineSitsOnLunasSideLikeTheWeb()
    {
        var choices = MakeChoices(0, 0);
        var authored = Load<WorldInteractionChoiceView>(ChoicesPath).MoneyLabel.horizontalAlignment;
        choices.Show(new[] { Allowed("talk", "대화한다"), Allowed("cancel", "취소") }, Receive, ELanguage.Ko, 500);
        var content = (RectTransform)choices.SelectionCursor.parent;
        var money = choices.MoneyLabel;

        choices.SetSide(true);
        Assert.That(money.horizontalAlignment, Is.EqualTo(authored), "오른쪽 배치는 프리팹 정렬 그대로입니다.");
        float rowsLeft = VisibleRows(choices).Min(row => Span(content, row.Rect).min);
        Assert.That(Span(content, money.rectTransform).min, Is.EqualTo(rowsLeft).Within(Tolerance),
            "오른쪽 배치에서 소지금은 줄 묶음 왼쪽 끝(루나 쪽)에서 시작합니다.");

        choices.SetSide(false);
        var mirrored = authored == HorizontalAlignmentOptions.Left ? HorizontalAlignmentOptions.Right
            : authored == HorizontalAlignmentOptions.Right ? HorizontalAlignmentOptions.Left : authored;
        Assert.That(money.horizontalAlignment, Is.EqualTo(mirrored), "왼쪽 배치는 정렬을 좌우로 바꿉니다.");
        float rowsRight = VisibleRows(choices).Max(row => Span(content, row.Rect).max);
        Assert.That(Span(content, money.rectTransform).max, Is.EqualTo(rowsRight).Within(Tolerance),
            "왼쪽 배치에서 소지금은 줄 묶음 오른쪽 끝(루나 쪽)에 붙습니다.");

        choices.SetSide(true);
        Assert.That(money.horizontalAlignment, Is.EqualTo(authored), "다시 오른쪽이면 프리팹 정렬로 돌아옵니다.");
    }

    // ── 17. 선택지 행의 질문 말풍선 ───────────────────────────────────────

    [Test]
    public void ChoiceRowQuestionStaysWithChoicesAndDoesNotCarryIntoTheNextScene()
    {
        var view = MakeView();
        var player = MakePlayer();
        view.Begin(MakeTarget("수리공"), player);
        view.ShowChoicePrompt("repair", "나한테 남은 볼 일 있나?");
        var bubble = view.Bubble;
        Assert.That(bubble.gameObject.activeSelf, Is.True);
        Assert.That(bubble.textLabel.text, Does.Contain("나한테 남은 볼 일 있나?"));
        Assert.That(bubble.TextPlayer.IsHolding, Is.True, "질문은 타이핑 없이 다 친 상태로 보여야 합니다.");

        var options = new[] { Allowed("gear", "기어 부품 건네주기"), Allowed("cancel", "취소") };
        view.ShowOutsideChoices(options, _ => { });
        Assert.That(view.IsChoosing, Is.True);
        Assert.That(bubble.gameObject.activeSelf, Is.True, "선택지를 띄워도 질문 말풍선이 남아야 합니다.");
        Assert.That(bubble.textLabel.text, Does.Contain("나한테 남은 볼 일 있나?"));

        view.ShowChoicePrompt("repair", "   ");
        Assert.That(view.IsChoosing, Is.True, "빈 문구는 표시하지 않고 열린 선택지를 유지합니다.");
        Assert.That(bubble.textLabel.text, Does.Contain("나한테 남은 볼 일 있나?"));

        // 대사 없이 메뉴를 여는 다른 대상은 이전 NPC의 말풍선을 가져오지 않는다.
        view.EndScene();
        view.Begin(MakeTarget("TV"), player);
        view.ShowOutsideChoices(options, _ => { });
        Assert.That(view.IsChoosing, Is.True);
        Assert.That(bubble.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void MenuMessageUsesTheSameInstantBubble()
    {
        var view = MakeView();
        var player = MakePlayer();
        view.Begin(MakeTarget("소파"), player, player);
        using var cancellation = new CancellationTokenSource();
        var menu = view.ChooseMenuAsync(new[] { Allowed("save", "저장"), Allowed("cancel", "취소") }, "저장했습니다.", cancellation.Token);
        Assert.That(view.IsChoosing, Is.True);
        Assert.That(view.Bubble.gameObject.activeSelf, Is.True);
        Assert.That(view.Bubble.textLabel.text, Does.Contain("저장했습니다."));
        Assert.That(view.Bubble.TextPlayer.IsHolding, Is.True);
        cancellation.Cancel();
        menu.Forget();
    }

    [Test]
    public void ChoiceRowsPassTheirTextAndActorBeforeChoicesAndBlankRowsKeepThePreviousLine()
    {
        var presenter = new RecordingPresenter { PickFirst = true };
        var runner = MakeRunner(presenter, null);
        var question = new Step
        {
            Type = "choice", Actor = "repair", Text = new Texts { Ko = "나한테 남은 볼 일 있나?", En = "Anything else?" },
            Options = new[] { Allowed("cancel", "취소") }
        };
        var blank = new Step { Type = "choice", Actor = "repair", Options = new[] { Allowed("ok", "좋아") } };
        var said = runner.PlayOutsideAsync(new[] { Say("luna", "안녕하세요."), question, blank }).GetAwaiter().GetResult();
        Assert.That(said.Completed, Is.True, said.Error);
        Assert.That(presenter.Events, Is.EqualTo(new[]
        {
            "say:luna:안녕하세요.", "prompt:repair:나한테 남은 볼 일 있나?", "choices:cancel", "choices:ok"
        }));

        presenter.Events.Clear();
        GameStateManager.Instance.Language = ELanguage.En;
        question.Options = new[] { Allowed("cancel", "Cancel") };
        var english = runner.PlayOutsideAsync(new[] { question }).GetAwaiter().GetResult();
        Assert.That(english.Completed, Is.True, english.Error);
        Assert.That(presenter.Events, Is.EqualTo(new[] { "prompt:repair:Anything else?", "choices:cancel" }));
    }

    [Test]
    public void RealCsvCommandChoiceShowsItsQuestionBeforeTheChoices()
    {
        var content = new CsvStoryTestContent();
        var thug = content.GetSceneSteps("day1-corridor-thug").Single(step => step.Command == OutsideStoryCommand.ThugChoice);
        Assert.That(thug.Text?.Ko, Is.Not.Null.And.Not.Empty, "CSV의 thug-choice 행에 질문 문구가 있어야 합니다.");
        var presenter = new RecordingPresenter();
        var runner = MakeRunner(presenter, new OutsideStoryEvents(new[] { new StreetStoryEvents(content, GameStateManager.Instance) }));
        using var cancellation = new CancellationTokenSource();
        var operation = runner.PlayOutsideAsync(new[] { thug }, cancellation.Token);
        Assert.That(operation.Status, Is.EqualTo(UniTaskStatus.Pending));
        Assert.That(presenter.Events.Count, Is.EqualTo(2), string.Join(", ", presenter.Events));
        Assert.That(presenter.Events[0], Is.EqualTo($"prompt:{thug.Actor}:{thug.Text.Ko}"));
        Assert.That(presenter.Events[1], Does.StartWith("choices:"));
        cancellation.Cancel();
        Assert.That(operation.GetAwaiter().GetResult().Status, Is.EqualTo(StoryExecutionStatus.Cancelled));
    }

    // ── 19. 대화 중 하단 안내 ───────────────────────────────────────────

    [TestCase("OutSide")]
    [TestCase("Home")]
    public void InteractionHintUsesTheSharedIconAndBoldFont(string sceneName)
    {
        const string iconPath = "Assets/01.Game/03.Content/01.ArtResources/05.UI/06.Ect/InteractionHintIcon/outside_interact_icon.png";
        const string fontPath = "Assets/01.Game/03.Content/05.Fonts/Bold_Paperlogy_SDF.asset";
        var expected = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        var expectedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        Assert.That(expected, Is.Not.Null);
        Assert.That(expectedFont, Is.Not.Null);

        string preference = GameKeyBindings.PreferenceKey(GameControl.Interact);
        bool hadPreference = PlayerPrefs.HasKey(preference);
        int previousKey = PlayerPrefs.GetInt(preference);
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/" + sceneName + ".unity");
        try
        {
            var view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionView>(true)).Single();
            view.Initialize(() => { }, () => { });
            var target = MakeTarget("NPC");

            PlayerPrefs.SetInt(preference, (int)Key.E);
            view.ShowHint(target);
            Assert.That(view.Hint.KeyIcon.enabled, Is.True);
            Assert.That(view.Hint.KeyIcon.sprite, Is.SameAs(expected));
            Assert.That(view.Hint.Label.font, Is.SameAs(expectedFont));
            Assert.That(view.Hint.Label.fontSharedMaterial, Is.SameAs(expectedFont.material));

            PlayerPrefs.SetInt(preference, (int)Key.F);
            view.ShowHint(target);
            Assert.That(view.Hint.KeyIcon.enabled, Is.False);
            Assert.That(view.Hint.Label.text, Does.StartWith("F"));
        }
        finally
        {
            if (hadPreference) PlayerPrefs.SetInt(preference, previousKey);
            else PlayerPrefs.DeleteKey(preference);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void DialogueHidesRevealAndNextHintButKeepsInteractionAndImageExit()
    {
        var view = MakeView();
        var player = MakePlayer();
        var npc = MakeTarget("NPC");
        view.ShowHint(npc);
        Assert.That(view.Hint.gameObject.activeSelf, Is.True, "대상에게 말을 거는 안내는 유지합니다.");

        view.Begin(npc, player);
        view.SetWaitingForInput(false);
        Assert.That(view.Hint.gameObject.activeSelf, Is.False, "대사 출력 중 '펼치기'를 표시하지 않습니다.");
        view.SetWaitingForInput(true);
        Assert.That(view.Hint.gameObject.activeSelf, Is.False, "입력 대기 중 '다음'을 표시하지 않습니다.");

        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f));
        owned.Add(sprite);
        view.Begin(MakeTarget("포스터", WorldInteractionOutput.Image, sprite), player);
        view.SetWaitingForInput(true);
        Assert.That(view.Hint.gameObject.activeSelf, Is.True, "이미지 조사의 나가기 안내는 유지합니다.");
        string exitText;
        using (var data = new SerializedObject(view.Hint)) exitText = data.FindProperty("exitText").stringValue;
        Assert.That(view.Hint.Label.text, Does.Contain(exitText));
    }

    [Test]
    public void BubbleClickAdvancesOnlyWhileTheDialogueBubbleIsWaiting()
    {
        var view = MakeView();
        view.Begin(MakeTarget("NPC"), MakePlayer());
        view.ShowChoicePrompt("npc", "말풍선을 눌러 넘깁니다.");
        var rect = view.Bubble.bubble;
        Vector2 center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        Assert.That(view.IsDialoguePointer(center), Is.True, "하단 안내 대신 말풍선 클릭으로 진행해야 합니다.");
        Assert.That(view.IsDialoguePointer(center + new Vector2(100000, 0)), Is.False);

        view.ShowOutsideChoices(new[] { Allowed("ok", "좋아") }, _ => { });
        Assert.That(view.IsDialoguePointer(center), Is.False, "선택 중 클릭은 선택창이 처리합니다.");
        view.EndScene();
        Assert.That(view.IsDialoguePointer(center), Is.False);
    }

    // ── 20. 등장 직후 확정 보호 시간 ──────────────────────────────────────

    [Test]
    public void ConfirmIsDroppedDuringTheGuardWhileSelectionMoves()
    {
        var choices = MakeChoices(reveal: 0, guard: .5f);
        Open(choices, null, Locked("pay"), Allowed("next", "다음 대화로"), Allowed("cancel", "취소"));
        Assert.That(choices.IsConfirmGuarded, Is.True);
        double readyAt = ReadyAt(choices);

        choices.Confirm();
        Assert.That(choices.Rows[0].IsRejected, Is.False, "보호 시간에는 잠긴 항목의 거부 연출도 하지 않습니다.");
        choices.MoveSelection(1);
        Assert.That(choices.Rows[1].IsFocused, Is.True, "보호 시간에도 선택 이동은 됩니다.");
        choices.Confirm();
        choices.SetSide(false);
        Assert.That(ReadyAt(choices), Is.EqualTo(readyAt), "좌우 전환 같은 다시 그리기는 보호 시간을 다시 시작하지 않습니다.");
        Assert.That(resultCount, Is.Zero);
        Assert.That(choices.IsOpen, Is.True);

        ExpireGuard(choices);
        Assert.That(choices.IsConfirmGuarded, Is.False);
        Assert.That(resultCount, Is.Zero, "보호 시간에 누른 확정을 나중에 자동 실행하지 않습니다.");
        choices.Confirm();
        Assert.That(resultCount, Is.EqualTo(1));
        Assert.That(result.Id, Is.EqualTo("next"));
    }

    [Test]
    public void GuardEndsOnTheRealClock()
    {
        var choices = MakeChoices(reveal: 0, guard: .05f);
        Open(choices, null, Allowed("next", "다음 대화로"));
        choices.Confirm();
        Assert.That(resultCount, Is.Zero);
        Thread.Sleep(120);
        Assert.That(choices.IsConfirmGuarded, Is.False);
        choices.Confirm();
        Assert.That(resultCount, Is.EqualTo(1));
    }

    [Test]
    public void SelectionMovesWhileTheMenuIsStillRevealing()
    {
        var choices = MakeChoices(reveal: .22f, guard: .5f);
        Open(choices, null, Allowed("a", "첫째"), Allowed("b", "둘째"));
        Assert.That(choices.IsRevealing, Is.True);
        choices.MoveSelection(1);
        Assert.That(choices.Rows[1].IsFocused, Is.True);
        choices.Confirm();
        Assert.That(resultCount, Is.Zero);
    }

    [Test]
    public void EscapeCancelIsNotHeldByTheConfirmGuard()
    {
        var choices = MakeChoices(reveal: 0, guard: .5f);
        var cancel = Allowed("cancel", "취소");
        cancel.IsCancel = true;
        Open(choices, null, Allowed("go", "간다"), cancel);
        Assert.That(choices.IsConfirmGuarded, Is.True);
        choices.SelectCancelChoice();
        Assert.That(resultCount, Is.EqualTo(1), "웹과 같이 Esc 취소는 확정 보호 대상이 아닙니다.");
        Assert.That(result.Id, Is.EqualTo("cancel"));
    }

    [Test]
    public void DefaultsAreHalfASecondOutsideAndTerraceUsesItsOwnLongerGuard()
    {
        using (var prefab = new SerializedObject(Load<WorldInteractionChoiceView>(ChoicesPath)))
            Assert.That(prefab.FindProperty("confirmGuardDuration").floatValue, Is.EqualTo(.5f));
        var controller = Own(new GameObject("Controller")).AddComponent<WorldInteractionController>();
        Assert.That(ReadFloat(controller, "terraceChoiceConfirmGuard"), Is.EqualTo(.8f));

        var view = MakeView();
        var player = MakePlayer();
        var target = MakeTarget("크리스");
        view.Begin(target, player, choiceConfirmGuard: .8f);
        view.ShowOutsideChoices(new[] { Allowed("a", "첫째") }, _ => { });
        Assert.That(ReadyAt(view.Choices) - Time.realtimeSinceStartupAsDouble, Is.EqualTo(.8).Within(.05));

        view.EndScene();
        view.Begin(target, player);
        view.ShowOutsideChoices(new[] { Allowed("a", "첫째") }, _ => { });
        Assert.That(ReadyAt(view.Choices) - Time.realtimeSinceStartupAsDouble, Is.EqualTo(.5).Within(.05),
            "테라스 값은 그 장면에만 쓰고 다음 장면은 기본값으로 돌아가야 합니다.");
    }

    // ── 도우미 ──────────────────────────────────────────────────────

    private sealed class RecordingPresenter : IDialoguePresenter, IDialogueAutomaticPlayback, IDialogueChoicePrompt
    {
        public readonly List<string> Events = new();
        public bool PickFirst;
        public void SkipTyping() { }
        public void HideDialogue() { }
        public void EndScene() { }
        public EActivationMode GetPlayMode() => EActivationMode.Interact;
        public UniTask ShowDialogueAsync(string actor, string text, string arg, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Events.Add($"say:{actor}:{text}");
            return UniTask.CompletedTask;
        }
        public void ShowChoicePrompt(string actor, string text) => Events.Add($"prompt:{actor}:{text}");
        public void ShowOutsideChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> selected)
        {
            Events.Add("choices:" + string.Join("|", options.Select(option => option.Id)));
            if (PickFirst) selected(options[0]);
        }
        public UniTask WaitUntilReadyAsync(CancellationToken token) => UniTask.CompletedTask;
        public UniTask WaitAfterLineAsync(CancellationToken token) => UniTask.CompletedTask;
    }

    private DialogueRunner MakeRunner(IDialoguePresenter presenter, IOutsideStoryEvents events)
    {
        var data = Own(ScriptableObject.CreateInstance<PlayerDataSO>());
        var runner = Own(new GameObject("Choice prompt runner")).AddComponent<DialogueRunner>();
        typeof(DialogueRunner).GetField("conditionUtil", Private)
            .SetValue(runner, new ConditionUtil(GameStateManager.Instance, data, data));
        typeof(DialogueRunner).GetField("storyEvents", Private).SetValue(runner, events);
        runner.Bind(presenter);
        return runner;
    }

    private WorldInteractionView MakeView(Func<int> moneyReader = null)
    {
        var canvas = Own(new GameObject("Choice presentation canvas", typeof(RectTransform), typeof(Canvas)));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var layer = new GameObject("InteractionLayer", typeof(RectTransform));
        var rect = (RectTransform)layer.transform;
        rect.SetParent(canvas.transform, false);
        // 편집 모드 Overlay 캔버스는 크기가 정해지지 않을 수 있어 바 화면 기준 크기를 직접 준다.
        rect.sizeDelta = new Vector2(960, 540);
        var view = layer.AddComponent<WorldInteractionView>();
        using (var data = new SerializedObject(view))
        {
            data.FindProperty("bubblePrefab").objectReferenceValue = Load<DynamicSpeechBubble>(BubblePath);
            data.FindProperty("imageViewerPrefab").objectReferenceValue = Load<WorldInteractionImageView>(ImagePath);
            data.FindProperty("interactionHintPrefab").objectReferenceValue = Load<WorldInteractionHintView>(HintPath);
            data.FindProperty("choicesPrefab").objectReferenceValue = Load<WorldInteractionChoiceView>(ChoicesPath);
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        view.Initialize(() => { }, () => { }, moneyReader, () => { });
        return view;
    }

    private WorldInteractionChoiceView MakeChoices(float reveal, float guard)
    {
        var canvas = Own(new GameObject("Choice guard canvas", typeof(RectTransform), typeof(Canvas)));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ChoicesPath), canvas.transform);
        var choices = instance.GetComponent<WorldInteractionChoiceView>();
        Assert.That(choices.IsConfigured, Is.True, choices.ConfigurationError);
        using var data = new SerializedObject(choices);
        data.FindProperty("revealDuration").floatValue = reveal;
        data.FindProperty("cursorMoveDuration").floatValue = 0;
        data.FindProperty("confirmGuardDuration").floatValue = guard;
        data.ApplyModifiedPropertiesWithoutUndo();
        return choices;
    }

    private void Open(WorldInteractionChoiceView choices, float? guard, params NewStreetOptionData[] options)
    {
        choices.Show(options, Receive, ELanguage.Ko, null, guard);
        // 편집 모드에서는 Awake가 돌지 않으므로 버튼 연결을 직접 준비한다.
        foreach (var row in choices.Rows)
            if (awakenedRows.Add(row)) typeof(WorldInteractionChoiceItem).GetMethod("Awake", Private).Invoke(row, null);
        // 같은 프레임 중복 입력 방지만 풀고 보호 시간은 그대로 둔다.
        typeof(WorldInteractionChoiceView).GetField("openedFrame", Private).SetValue(choices, -1);
    }

    private void Receive(NewStreetOptionData selected)
    {
        result = selected;
        resultCount++;
    }

    private WorldInteractionTarget MakeTarget(string name, WorldInteractionOutput output = WorldInteractionOutput.SpeechBubble,
        Sprite image = null)
    {
        var target = Own(new GameObject(name)).AddComponent<WorldInteractionTarget>();
        using var data = new SerializedObject(target);
        data.FindProperty("output").enumValueIndex = (int)output;
        if (image != null) data.FindProperty("inspectionImage").objectReferenceValue = image;
        data.ApplyModifiedPropertiesWithoutUndo();
        return target;
    }

    private Transform MakePlayer() => Own(new GameObject("Player")).transform;

    private T Own<T>(T value) where T : Object
    {
        owned.Add(value);
        return value;
    }

    private static T Load<T>(string path) where T : Component
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        var component = prefab.GetComponent<T>();
        Assert.That(component, Is.Not.Null, path);
        return component;
    }

    private static float ReadFloat(Object owner, string property)
    {
        using var data = new SerializedObject(owner);
        var value = data.FindProperty(property);
        Assert.That(value, Is.Not.Null, property);
        return value.floatValue;
    }

    private static WorldInteractionChoiceItem[] VisibleRows(WorldInteractionChoiceView choices) =>
        choices.Rows.Where(row => row.gameObject.activeSelf).ToArray();

    private static float[] RowWidths(WorldInteractionChoiceView choices) =>
        VisibleRows(choices).Select(row => row.Rect.rect.width).ToArray();

    // 줄 폭 규칙: 기준은 줄 프리팹 Width, 문구가 더 길면 최대 폭까지.
    private static float ExpectedWidth(WorldInteractionChoiceItem item, float maximum, string text)
    {
        float authored = item.Rect.rect.width;
        return Mathf.Clamp(item.MeasureWidth(text), authored, Mathf.Max(authored, maximum));
    }

    // 줄 프리팹 루트의 Width를 Inspector에서 바꾼 것과 같은 상태를 만든다(원본 프리팹은 건드리지 않음).
    private void UseItemWidth(WorldInteractionChoiceView choices, float width)
    {
        var item = Object.Instantiate(Load<WorldInteractionChoiceItem>(ItemPath));
        Own(item.gameObject);
        item.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        using var data = new SerializedObject(choices);
        data.FindProperty("itemPrefab").objectReferenceValue = item;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssertMoneyOnOneLine(WorldInteractionChoiceView choices)
    {
        var money = choices.MoneyLabel;
        float authored = Load<WorldInteractionChoiceView>(ChoicesPath).MoneyLabel.rectTransform.rect.width;
        Assert.That(money.gameObject.activeSelf, Is.True, "대화 선택지에는 소지금 줄이 보여야 합니다.");
        Assert.That(money.rectTransform.rect.width, Is.GreaterThanOrEqualTo(authored - Tolerance),
            "소지금 칸이 프리팹의 Money Text 폭보다 좁아지면 안 됩니다.");
        money.ForceMeshUpdate(true);
        Assert.That(money.textInfo.lineCount, Is.EqualTo(1), $"'{money.text}'가 한 줄로 나와야 합니다.");
    }

    /// <summary>
    /// 오른쪽(기본) 배치는 프리팹 값 그대로, 왼쪽 배치는 선택창 전체를 좌우로 뒤집은 모양이고,
    /// 양쪽 모두 화살표가 줄과 겹치지 않는지 확인한다.
    /// </summary>
    private void AssertSidesFollowTheAuthoredLayout(WorldInteractionChoiceView choices, Vector2 key, Vector2 pointer)
    {
        choices.Show(new[] { Allowed("watch", "본다"), Allowed("off", "끈다"), Allowed("cancel", "취소") }, Receive, ELanguage.Ko);
        var content = (RectTransform)choices.SelectionCursor.parent;
        var keyRect = choices.KeyIcon.rectTransform;
        var pointerRect = choices.Pointer.rectTransform;

        choices.SetSide(true);
        Assert.That(keyRect.anchoredPosition, Is.EqualTo(key), "오른쪽 배치의 Key Icon은 프리팹 위치 그대로여야 합니다.");
        Assert.That(pointerRect.anchoredPosition, Is.EqualTo(pointer), "오른쪽 배치의 Pointer는 프리팹 위치 그대로여야 합니다.");
        Assert.That(pointerRect.localScale.x, Is.EqualTo(1));
        var rightKey = Span(content, keyRect);
        var rightPointer = Span(content, pointerRect);
        float rowsLeft = VisibleRows(choices).Min(row => Span(content, row.Rect).min);
        Assert.That(rightPointer.max, Is.LessThanOrEqualTo(rowsLeft + Tolerance), "오른쪽 배치에서 화살표가 줄 왼쪽 끝과 겹칩니다.");

        choices.SetSide(false);
        float panel = choices.Rect.rect.width;
        var leftKey = Span(content, keyRect);
        var leftPointer = Span(content, pointerRect);
        float rowsRight = VisibleRows(choices).Max(row => Span(content, row.Rect).max);
        Assert.That(leftPointer.min, Is.GreaterThanOrEqualTo(rowsRight - Tolerance), "왼쪽 배치에서 화살표가 줄 오른쪽 끝과 겹칩니다.");
        Assert.That(new[] { leftKey.min, leftKey.max }, Is.EqualTo(new[] { panel - rightKey.max, panel - rightKey.min }).Within(Tolerance),
            "왼쪽 배치의 Key Icon은 오른쪽 배치를 선택창 기준으로 좌우 반전한 자리여야 합니다.");
        Assert.That(new[] { leftPointer.min, leftPointer.max },
            Is.EqualTo(new[] { panel - rightPointer.max, panel - rightPointer.min }).Within(Tolerance),
            "왼쪽 배치의 Pointer는 오른쪽 배치를 선택창 기준으로 좌우 반전한 자리여야 합니다(줄과의 간격도 같음).");

        choices.SetSide(true);
        Assert.That(keyRect.anchoredPosition, Is.EqualTo(key), "다시 오른쪽으로 돌아오면 프리팹 위치로 돌아와야 합니다.");
        Assert.That(pointerRect.anchoredPosition, Is.EqualTo(pointer), "다시 오른쪽으로 돌아오면 프리팹 위치로 돌아와야 합니다.");
        Assert.That(pointerRect.localScale.x, Is.EqualTo(1));
    }

    // space 좌표에서 본 가로 범위. 좌우 반전(Scale -1)도 반영한다.
    private static (float min, float max) Span(RectTransform space, RectTransform part)
    {
        var corners = new Vector3[4];
        part.GetWorldCorners(corners);
        float[] xs = corners.Select(corner => space.InverseTransformPoint(corner).x).ToArray();
        return (xs.Min(), xs.Max());
    }

    private static double ReadyAt(WorldInteractionChoiceView choices) =>
        (double)typeof(WorldInteractionChoiceView).GetField("confirmReadyAt", Private).GetValue(choices);

    private static void ExpireGuard(WorldInteractionChoiceView choices) =>
        typeof(WorldInteractionChoiceView).GetField("confirmReadyAt", Private).SetValue(choices, 0d);

    private static Step Say(string actor, string text) => new() { Type = "say", Actor = actor, Text = new Texts { Ko = text, En = text } };

    private static NewStreetOptionData Allowed(string id, string text) => new()
    {
        Id = id, Selectable = true, Text = new Texts { Ko = text, En = text }
    };

    private static NewStreetOptionData Locked(string id) => new()
    {
        Id = id, Selectable = false,
        Text = new Texts { Ko = "비용을 건넨다.", En = "Pay the fee." },
        LockReason = new Texts { Ko = "돈이 부족합니다.", En = "Not enough money." }
    };
}
