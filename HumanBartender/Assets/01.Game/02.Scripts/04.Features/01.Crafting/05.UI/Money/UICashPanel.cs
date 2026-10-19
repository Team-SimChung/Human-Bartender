using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// 상단 재화(현금) 표시 패널. 재화 증감 시 CurrencyGainText 팝업을 생성해 메인 텍스트 쪽으로 날아가게 하고,
/// 도착(병합) 시점에 실제 금액을 반영하며 텍스트에 펀치 효과를 준다.
/// </summary>
public class UICashPanel : MonoBehaviour
{
    [Header("Main Display")]
    [SerializeField] private TMP_Text amountText;        // 메인 "191V" 텍스트
    [SerializeField] private string suffix = "";        // 단위 표기
    [SerializeField] private string numberFormat = "N0"; // "N0" => 1,234 / "0" => 1234
    [SerializeField] private int currentAmount = 0;      // 현재 보유량

    [Header("Gain Popups")]
    [SerializeField] private Canvas canvas;              // 소속 Canvas (비우면 부모에서 자동 검색)
    [SerializeField] private GameObject cashLine;
    [SerializeField] private CurrencyGainText gainPrefab; // 팝업 프리팹
    [SerializeField] private RectTransform gainLayer;     // 팝업이 생성될 컨테이너 (★ LayoutGroup 없는 빈 RectTransform)
    [SerializeField] private RectTransform mergeTarget;   // 병합 도착점 (비우면 amountText 사용)
    [SerializeField] private Vector2 spawnOffset = new Vector2(0f, -55f); // 도착점 기준 시작 위치(아래쪽)
    [SerializeField] private float stackSpacing = 38f;    // 연속 획득 시 세로 간격

    [Header("Punch On Merge")]
    [SerializeField] private float punchScale = 1.12f;    // 병합 시 메인 텍스트 살짝 커지는 효과
    [SerializeField] private float punchTime = 0.12f;

    [Header("Cash Line")]
    [SerializeField] private float lineHideDelay = 1f;    // 마지막 팝업 종료 후 cashLine 숨김 지연(초)

    private int activeGainCount;
    private Coroutine punchRoutine;
    private Coroutine hideLineRoutine;
    [Inject] IPlayerDataReader playerData;

    private void Awake()
    {
        if (mergeTarget == null && amountText != null)
            mergeTarget = amountText.rectTransform;

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        // 시작 시에는 숨김 상태로
        if (cashLine != null)
            cashLine.SetActive(false);
    }

    private void OnEnable() => SyncFromModel();
    private void Start() => SyncFromModel();

    private void SyncFromModel()
    {
        if (playerData != null) currentAmount = playerData.HasMoney();
        Refresh();
    }

    /// <summary>재화 획득(+) 또는 차감(-). 팝업이 메인 텍스트에 도달하면 값이 반영된다.</summary>
    public void AddCurrency(int amount)
    {
        SyncFromModel();
        if (amount == 0 || gainPrefab == null || gainLayer == null) return;

        // cashLine 표시 + 대기 중인 숨김 타이머 취소 (연속 호출 대응)
        ShowCashLine();

        Logger.Log($"amount {amount}");

        // 메인 텍스트의 월드 위치를 gainLayer 로컬 좌표로 변환 → 좌표계 일치
        Vector2 target = WorldToLocal(gainLayer, mergeTarget.position, null);
        Vector2 start = target + spawnOffset + new Vector2(0f, -stackSpacing * activeGainCount);
        activeGainCount++;

        CurrencyGainText popup = Instantiate(gainPrefab, gainLayer);
        popup.Play(amount, suffix, start, target, () =>
        {
            SyncFromModel();
            Punch();
            activeGainCount = Mathf.Max(0, activeGainCount - 1);

            // 진행 중인 팝업이 모두 끝났을 때만 숨김 타이머 시작
            if (activeGainCount == 0)
                ScheduleHideCashLine();
        });
    }

    /// <summary>팝업 없이 즉시 값 설정 (세이브 로드 등).</summary>
    public void SetAmount(int amount)
    {
        currentAmount = playerData != null ? playerData.HasMoney() : amount;
        Refresh();
    }

    public int CurrentAmount => currentAmount;

    /// <summary>현재 보유량(currentAmount)을 텍스트에 반영한다.</summary>
    private void Refresh()
    {
        if (amountText != null)
            amountText.text = currentAmount.ToString(numberFormat) + suffix;
    }

    // ─────────────────────────────── Cash Line ───────────────────────────────

    /// <summary>재화 변동 표시줄(cashLine)을 켜고, 예정된 숨김 타이머가 있으면 취소한다.</summary>
    private void ShowCashLine()
    {
        // 진행 중인 숨김 타이머가 있으면 취소 (연속 호출 시 깜빡임 방지)
        if (hideLineRoutine != null)
        {
            StopCoroutine(hideLineRoutine);
            hideLineRoutine = null;
        }

        if (cashLine != null && !cashLine.activeSelf)
            cashLine.SetActive(true);
    }

    /// <summary>lineHideDelay초 후 cashLine을 숨기는 코루틴을 예약한다.</summary>
    private void ScheduleHideCashLine()
    {
        if (hideLineRoutine != null) StopCoroutine(hideLineRoutine);
        hideLineRoutine = StartCoroutine(HideCashLineCo());
    }

    private IEnumerator HideCashLineCo()
    {
        yield return new WaitForSeconds(lineHideDelay);

        if (cashLine != null)
            cashLine.SetActive(false);

        hideLineRoutine = null;
    }

    // ─────────────────────────────── Punch ───────────────────────────────

    /// <summary>금액 텍스트에 확대/축소 펀치 효과를 재생한다.</summary>
    private void Punch()
    {
        if (amountText == null) return;
        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(PunchCo(amountText.rectTransform));
    }

    /// <summary>rt를 punchScale까지 키웠다가 원래 크기로 되돌리는 펀치 애니메이션 코루틴.</summary>
    private IEnumerator PunchCo(RectTransform rt)
    {
        Vector3 baseScale = Vector3.one;
        float half = Mathf.Max(0.001f, punchTime * 0.5f);
        float t = 0f;
        while (t < half) { t += Time.deltaTime; rt.localScale = Vector3.Lerp(baseScale, baseScale * punchScale, t / half); yield return null; }
        t = 0f;
        while (t < half) { t += Time.deltaTime; rt.localScale = Vector3.Lerp(baseScale * punchScale, baseScale, t / half); yield return null; }
        rt.localScale = baseScale;
        punchRoutine = null;
    }

    /// <summary>월드 좌표를 area(RectTransform) 기준 로컬 좌표로 변환한다 (스크린 좌표를 경유).</summary>
    private static Vector2 WorldToLocal(RectTransform area, Vector3 world, Camera cam)
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, cam, out Vector2 local);
        return local;
    }
}
