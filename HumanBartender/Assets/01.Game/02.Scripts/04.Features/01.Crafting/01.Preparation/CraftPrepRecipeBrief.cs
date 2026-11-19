using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 제조 준비 화면 왼쪽 위의 레시피 버튼. 고른 칵테일 이름을 보여 주고, 마우스를 올리거나 선택하면 필요한
/// 잔·도구·재료와 담았는지를 아래로 펼친다. 누르면 상세 레시피를 여는 연결은 CraftPrepStageScreen이 한다.
/// 웹 bar-views.js prepRecipeBrief · bar-views.css .prep-recipe-brief(1280 기준)를 960×540 캔버스에 0.75배로 옮겼다.
/// </summary>
public sealed class CraftPrepRecipeBrief : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("Parts")]
    [SerializeField] private Button button;
    [Tooltip("버튼 전체 사각형. 위쪽 피벗이라 펼치면 아래로 길어진다.")]
    [SerializeField] private RectTransform root;
    [SerializeField] private TextMeshProUGUI cocktailNameText;
    [Tooltip("버튼 패널(테두리 포함, Sliced). 레이캐스트를 받아 호버를 감지한다. 색은 그림에 들어 있으므로 흰색으로 둔다.")]
    [SerializeField] private Image background;
    [Tooltip("제목 아래 펼침 영역. RectMask2D로 잘리고 이 CanvasGroup으로 흐려진다.")]
    [SerializeField] private CanvasGroup revealGroup;
    [Tooltip("목록 줄이 놓이는 VerticalLayoutGroup. 위쪽 여백이 제목과 목록 사이 간격이다.")]
    [SerializeField] private RectTransform rowList;
    [SerializeField] private CraftPrepRecipeBriefRow rowPrefab;

    [Header("Size")]
    [Tooltip("접힌 높이. 위 여백 9 + 제목 22 + 아래 여백 9(웹 12·약 30·12의 0.75배).")]
    [SerializeField, Min(0)] private float collapsedHeight = 40f;

    [Header("Motion")]
    [Tooltip("펼치고 접는 시간(초). 웹 0.24초.")]
    [SerializeField, Min(0)] private float revealDuration = 0.24f;
    [Tooltip("목록이 나타나고 사라지는 시간(초). 웹 0.16초.")]
    [SerializeField, Min(0)] private float fadeDuration = 0.16f;
    [SerializeField] private Ease revealEase = Ease.OutCubic;

    [Header("Sprites")]
    [Tooltip("기본 패널(BTN_recipe_brief_A). 웹 배경 rgba(12,23,35,.65) · 테두리 #5b919f.")]
    [SerializeField] private Sprite normalSprite;
    [Tooltip("마우스를 올렸을 때 패널(BTN_recipe_brief_B). 웹 배경 rgba(18,38,52,.74) · 테두리 #94dce1.")]
    [SerializeField] private Sprite hoverSprite;

    [Header("Labels")]
    [SerializeField] private string glassLabel = "잔";
    [SerializeField] private string toolLabel = "도구";
    [SerializeField] private string ingredientLabel = "재료";

    private readonly List<CraftPrepRecipeBriefRow> rows = new List<CraftPrepRecipeBriefRow>();
    private int boundCount;
    private bool hovered;
    private bool selected;
    private Tween heightTween;
    private Tween fadeTween;

    public Button Button => button;
    public bool IsExpanded => hovered || selected;
    public float CollapsedHeight => collapsedHeight;
    public float CurrentHeight => root != null ? root.rect.height : 0f;
    public string CocktailName => cocktailNameText != null ? cocktailNameText.text : null;
    public Sprite BackgroundSprite => background != null ? background.sprite : null;
    public int RowCount => boundCount;
    public CraftPrepRecipeBriefRow GetRow(int index) => index >= 0 && index < boundCount ? rows[index] : null;

    /// <summary>칵테일 이름과 준비물 목록을 표시한다. 줄은 재사용하고 모자랄 때만 만든다.</summary>
    public void Bind(string cocktailName, IReadOnlyList<CraftPrepChecklistEntry> entries, Func<string, string> displayName)
    {
        if (root == null || cocktailNameText == null || revealGroup == null || rowList == null || rowPrefab == null)
        {
            Debug.LogError("[CraftPrep] 레시피 버튼의 Root, Cocktail Name Text, Reveal Group, Row List, Row Prefab을 연결해 주세요.", this);
            return;
        }

        cocktailNameText.text = cocktailName;
        boundCount = entries != null ? entries.Count : 0;
        for (int index = 0; index < boundCount; index++)
        {
            if (index == rows.Count) rows.Add(Instantiate(rowPrefab, rowList));
            CraftPrepChecklistEntry entry = entries[index];
            CraftPrepRecipeBriefRow row = rows[index];
            row.gameObject.SetActive(true);
            row.Bind(entry.Id, KindLabel(entry.Kind), displayName != null ? displayName(entry.Id) : entry.Id, entry.Packed);
        }
        for (int index = boundCount; index < rows.Count; index++)
            rows[index].gameObject.SetActive(false);

        // 줄 수가 바뀌었을 수 있으므로 지금 상태의 높이로 맞춘다.
        ApplyExpansion(false);
    }

    public void OnPointerEnter(PointerEventData eventData) => SetState(true, selected);
    public void OnPointerExit(PointerEventData eventData) => SetState(false, selected);
    public void OnSelect(BaseEventData eventData) => SetState(hovered, true);
    public void OnDeselect(BaseEventData eventData) => SetState(hovered, false);

    private void OnEnable()
    {
        hovered = selected = false;
        ApplyExpansion(false);
    }

    private void OnDisable()
    {
        hovered = selected = false;
        KillTweens();
    }

    private void SetState(bool isHovered, bool isSelected)
    {
        if (hovered == isHovered && selected == isSelected) return;
        hovered = isHovered;
        selected = isSelected;
        ApplyExpansion(true);
    }

    private string KindLabel(ECraftPrepChecklistKind kind) => kind switch
    {
        ECraftPrepChecklistKind.Glass => glassLabel,
        ECraftPrepChecklistKind.Tool => toolLabel,
        _ => ingredientLabel,
    };

    private void ApplyExpansion(bool animate)
    {
        if (root == null || revealGroup == null) return;
        KillTweens();
        if (background != null)
        {
            Sprite sprite = hovered ? hoverSprite : normalSprite;
            if (sprite != null) background.sprite = sprite;
        }

        bool expanded = IsExpanded;
        float height = expanded ? collapsedHeight + RowListHeight() : collapsedHeight;
        float alpha = expanded ? 1f : 0f;
        if (!animate || !Application.isPlaying)
        {
            SetHeight(height);
            revealGroup.alpha = alpha;
            return;
        }

        heightTween = DOTween.To(() => root.rect.height, SetHeight, height, revealDuration)
            .SetEase(revealEase).SetUpdate(true).SetLink(gameObject);
        fadeTween = revealGroup.DOFade(alpha, fadeDuration).SetUpdate(true).SetLink(gameObject);
    }

    private float RowListHeight()
    {
        if (rowList == null) return 0f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowList);
        return Mathf.Ceil(LayoutUtility.GetPreferredHeight(rowList));
    }

    private void SetHeight(float height) => root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

    private void KillTweens()
    {
        heightTween?.Kill();
        fadeTween?.Kill();
        heightTween = fadeTween = null;
    }
}
