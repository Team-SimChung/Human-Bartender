using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>레시피 목록을 해금 상태와 한글 이름으로 정렬하고 제조 요청을 전달한다.</summary>
public sealed class CocktailRecipeBrowser : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private RectTransform listContent;
    [SerializeField] private ScrollRect listScrollRect;
    [SerializeField] private CocktailRecipeRow rowPrefab;
    [SerializeField] private CocktailRecipePreview preview;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Data and action")]
    [SerializeField] private NewCocktailDataSO cocktailData;
    [SerializeField] private CocktailRecipeVisualCatalog visuals;
    [SerializeField] private OrderRequestController orderRequests;
    [SerializeField] private CraftFlowController craftFlow;

    private static readonly CompareInfo KoreanCompareInfo = CultureInfo.GetCultureInfo("ko-KR").CompareInfo;
    private readonly List<CocktailRecipeRow> rows = new List<CocktailRecipeRow>();
    private int currentDay;
    private string previewedId;
    private string instructionText;

    public event Action CraftStarted;

    private void Awake()
    {
        if (statusText != null) instructionText = statusText.text;
    }

    public void Open()
    {
        gameObject.SetActive(true);
        if (preview != null) preview.Hide();
        previewedId = null;
        if (statusText != null) statusText.text = instructionText;

        ClearRows();
        if (listContent == null || rowPrefab == null || preview == null || cocktailData == null || craftFlow == null)
        {
            Debug.LogError("[CocktailRecipeBrowser] Content, Row Prefab, Preview, Cocktail Data, Craft Flow 참조를 연결해 주세요.", this);
            if (statusText != null) statusText.text = "레시피 목록 연결을 확인해 주세요.";
            return;
        }

        if (cocktailData.cocktailData == null)
        {
            if (statusText != null) statusText.text = "레시피 데이터를 불러오는 중입니다.";
            return;
        }

        currentDay = GameStateManager.Instance.CurrentDay;
        var sorted = new List<NewCocktailData>(cocktailData.cocktailData);
        sorted.Sort(CompareRecipes);

        foreach (NewCocktailData cocktail in sorted)
        {
            bool unlocked = IsSelectable(cocktail, currentDay);
            Sprite icon = visuals != null ? visuals.GetCocktailSprite(cocktail.Id) : null;
            CocktailRecipeRow row = Instantiate(rowPrefab, listContent);
            row.Bind(cocktail, unlocked, icon, this);
            rows.Add(row);
        }

        if (listScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            listScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void Close()
    {
        previewedId = null;
        if (preview != null) preview.Hide();
        gameObject.SetActive(false);
    }

    public void ShowPreview(string cocktailId)
    {
        if (preview == null || cocktailData == null || !cocktailData.TryGet(cocktailId, out NewCocktailData cocktail))
            return;
        if (!IsSelectable(cocktail, GameStateManager.Instance.CurrentDay)) return;

        previewedId = cocktailId;
        preview.Show(cocktail, visuals);
    }

    public void HidePreview(string cocktailId)
    {
        if (previewedId != cocktailId) return;
        previewedId = null;
        if (preview != null) preview.Hide();
    }

    public void TryStart(string cocktailId)
    {
        if (craftFlow == null) return;
        if (!craftFlow.TryBegin(cocktailId, out _, out string reason))
        {
            if (statusText != null) statusText.text = reason;
            return;
        }

        CraftStarted?.Invoke();
    }

    private int CompareRecipes(NewCocktailData left, NewCocktailData right)
    {
        bool leftUnlocked = IsSelectable(left, currentDay);
        bool rightUnlocked = IsSelectable(right, currentDay);
        if (leftUnlocked != rightUnlocked) return leftUnlocked ? -1 : 1;

        int byName = KoreanCompareInfo.Compare(left.Name.Ko, right.Name.Ko, CompareOptions.None);
        if (byName != 0) return byName;
        return StringComparer.Ordinal.Compare(left.Id, right.Id);
    }

    private bool IsSelectable(NewCocktailData cocktail, int day)
    {
        bool isRequested = orderRequests != null && orderRequests.HasWaitingOrderForCocktail(cocktail.Id);
        return CocktailUnlockPolicy.CanSelect(cocktail, day, isRequested);
    }

    private void ClearRows()
    {
        foreach (CocktailRecipeRow row in rows)
        {
            if (row == null) continue;
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }

        rows.Clear();
    }
}
