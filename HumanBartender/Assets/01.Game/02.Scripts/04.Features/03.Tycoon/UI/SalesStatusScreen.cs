using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

/// <summary>영업 중 당일 장부와 현재 소지금을 표시한다.</summary>
public sealed class SalesStatusScreen : MonoBehaviour
{
    [Header("Screen")]
    [SerializeField] GameObject root;
    [SerializeField] CanvasGroup panelGroup;
    [SerializeField] Button closeButton;
    [SerializeField, Min(0f), Tooltip("게임이 일시정지된 상태에서도 진행하는 등장 페이드 시간입니다.")]
    float fadeDuration = 0.12f;

    [Header("Summary")]
    [SerializeField] TMP_Text balanceText;
    [SerializeField] TMP_Text salesText;
    [SerializeField] TMP_Text tipsText;
    [SerializeField] TMP_Text refundsText;
    [SerializeField] TMP_Text totalText;

    [Header("Settled orders")]
    [SerializeField] ScrollRect scroll;
    [SerializeField] SalesStatusRow rowPrefab;
    [SerializeField] GameObject emptyMessage;
    [SerializeField] NewCocktailDataSO cocktailData;

    [Inject] DailySales ledger;
    [Inject] IPlayerDataReader player;

    readonly List<SalesStatusRow> rows = new();
    IDisposable pauseHandle;
    Tween fade;

    public bool CanOpen => isActiveAndEnabled && ledger != null && player != null;
    public bool IsOpen { get; private set; }
    public event Action OpenChanged;

    void Awake() => root.SetActive(false);
    void OnEnable() => closeButton.onClick.AddListener(Close);

    void OnDisable()
    {
        closeButton.onClick.RemoveListener(Close);
        Close();
    }

    public bool TryOpen()
    {
        if (IsOpen || !CanOpen) return false;

        var (sales, tips, refunds) = ledger.GetTotals();
        balanceText.text = Money(player.HasMoney());
        salesText.text = Money(sales);
        tipsText.text = Money(tips);
        refundsText.text = Money(-refunds);
        totalText.text = Money(sales + tips - refunds);

        var orders = ledger.Items;
        for (int i = 0; i < orders.Count; i++)
        {
            if (i == rows.Count) rows.Add(Instantiate(rowPrefab, scroll.content, false));
            var order = orders[i];
            string name = cocktailData.TryGet(order.ServedCocktailId, out var cocktail)
                ? cocktail.Name.Ko
                : order.ServedCocktailId ?? "—";
            rows[i].Show(name, order.FinalGrade.ToString(), Money(order.SettlementAmount));
            rows[i].gameObject.SetActive(true);
        }
        for (int i = orders.Count; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
        emptyMessage.SetActive(orders.Count == 0);

        pauseHandle = GamePause.Acquire();
        IsOpen = true;
        panelGroup.alpha = fadeDuration > 0f ? 0f : 1f;
        root.SetActive(true);
        if (fadeDuration > 0f) fade = panelGroup.DOFade(1f, fadeDuration).SetUpdate(true);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1f;
        EventSystem.current?.SetSelectedGameObject(null);
        OpenChanged?.Invoke();
        return true;
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        fade?.Kill();
        fade = null;
        scroll.StopMovement();
        root.SetActive(false);
        pauseHandle?.Dispose();
        pauseHandle = null;
        EventSystem.current?.SetSelectedGameObject(null);
        OpenChanged?.Invoke();
    }

    static string Money(long amount) => amount.ToString("N0", CultureInfo.InvariantCulture) + " G";
}
