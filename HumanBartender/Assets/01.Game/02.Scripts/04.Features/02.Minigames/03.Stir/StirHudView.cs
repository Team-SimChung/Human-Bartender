using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아레나 바깥의 표시 전부 — 상단바 스탯, 라운드 타이머 카드, 사선 게이지, 시작 오버레이.
/// 판정은 하지 않고 StirManager가 넘겨준 값을 그리기만 한다.
///
/// 프로토타입(Stir_Minigame.html)의 화면 구성을 그대로 따른다. 참조가 비어 있어도 게임은 돌아가므로
/// 필요 없는 요소는 인스펙터에서 비워두면 된다.
/// </summary>
public class StirHudView : MonoBehaviour
{
    [Header("Top Stats")]
    [SerializeField] TMP_Text elapsedText;
    [SerializeField] TMP_Text circleText;
    [SerializeField] TMP_Text comboText;

    [Header("Round Timer Card")]
    [SerializeField] TMP_Text roundLimitText;
    [SerializeField] TMP_Text roundTimeText;
    [Tooltip("남은 시간을 그리는 가로 막대. Filled/Horizontal이어야 한다.")]
    [SerializeField] Image roundTimeFill;
    [SerializeField] Color roundNormalColor = new Color(0.87f, 1f, 0.42f);
    [SerializeField] Color roundDangerColor = new Color(1f, 0.39f, 0.47f);
    [Tooltip("경고색으로 바뀌는 잔여 비율. 프로토타입과 같은 34%.")]
    [SerializeField] float dangerRatio = 0.34f;

    [Header("Gauge")]
    [SerializeField] StirGaugeView gauge;

    [Header("Overlay")]
    [Tooltip("시작 대기 카드. 첫 W 입력에 꺼진다.")]
    [SerializeField] GameObject startOverlay;
    [SerializeField] TMP_Text judgeText;

    /// <summary>
    /// 공통 표시와 겹치는 상단 스탯(판정 수·콤보)을 감춘다.
    /// 같은 정보가 두 군데 뜨는 걸 막기 위해, 기믹 큐가 돌릴 때 한 번 부른다.
    ///
    /// 잔 주변 2초 게이지와 사선 진행 게이지는 감추지 않는다 — 스터에만 있는 판정 표시라
    /// 공통 표시에 자리가 없다.
    /// </summary>
    public void HideStatsSharedWithCommonHud()
    {
        if (circleText != null) circleText.gameObject.SetActive(false);
        if (comboText != null) comboText.gameObject.SetActive(false);
    }

    public void SetRoundLimit(float seconds)
    {
        if (roundLimitText != null) roundLimitText.text = $"{seconds:0.00} SEC";
    }

    /// <summary>첫 입력부터의 총 소요시간을 mm:ss로 표시한다.</summary>
    public void SetElapsed(float seconds)
    {
        if (elapsedText == null) return;

        int totalSeconds = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        elapsedText.text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    public void SetCircle(int done, int total)
    {
        if (circleText != null) circleText.text = $"{done} / {total}";
    }

    public void SetCombo(int combo, int best)
    {
        if (comboText != null) comboText.text = $"{combo} / {best}";
    }

    public void SetRoundTime(float remainSeconds, float ratio)
    {
        if (roundTimeText != null) roundTimeText.text = $"{Mathf.Max(0f, remainSeconds):0.00}";

        if (roundTimeFill != null)
        {
            roundTimeFill.fillAmount = Mathf.Clamp01(ratio);
            roundTimeFill.color = ratio < dangerRatio ? roundDangerColor : roundNormalColor;
        }
    }

    public void SetGaugeResult(int index, bool success) => gauge?.SetResult(index, success);

    public void SetGaugeNextSegment(int index) => gauge?.SetNextSegment(index);

    public void ResetGauge() => gauge?.ResetAll();

    public void SetStartOverlay(bool visible)
    {
        if (startOverlay != null) startOverlay.SetActive(visible);
    }

    public void SetJudge(string message)
    {
        if (judgeText != null) judgeText.text = message;
    }
}
