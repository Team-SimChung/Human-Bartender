using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 대본 중 히든 레시피(조니의 올드 패션드)가 해금될 때 띄우는 확인 창. 표시와 확인 대기만 맡는다.
/// 해금 판정과 플래그 기록은 CampaignStoryRules가, 대본 대기는 StoryScriptRunner가 맡는다.
/// 고정 문구는 씬의 TMP에 두고, 칵테일 이름·그림·재료 줄만 CSV에서 채운다.
/// </summary>
[DisallowMultipleComponent]
public sealed class HiddenRecipeUnlockScreen : MonoBehaviour
{
    [Serializable]
    sealed class IngredientRow
    {
        [Tooltip("줄 전체. 레시피 줄이 없으면 숨깁니다.")]
        public GameObject root;
        [Tooltip("재료 그림. 이미지가 없으면 대체 그림을 쓰고, 그것도 없으면 숨깁니다.")]
        public Image icon;
        [Tooltip("재료 이름(shelf_items의 name.ko).")]
        public TMP_Text nameText;
        [Tooltip("목표량(recipes의 qty + unit).")]
        public TMP_Text amountText;
    }

    [Header("화면")]
    [Tooltip("켜고 끄는 자식 루트. 이 컴포넌트는 항상 켜져 있는 Canvas에 둡니다.")]
    [SerializeField] GameObject root;
    [Tooltip("루트의 묶음 투명도.")]
    [SerializeField] CanvasGroup canvasGroup;
    [Tooltip("확인을 누르면 대본이 이어집니다.")]
    [SerializeField] Button confirmButton;

    [Header("칵테일")]
    [Tooltip("칵테일 그림(CocktailRecipeVisualCatalog의 레시피 그림).")]
    [SerializeField] Image drinkImage;
    [Tooltip("칵테일 이름(cocktails의 name.ko).")]
    [SerializeField] TMP_Text drinkNameText;

    [Header("재료")]
    [Tooltip("씬에 배치한 재료 줄. 위에서부터 레시피 순서대로 채웁니다.")]
    [SerializeField] IngredientRow[] ingredientRows = Array.Empty<IngredientRow>();
    [Tooltip("unit=ml 표시.")]
    [SerializeField] string mlUnit = "ml";
    [Tooltip("unit=count 표시.")]
    [SerializeField] string countUnit = "개";
    [Tooltip("unit=oz 표시.")]
    [SerializeField] string ozUnit = "oz";
    [Tooltip("unit=tsp 표시.")]
    [SerializeField] string tspUnit = "tsp";

    [Header("시각 자료")]
    [Tooltip("CSV ID에 대응하는 레시피 이미지 카탈로그.")]
    [SerializeField] CocktailRecipeVisualCatalog visuals;
    [Tooltip("카탈로그 이미지가 없을 때 쓸 그림. 비워 두면 아이콘을 숨깁니다.")]
    [SerializeField] Sprite fallbackSprite;
    [Tooltip("등장 페이드 시간(초). 0이면 즉시 표시합니다.")]
    [Min(0)] [SerializeField] float fadeDuration = .28f;

    UniTaskCompletionSource pending;
    bool submitted;
    Tween fade;

    public bool IsOpen => root != null && root.activeSelf;

    void Awake()
    {
        if (root != null) root.SetActive(false);
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
    }

    /// <summary>창을 띄우고 확인을 누를 때까지 기다린다. 취소되면 창을 닫고 취소를 전달한다.</summary>
    public async UniTask ShowAsync(NewCocktailData cocktail, IReadOnlyList<NewShelfItemData> shelfItems, CancellationToken ct)
    {
        if (pending != null) throw new InvalidOperationException("[HiddenRecipeUnlock] 이미 확인을 기다리고 있습니다.");
        ValidateReferences();
        ct.ThrowIfCancellationRequested();
        var completion = new UniTaskCompletionSource();
        pending = completion;
        submitted = false;
        try
        {
            Render(cocktail, shelfItems);
            root.SetActive(true);
            confirmButton.interactable = true;
            // Explicit navigation with no neighbours keeps keyboard focus inside the modal.
            confirmButton.navigation = new Navigation { mode = Navigation.Mode.Explicit };
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

    void Render(NewCocktailData cocktail, IReadOnlyList<NewShelfItemData> shelfItems)
    {
        drinkNameText.text = cocktail.Name.Ko ?? cocktail.Id;
        Bind(drinkImage, visuals.GetCocktailSprite(cocktail.Id), cocktail.Id);

        var ingredients = RecipeNote.Build(cocktail).Ingredients;
        if (ingredients.Count > ingredientRows.Length)
            Debug.LogWarning($"[HiddenRecipeUnlock] '{cocktail.Id}'의 재료 {ingredients.Count}줄 중 " +
                             $"{ingredientRows.Length}줄만 표시합니다. 씬에 줄을 추가하세요.", this);
        for (int i = 0; i < ingredientRows.Length; i++)
        {
            var row = ingredientRows[i];
            bool used = i < ingredients.Count;
            row.root.SetActive(used);
            if (!used) continue;
            var ingredient = ingredients[i];
            row.nameText.text = FindName(shelfItems, ingredient.IngredientId);
            row.amountText.text = FormatAmount(ingredient);
            Bind(row.icon, visuals.GetIngredientSprite(ingredient.IngredientId), ingredient.IngredientId);
        }
    }

    static string FindName(IReadOnlyList<NewShelfItemData> shelfItems, string id)
    {
        if (shelfItems != null)
            foreach (var item in shelfItems)
                if (item.Id == id && !string.IsNullOrEmpty(item.Name.Ko)) return item.Name.Ko;
        return id;
    }

    string FormatAmount(RecipeNoteIngredient ingredient)
    {
        if (!ingredient.Qty.HasValue) return "";
        string qty = ingredient.Qty.Value.ToString("0.##", CultureInfo.InvariantCulture);
        string unit = ingredient.Unit switch
        {
            ENewUnit.Ml => mlUnit,
            ENewUnit.Count => countUnit,
            ENewUnit.Oz => ozUnit,
            ENewUnit.Tsp => tspUnit,
            _ => ""
        };
        return qty + unit;
    }

    void Bind(Image image, Sprite sprite, string id)
    {
        if (sprite == null)
        {
            Debug.LogWarning($"[HiddenRecipeUnlock] '{id}'의 이미지가 없어 대체 이미지를 사용합니다.", this);
            sprite = fallbackSprite;
        }
        image.sprite = sprite;
        image.enabled = sprite != null;
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
        bool rowsReady = ingredientRows != null && ingredientRows.Length > 0;
        if (rowsReady)
            foreach (var row in ingredientRows)
                if (row == null || row.root == null || row.icon == null || row.nameText == null || row.amountText == null)
                    rowsReady = false;
        if (root == null || root == gameObject || !root.transform.IsChildOf(transform) || canvasGroup == null ||
            confirmButton == null || drinkImage == null || drinkNameText == null || visuals == null || !rowsReady)
            throw new InvalidOperationException("[HiddenRecipeUnlock] HiddenRecipeUnlockScreen의 UI 참조를 연결해 주세요. " +
                                                "Setup Hidden Recipe Unlock 메뉴로 만들 수 있습니다.");
    }
}
