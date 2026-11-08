using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>일차 시작 안내 표시와 확인 대기. 진행 상태나 입력 맵은 변경하지 않는다.</summary>
[DisallowMultipleComponent]
public sealed class DailyUnlocksScreen : MonoBehaviour
{
    [Header("화면")]
    [Tooltip("켜고 끄는 자식 루트. 이 컴포넌트는 활성 Canvas에 둡니다.")]
    [SerializeField] GameObject root;
    [Tooltip("일차 배지 글자.")]
    [SerializeField] TMP_Text dayText;
    [Tooltip("{0}에 일차가 들어갑니다.")]
    [SerializeField] string dayFormat = "{0}일차";
    [Tooltip("기본 텍스트는 카드를 선택하기 전 안내 문구입니다.")]
    [SerializeField] TMP_Text detailText;
    [Tooltip("확인을 누르면 개점 대사로 진행합니다.")]
    [SerializeField] Button confirmButton;

    [Header("재료")]
    [Tooltip("신규 재료 개수.")]
    [SerializeField] TMP_Text ingredientCountText;
    [Tooltip("재료 카드용 GridLayoutGroup의 Content.")]
    [SerializeField] RectTransform ingredientContent;
    [Tooltip("신규 재료가 없을 때 표시할 안내.")]
    [SerializeField] GameObject ingredientEmpty;
    [Tooltip("재료 목록 스크롤. 키보드로 선택한 카드가 보이게 합니다.")]
    [SerializeField] ScrollRect ingredientScroll;

    [Header("칵테일")]
    [Tooltip("신규 칵테일 개수.")]
    [SerializeField] TMP_Text cocktailCountText;
    [Tooltip("칵테일 카드용 GridLayoutGroup의 Content.")]
    [SerializeField] RectTransform cocktailContent;
    [Tooltip("신규 칵테일이 없을 때 표시할 안내.")]
    [SerializeField] GameObject cocktailEmpty;
    [Tooltip("칵테일 목록 스크롤.")]
    [SerializeField] ScrollRect cocktailScroll;

    [Header("시각 자료")]
    [Tooltip("부족한 카드만 생성하고 다음 표시에서 재사용합니다.")]
    [SerializeField] DailyUnlockCard cardPrefab;
    [Tooltip("CSV ID에 대응하는 기존 레시피 이미지 카탈로그.")]
    [SerializeField] CocktailRecipeVisualCatalog visuals;
    [Tooltip("카탈로그 이미지가 없을 때 쓸 그림. 비워 두면 아이콘을 숨깁니다.")]
    [SerializeField] Sprite fallbackSprite;
    [Tooltip("루트의 묶음 투명도.")]
    [SerializeField] CanvasGroup canvasGroup;
    [Tooltip("등장 페이드 시간(초). 0이면 즉시 표시합니다.")]
    [Min(0)] [SerializeField] float fadeDuration = .28f;

    readonly List<DailyUnlockCard> ingredientCards = new();
    readonly List<DailyUnlockCard> cocktailCards = new();
    UniTaskCompletionSource pending;
    string defaultDetail;
    bool submitted;
    Tween fade;

    public bool IsOpen => root != null && root.activeSelf;

    void Awake()
    {
        defaultDetail = detailText != null ? detailText.text : "";
        if (root != null) root.SetActive(false);
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
    }

    public async UniTask ShowAsync(int day, DailyUnlocks unlocks, CancellationToken ct)
    {
        if (pending != null) throw new InvalidOperationException("[DailyUnlocks] 이미 확인을 기다리고 있습니다.");
        ValidateReferences();
        ct.ThrowIfCancellationRequested();
        var completion = new UniTaskCompletionSource();
        pending = completion;
        submitted = false;
        try
        {
            Render(day, unlocks);
            root.SetActive(true);
            confirmButton.interactable = true;
            LayoutRebuilder.ForceRebuildLayoutImmediate(ingredientContent);
            LayoutRebuilder.ForceRebuildLayoutImmediate(cocktailContent);
            ingredientScroll.verticalNormalizedPosition = cocktailScroll.verticalNormalizedPosition = 1;
            fade?.Kill();
            canvasGroup.alpha = 0;
            if (fadeDuration > 0 && Application.isPlaying)
                fade = canvasGroup.DOFade(1, fadeDuration).SetUpdate(true);
            else canvasGroup.alpha = 1;
            EventSystem.current?.SetSelectedGameObject(confirmButton.gameObject);
            await completion.Task.AttachExternalCancellation(ct);
        }
        finally
        {
            pending = null;
            if (this != null) Hide();
        }
    }

