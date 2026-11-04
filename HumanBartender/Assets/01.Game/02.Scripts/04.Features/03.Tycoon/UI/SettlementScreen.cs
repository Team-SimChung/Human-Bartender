using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum SettlementAction { Confirm, Retry }

/// <summary>정산 결과 표시와 사용자 선택만 담당한다. 계산, 재화 변경, 씬 이동은 하지 않는다.</summary>
public sealed class SettlementScreen : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] GameObject root;
    [SerializeField] GameObject settlementPanel;
    [SerializeField] GameObject failurePanel;
    [SerializeField] Button confirmButton;
    [SerializeField] Button retryButton;

    [Header("Settlement")]
    [SerializeField] TMP_Text dayText;
    [SerializeField] TMP_Text incomeText;
    [SerializeField] TMP_Text servedCountText;
    [SerializeField] TMP_Text salesText;
    [SerializeField] TMP_Text tipsText;
    [SerializeField] TMP_Text refundsText;
    [SerializeField] TMP_Text balanceBeforeText;
    [SerializeField] TMP_Text upkeepText;
    [SerializeField] TMP_Text balanceAfterText;
    [SerializeField] TMP_Text noticeText;

    [Header("Insufficient balance")]
    [SerializeField] TMP_Text failureDayText;
    [SerializeField] TMP_Text failureBalanceText;
    [SerializeField] TMP_Text failureUpkeepText;
    [SerializeField] TMP_Text shortfallText;
    [SerializeField] TMP_Text failureNoticeText;

    UniTaskCompletionSource<SettlementAction> pending;
    bool canAfford;
    bool submitted;
    string normalNotice;
    string retryNotice;

    public bool IsOpen => root != null && root.activeSelf;

    void Awake()
    {
        normalNotice = noticeText != null ? noticeText.text : "";
        retryNotice = failureNoticeText != null ? failureNoticeText.text : "";
        if (root != null) root.SetActive(false);
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (retryButton != null) retryButton.onClick.AddListener(Retry);
    }

    public async UniTask<SettlementAction> WaitForActionAsync(DaySettlementSummary summary,
        CancellationToken cancellationToken, string error = null)
    {
        if (pending != null) throw new InvalidOperationException("정산 입력을 이미 기다리고 있습니다.");
        ValidateReferences();
        cancellationToken.ThrowIfCancellationRequested();
        var completion = new UniTaskCompletionSource<SettlementAction>();
        pending = completion;
        submitted = false;
        canAfford = summary.CanAfford;
        Render(summary, error);
        root.SetActive(true);
        confirmButton.interactable = canAfford;
        retryButton.interactable = !canAfford;
        try { return await completion.Task.AttachExternalCancellation(cancellationToken); }
        finally
        {
            pending = null;
            if (this != null) SetBusy();
        }
    }

    void Render(DaySettlementSummary summary, string error)
    {
        settlementPanel.SetActive(summary.CanAfford);
        failurePanel.SetActive(!summary.CanAfford);
        dayText.text = $"DAY {summary.Day:00} / CLOSING TIME";
        failureDayText.text = $"DAY {summary.Day:00} / BAR CLOSED";
        incomeText.text = Money(summary.Income, signed: true);
        servedCountText.text = $"제공한 칵테일 {summary.ServedCount}잔";
        salesText.text = Money(summary.Sales);
        tipsText.text = Money(summary.Tips);
        refundsText.text = Money(-summary.Refunds);
        balanceBeforeText.text = Money(summary.BalanceBefore);
        upkeepText.text = Money(-summary.Upkeep);
        balanceAfterText.text = Money(summary.BalanceAfter);
        failureBalanceText.text = Money(summary.BalanceBefore);
        failureUpkeepText.text = Money(summary.Upkeep);
        shortfallText.text = Money(summary.Shortfall);
        noticeText.text = error ?? normalNotice;
        failureNoticeText.text = error ?? retryNotice;
    }

    static string Money(long amount, bool signed = false) =>
        (signed && amount > 0 ? "+" : "") + amount.ToString("#,0", CultureInfo.InvariantCulture) + " G";

    void Confirm() => Submit(SettlementAction.Confirm);
    void Retry() => Submit(SettlementAction.Retry);

    void Submit(SettlementAction action)
    {
        if (pending == null || submitted || UIDisplayOptions.IsOptionOpen ||
            (action == SettlementAction.Confirm) != canAfford) return;
        submitted = true;
        SetBusy();
        pending.TrySetResult(action);
    }

    void SetBusy()
    {
        if (confirmButton != null) confirmButton.interactable = false;
        if (retryButton != null) retryButton.interactable = false;
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    void OnDisable()
    {
        pending?.TrySetCanceled();
        Hide();
    }

    void OnDestroy()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
        if (retryButton != null) retryButton.onClick.RemoveListener(Retry);
    }

    public void ValidateReferences()
    {
        if (root == null || root == gameObject || settlementPanel == null || failurePanel == null ||
            confirmButton == null || retryButton == null || dayText == null || incomeText == null ||
            servedCountText == null || salesText == null || tipsText == null || refundsText == null ||
            balanceBeforeText == null || upkeepText == null || balanceAfterText == null || noticeText == null ||
            failureDayText == null || failureBalanceText == null || failureUpkeepText == null ||
            shortfallText == null || failureNoticeText == null)
            throw new InvalidOperationException("SettlementScreen의 UI 참조를 연결해 주세요. 컴포넌트는 활성 Canvas에 둡니다.");
    }
}
