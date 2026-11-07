using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 바 화면 왼쪽 아래 좌석 세 칸의 표시만 맡는다. 무엇을 보여 줄지는 같은 오브젝트의 BarSeatHudPresenter가 정한다.
///
/// Play 씬 05.UI/Canvas_BarSeatHud/BarSeatHudRoot에 저장되어 있고, 수정은 Inspector에서 한다(960×540 기준 정수 값).
/// 튜토리얼 오버레이는 Indicators·키 안내를 강조 대상으로 빌려 쓰고, 좌석 수업 동안 SetExploration과 Suppressed로 상태를 알린다.
/// </summary>
public sealed class BarSeatStatusView : MonoBehaviour
{
    public static readonly Color EmptyColor = new(.29f, .31f, .33f);
    public static readonly Color OccupiedColor = new(.54f, .8f, .73f);
    public static readonly Color SelectedColor = new(.79f, 1f, 1f);
    public static readonly Color WaitingColor = new(1f, .65f, .28f);
    public static readonly Color DangerColor = new(1f, .38f, .36f);

    const float BlinkInterval = .4f;
    const float BlinkAlpha = .24f;
    const float DimmedKeyAlpha = .3f;

    [Header("Layout")]
    [SerializeField] RectTransform indicators;
    [Tooltip("왼쪽·가운데·오른쪽 좌석 점. 흰 스프라이트에 상태 색을 입힌다.")]
    [SerializeField] Image[] dots = new Image[3];
    [Tooltip("지금 보고 있는 좌석을 두르는 링. 점과 같은 순서다.")]
    [SerializeField] Image[] rings = new Image[3];

    [Header("Seat keys")]
    [Tooltip("좌석 둘러보기 수업에서만 켜지는 좌우 키 안내.")]
    [SerializeField] RectTransform leftKey;
    [SerializeField] RectTransform rightKey;
    [SerializeField] TMP_Text leftKeyLabel;
    [SerializeField] TMP_Text rightKeyLabel;

    public RectTransform Indicators => indicators;
    public RectTransform LeftKey => leftKey;
    public RectTransform RightKey => rightKey;

    /// <summary>튜토리얼 전면 안내 중에는 좌석 표시를 숨긴다.</summary>
    public bool Suppressed { get; set; }
    public bool IsExploring { get; private set; }
    public ESlotType ExploringSeat { get; private set; }

    void Awake() => SetExploration(false, ESlotType.Right, false);

    void OnEnable()
    {
        GameKeyBindings.Changed += RefreshKeyLabels;
        RefreshKeyLabels();
    }

    void OnDisable() => GameKeyBindings.Changed -= RefreshKeyLabels;

    /// <summary>좌석 둘러보기 수업 상태. 켜져 있으면 선택 좌석을 수업이 정하고, 좌우 키 안내를 보여 준다.</summary>
    public void SetExploration(bool active, ESlotType seat, bool cameraMoving)
    {
        IsExploring = active;
        ExploringSeat = seat;
        leftKey.gameObject.SetActive(active);
        rightKey.gameObject.SetActive(active);
        if (!active) return;
        // 끝 좌석에서는 더 갈 수 없는 쪽 키를, 카메라가 움직이는 동안에는 양쪽 키를 흐리게 한다.
        leftKeyLabel.alpha = seat == ESlotType.Left || cameraMoving ? DimmedKeyAlpha : 1f;
        rightKeyLabel.alpha = seat == ESlotType.Right || cameraMoving ? DimmedKeyAlpha : 1f;
    }

    public void SetVisible(bool visible)
    {
        if (indicators.gameObject.activeSelf != visible) indicators.gameObject.SetActive(visible);
    }

    /// <summary>좌석 한 칸의 색과 선택 링. 도착·퇴장은 깜빡인다.</summary>
    public void SetSeat(int index, EBarSeatSignal signal, bool selected, float secondsSinceChange)
    {
        if (rings[index].gameObject.activeSelf != selected) rings[index].gameObject.SetActive(selected);
        Color tint = SignalColor(signal, selected);
        if ((signal == EBarSeatSignal.Arrival || signal == EBarSeatSignal.Departing) &&
            (int)(secondsSinceChange / BlinkInterval) % 2 == 1) tint.a = BlinkAlpha;
        if (dots[index].color != tint) dots[index].color = tint;
    }

    void RefreshKeyLabels()
    {
        if (leftKeyLabel != null) leftKeyLabel.text = "< " + GameKeyBindings.DisplayName(GameControl.PreviousSeat);
        if (rightKeyLabel != null) rightKeyLabel.text = GameKeyBindings.DisplayName(GameControl.NextSeat) + " >";
    }

    public static Color SignalColor(EBarSeatSignal signal, bool selected = false) => signal switch
    {
        EBarSeatSignal.Empty => EmptyColor,
        EBarSeatSignal.Waiting => WaitingColor,
        EBarSeatSignal.LeavingSoon or EBarSeatSignal.Departing => DangerColor,
        _ => selected ? SelectedColor : OccupiedColor
    };
}