    void Render(int day, DailyUnlocks unlocks)
    {
        dayText.text = string.Format(dayFormat, day);
        ingredientCountText.text = unlocks.Ingredients.Count.ToString();
        cocktailCountText.text = unlocks.Cocktails.Count.ToString();
        ingredientEmpty.SetActive(unlocks.Ingredients.Count == 0);
        cocktailEmpty.SetActive(unlocks.Cocktails.Count == 0);
        detailText.text = defaultDetail;
        for (int i = 0; i < unlocks.Ingredients.Count; i++)
        {
            var item = unlocks.Ingredients[i];
            Card(ingredientCards, ingredientContent, i).Bind(
                SpriteOrFallback(visuals.GetIngredientSprite(item.Id), item.Id),
                item.Name.Ko ?? item.Id, item.Desc.Ko, ShowDetail);
        }
        for (int i = 0; i < unlocks.Cocktails.Count; i++)
        {
            var item = unlocks.Cocktails[i];
            Card(cocktailCards, cocktailContent, i).Bind(
                SpriteOrFallback(visuals.GetCocktailSprite(item.Id), item.Id),
                item.Name.Ko ?? item.Id, item.Flavor.Ko, ShowDetail);
        }
        HideSurplus(ingredientCards, unlocks.Ingredients.Count);
        HideSurplus(cocktailCards, unlocks.Cocktails.Count);
        WireNavigation(ingredientCards, unlocks.Ingredients.Count, ingredientContent, cocktailCards, unlocks.Cocktails.Count, true);
        WireNavigation(cocktailCards, unlocks.Cocktails.Count, cocktailContent, ingredientCards, unlocks.Ingredients.Count, false);
        var first = unlocks.Ingredients.Count > 0 ? ingredientCards[0].GetComponent<Selectable>() :
            unlocks.Cocktails.Count > 0 ? cocktailCards[0].GetComponent<Selectable>() : confirmButton;
        confirmButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = first, selectOnDown = first };
    }

    DailyUnlockCard Card(List<DailyUnlockCard> cards, Transform content, int index)
    {
        if (index == cards.Count) cards.Add(Instantiate(cardPrefab, content, false));
        cards[index].gameObject.SetActive(true);
        return cards[index];
    }

    static void HideSurplus(List<DailyUnlockCard> cards, int count)
    {
        for (int i = count; i < cards.Count; i++) cards[i].gameObject.SetActive(false);
    }

    // Explicit navigation keeps focus away from the bar UI behind the modal.
    void WireNavigation(List<DailyUnlockCard> cards, int count, RectTransform content,
        List<DailyUnlockCard> other, int otherCount, bool leftSection)
    {
        int columns = content.GetComponent<GridLayoutGroup>().constraintCount;
        for (int i = 0; i < count; i++)
        {
            Selectable At(int index) => index >= 0 && index < count ? cards[index].GetComponent<Selectable>() : confirmButton;
            var nav = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = At(i - columns), selectOnDown = At(i + columns),
                selectOnLeft = i % columns > 0 ? At(i - 1) : At(i),
                selectOnRight = i % columns < columns - 1 && i + 1 < count ? At(i + 1) : At(i) };
            if (otherCount > 0)
            {
                if (leftSection && (i % columns == columns - 1 || i == count - 1))
                    nav.selectOnRight = other[Math.Min(i / columns * columns, otherCount - 1)].GetComponent<Selectable>();
                if (!leftSection && i % columns == 0)
                    nav.selectOnLeft = other[Math.Min(i + columns - 1, otherCount - 1)].GetComponent<Selectable>();
            }
            cards[i].GetComponent<Selectable>().navigation = nav;
        }
    }

    Sprite SpriteOrFallback(Sprite sprite, string id)
    {
        if (sprite != null) return sprite;
        Debug.LogWarning($"[DailyUnlocks] '{id}'의 이미지가 없어 대체 이미지를 사용합니다.", this);
        return fallbackSprite;
    }

    void ShowDetail(DailyUnlockCard card)
    {
        if (!IsOpen || UIDisplayOptions.IsOptionOpen) return;
        detailText.text = string.IsNullOrEmpty(card.Description) ? card.DisplayName : $"{card.DisplayName} — {card.Description}";
        // Pointer hover does not move the list; only keyboard selection reveals clipped cards.
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != card.gameObject) return;
        var scroll = card.transform.parent == ingredientContent ? ingredientScroll : cocktailScroll;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, card.transform);
        var viewport = scroll.viewport.rect;
        float shift = bounds.min.y < viewport.yMin ? viewport.yMin - bounds.min.y :
            bounds.max.y > viewport.yMax ? viewport.yMax - bounds.max.y : 0;
        scroll.StopMovement();
        scroll.content.anchoredPosition += new Vector2(0, shift);
    }

    void Confirm()
    {
        if (pending == null || submitted || !IsOpen || UIDisplayOptions.IsOptionOpen) return;
        submitted = true;
        confirmButton.interactable = false;
        pending.TrySetResult();
    }

    public void Hide()
    {
        fade?.Kill();
        fade = null;
        if (confirmButton != null) confirmButton.interactable = false;
        if (root != null) root.SetActive(false);
        pending?.TrySetCanceled();
    }

    void OnDisable() => Hide();
    void OnDestroy()
    {
        Hide();
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
    }

    public void ValidateReferences()
    {
        if (root == null || root == gameObject || !root.transform.IsChildOf(transform) || dayText == null ||
            detailText == null || confirmButton == null || ingredientCountText == null || ingredientContent == null ||
            ingredientEmpty == null || ingredientScroll == null || cocktailCountText == null || cocktailContent == null ||
            cocktailEmpty == null || cocktailScroll == null || cardPrefab == null || visuals == null || canvasGroup == null ||
            ingredientScroll.viewport == null || cocktailScroll.viewport == null ||
            ingredientScroll.content != ingredientContent || cocktailScroll.content != cocktailContent ||
            ingredientContent.GetComponent<GridLayoutGroup>() == null || cocktailContent.GetComponent<GridLayoutGroup>() == null ||
            ingredientContent.GetComponent<GridLayoutGroup>().constraintCount < 1 || cocktailContent.GetComponent<GridLayoutGroup>().constraintCount < 1)
            throw new InvalidOperationException("[DailyUnlocks] DailyUnlocksScreen의 UI 참조를 연결해 주세요. 컴포넌트는 활성 Canvas에 둡니다.");
    }
}
