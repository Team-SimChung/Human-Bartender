using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using VContainer;

/// <summary>웹과 같은 일시 소지금 알림. 실제 잔액은 즉시 확정하고 표시 숫자만 보간한다.</summary>
public class UICashPanel : MonoBehaviour, ICurrencyPresentation
{
    [Header("Currency Notice")]
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private TMP_Text deltaText;
    [SerializeField] private RectTransform noticeRoot;
    [SerializeField] private CanvasGroup noticeGroup;
    [SerializeField] private string suffix = "";
    [SerializeField] private string numberFormat = "N0";
    [SerializeField] private int currentAmount;

    readonly CurrencyNoticeState notice = new();
    [Inject] IPlayerDataReader playerData;
    IPlayerDataReader syncedPlayerData;
    RectTransform cachedRoot;
    TMP_Text cachedDelta;
    Vector2 restingPosition;
    Vector2 deltaPosition;
    Vector3 restingScale;

    public int CurrentAmount => currentAmount;
    public bool IsAnimating => CanAnimate && amountText != null && notice.Active;
    bool CanAnimate => this != null && isActiveAndEnabled && gameObject.activeInHierarchy;

    void Awake() => notice.Reset(currentAmount);
    void OnEnable() => SetAmount(currentAmount);

    void Start()
    {
        // VContainer 주입은 Awake보다 늦다. 먼저 도착한 증감 연출을 중단하지 않는다.
        if (!notice.Active) SetAmount(currentAmount);
    }

    void Update()
    {
        if (!notice.Active) return;
        notice.Advance(Time.unscaledDeltaTime);
        Refresh();
    }

    void OnDisable()
    {
        notice.Reset(playerData != null ? playerData.HasMoney() : notice.Balance);
        Refresh();
    }

    /// <summary>같은 방향의 연속 증감은 합산하고, 반대 방향이면 새 알림으로 전환한다.</summary>
    public void AddCurrency(int amount)
    {
        if (amount == 0) return;
        int balance = playerData != null ? playerData.HasMoney() :
            (int)Math.Clamp((long)notice.Balance + amount, int.MinValue, int.MaxValue);
        if (playerData != null && !ReferenceEquals(syncedPlayerData, playerData))
        {
            // 주입 직후 Start보다 먼저 온 증감도 이전 잔액부터 올바른 방향으로 센다.
            notice.Reset((int)Math.Clamp((long)balance - amount, int.MinValue, int.MaxValue));
            syncedPlayerData = playerData;
        }
        if (!CanAnimate || amountText == null)
        {
            SetAmount(balance);
            return;
        }
        notice.Change(balance);
        Refresh();
    }

    /// <summary>로드·초기화 시에는 알림을 중단하고 확정 잔액을 즉시 반영한다.</summary>
    public void SetAmount(int amount)
    {
        notice.Reset(playerData != null ? playerData.HasMoney() : amount);
        syncedPlayerData = playerData;
        Refresh();
    }

    public UniTask WaitForAnimationsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAnimating) return UniTask.CompletedTask;
        return UniTask.WaitUntil(() => !IsAnimating, cancellationToken: cancellationToken);
    }

    void CaptureLayout()
    {
        if (noticeRoot != null && cachedRoot != noticeRoot)
        {
            cachedRoot = noticeRoot;
            restingPosition = noticeRoot.anchoredPosition;
            restingScale = noticeRoot.localScale;
        }
        if (deltaText != null && cachedDelta != deltaText)
        {
            cachedDelta = deltaText;
            deltaPosition = deltaText.rectTransform.anchoredPosition;
        }
    }

    void Refresh()
    {
        CaptureLayout();
        currentAmount = (int)Math.Clamp(Math.Floor(notice.Displayed + .5), int.MinValue, int.MaxValue);
        bool active = notice.Active && CanAnimate && amountText != null;
        if (amountText != null)
        {
            string value = currentAmount.ToString(numberFormat);
            amountText.text = value + suffix;
            amountText.fontSize = Mathf.Min(31f, 250f / Mathf.Max(1, value.Length));
            amountText.color = notice.Delta < 0 ? new Color32(255, 188, 159, 255) : new Color32(255, 229, 154, 255);
        }
        if (deltaText != null)
        {
            deltaText.text = (notice.Delta < 0 ? "−" : "+") + Math.Abs(notice.Delta).ToString(numberFormat) + suffix;
            deltaText.color = notice.Delta < 0 ? new Color32(255, 172, 158, 255) : new Color32(255, 229, 154, 255);
            deltaText.rectTransform.anchoredPosition = deltaPosition + Vector2.up * (float)Math.Min(12, notice.Elapsed * 7);
        }
        if (noticeGroup != null) noticeGroup.alpha = active ? notice.Opacity : 0f;
        if (noticeRoot == null) return;
        if (noticeRoot.gameObject != gameObject) noticeRoot.gameObject.SetActive(active);
        float entrance = 1f - Mathf.Pow(1f - Mathf.Clamp01((float)notice.Elapsed / .18f), 3f);
        noticeRoot.anchoredPosition = restingPosition + Vector2.up * (8f * (1f - entrance));
        noticeRoot.localScale = restingScale * Mathf.Lerp(.96f, 1f, entrance);
    }
}
