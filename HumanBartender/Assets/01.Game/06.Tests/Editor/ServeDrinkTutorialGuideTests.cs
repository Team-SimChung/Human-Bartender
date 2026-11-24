using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0일차 서빙 수업(웹 tutorial-view.js serveDrink): 완성 칵테일 카드만 비워 강조하고 카드 옆에 안내 상자를 띄우며,
/// 화살표는 그리지 않고 잔을 끄는 동안에는 딤·강조·안내를 모두 숨기는지 검사한다.
/// </summary>
public sealed class ServeDrinkTutorialGuideTests
{
    const string OverlayPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/04.Tutorial/StoryTutorialOverlay.prefab";
    const string Text = "완성된 칵테일을 크리스 앞 코스터로 드래그하세요.";

    static StoryTutorialView CreateView(out RectTransform card)
    {
        var view = StoryTutorialView.Create(AssetDatabase.LoadAssetAtPath<StoryTutorialView>(OverlayPath), null);
        // 웹 .tray.drink-tray: 화면 아래 가운데의 완성 칵테일 카드.
        card = new GameObject("DrinkCard", typeof(RectTransform)).GetComponent<RectTransform>();
        card.SetParent(view.transform, false);
        card.sizeDelta = new Vector2(190, 84);
        card.anchoredPosition = new Vector2(0, -250);
        return view;
    }

    [Test]
    public void CardIsSpotlitWithoutArrowAndAllGuidanceHidesWhileDragging()
    {
        var view = CreateView(out var card);
        bool dragging = false;
        try
        {
            view.ShowGuide("완성된 잔 서빙", Text, card, pointer: false, hideGuideWhile: () => dragging);
            view.UpdateGuide();
            Assert.That(view.Guide.gameObject.activeSelf, Is.True);
            Assert.That(view.Guide.HasSource, Is.True, "카드 자리를 비워 강조한다.");
            Assert.That(view.Guide.HasDestination, Is.False, "끌기 전에는 놓을 자리를 강조하지 않는다.");
            Assert.That(view.Guide.RouteFrom.HasValue, Is.False, "웹 serveDrink는 화살표를 그리지 않는다.");
            Assert.That(view.InstructionPanel.gameObject.activeSelf, Is.True);
            Assert.That(view.Instruction, Is.EqualTo(Text));
            var copy = new Rect(view.InstructionPanel.anchoredPosition - view.InstructionPanel.sizeDelta * .5f, view.InstructionPanel.sizeDelta);
            var cardRect = new Rect(card.anchoredPosition - card.sizeDelta * .5f, card.sizeDelta);
            Assert.That(copy.Overlaps(cardRect), Is.False, "안내 상자는 카드를 가리지 않고 옆에 붙는다.");

            dragging = true;
            view.UpdateGuide();
            Assert.That(view.Guide.gameObject.activeSelf, Is.False, "웹 data-dragging: 잔을 끄는 동안 딤·강조를 숨긴다.");
            Assert.That(view.InstructionPanel.gameObject.activeSelf, Is.False);

            dragging = false;
            view.UpdateGuide();
            Assert.That(view.Guide.gameObject.activeSelf, Is.True, "놓지 못하고 돌아오면 다시 안내한다.");
            Assert.That(view.InstructionPanel.gameObject.activeSelf, Is.True);
        }
        finally { Object.DestroyImmediate(view.gameObject); }
    }

    [Test]
    public void OtherGuidesStillPointAtTheirTarget()
    {
        var view = CreateView(out var card);
        try
        {
            view.ShowGuide("레시피 선택", "칵테일 레시피를 눌러 주세요.", card);
            view.UpdateGuide();
            Assert.That(view.Guide.RouteFrom.HasValue && view.Guide.RouteTo.HasValue, Is.True, "다른 단계는 안내 상자에서 대상으로 화살표를 그린다.");
            Assert.That(view.Guide.HasDragRoute, Is.False);
        }
        finally { Object.DestroyImmediate(view.gameObject); }
    }
}
