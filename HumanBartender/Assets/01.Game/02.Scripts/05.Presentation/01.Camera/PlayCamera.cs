using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>슬롯 타입(Left/Right/Middle)별로 카메라(cameraAnchor)가 따라갈 Transform과 오프셋을 매핑하는 데이터.</summary>
[System.Serializable]
public class SlotCameraOption
{
    public ESlotType slotType = ESlotType.Middle;
    public Transform cameraParent;
    public Vector3 cameraOffset = Vector3.zero;
}

/// <summary>
/// Play(Bar) 씬 전용 카메라 슬롯 전환기. CameraControllerNew(cameraZoom)를 통해 cameraAnchor를
/// 슬롯별 Transform(cameraParent)의 자식으로 붙이고 오프셋을 전환시킨다. OutsideCamera와 동일한 패턴.
///
/// 여기서 옮기는 것은 가로 위치뿐이고 cameraAnchor의 y는 늘 0이다. 세로 구도는 CM_GamePlay의
/// Position Composer가 렌즈 크기를 읽어 잡는다 — 카메라 중심 y = Target Offset y + 2·ortho·Screen Position y.
/// 두 값은 양 끝 화면을 직선으로 잇는다.
/// - 960×540(ortho 2.7): 중심 y 0.265. 바 테이블 윗선(배경 위에서 500px)이 화면 높이의 534/720(웹 bar-views.js cameraLayout).
/// - 1280×720(ortho 3.6): 중심 y 0.36. 화면 위쪽이 배경 위쪽 끝에 맞닿는다(QA 시안). 웹 규칙대로면 배경 위로 34px 넘어간다.
/// 그 사이는 직선이라 줌 내내 위·아래·좌·우 가장자리가 같은 비율로 움직이고, Confiner2D에 걸리지 않는다.
/// 1280×720에서 테이블 윗선만 맞추고 위쪽을 Confiner로 멈추면 줌 3/4 지점에서 위쪽이 먼저 멈춰, 세로가 먼저
/// 끝나고 가로가 뒤따르는 것처럼 보인다. 그래서 끝 화면 값도 Confiner 한계(0.36)를 넘지 않게 잡았다.
/// </summary>
public class PlayCamera : MonoBehaviour, ISlotCamera
{
    [Header("References")]
    [SerializeField] private CameraControllerNew cameraZoom;

    [Header("Slot Options (1부)")]
    [Tooltip("1부 손님 자리별 카메라 위치. 2부는 이 표를 쓰지 않는다 — 자리도 프레임도 다르다.")]
    [SerializeField] private SlotCameraOption[] slotOptions = System.Array.Empty<SlotCameraOption>();
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private static readonly ESlotType[] SlotOrder = { ESlotType.Left, ESlotType.Middle, ESlotType.Right };

    private Dictionary<ESlotType, SlotCameraOption> _slotMap;
    private int _currentSlotIndex = 1; // Middle

    public ESlotType SelectedSeat => SlotOrder[_currentSlotIndex];
    public bool IsMoving => cameraZoom.IsMoving;
    public AnimationCurve Ease => ease;
    public event System.Action SelectedSeatChanged;

    private void Awake() => EnsureSlots();

    void EnsureSlots()
    {
        if (_slotMap != null) return;
        _slotMap = new Dictionary<ESlotType, SlotCameraOption>(slotOptions.Length);
        foreach (var opt in slotOptions)
        {
            if (!_slotMap.TryAdd(opt.slotType, opt))
                throw new System.InvalidOperationException("Duplicate camera slot: " + opt.slotType);
        }
    }

    /// <summary>지정된 슬롯의 Transform으로 cameraAnchor를 dur초 동안 부드럽게 이동시킨다.</summary>
    public void MoveToSlot(ESlotType slot, float dur = 1f)
    {
        EnsureSlots();
        var opt = _slotMap[slot];
        cameraZoom.FollowTarget(opt.cameraParent);
        cameraZoom.TransitionFollowOffset(opt.cameraOffset, dur, ease);

        int idx = System.Array.IndexOf(SlotOrder, slot);
        if (idx >= 0 && idx != _currentSlotIndex)
        {
            _currentSlotIndex = idx;
            SelectedSeatChanged?.Invoke();
        }
    }

    /// <summary>
    /// cameraAnchor를 world x로 dur초 동안 옮긴다. 2부가 계산해 온 좌표를 그대로 받는다.
    ///
    /// 슬롯 앵커에 붙지 않으므로 따라다니던 부모가 있으면 먼저 뗀다. 붙은 채로 두면 넘겨받은 값이
    /// 그 부모 기준으로 읽혀 엉뚱한 곳에 선다.
    /// </summary>
    public void MoveToX(float worldX, float dur = 1f)
        => MoveToXAsync(worldX, dur).Forget(Debug.LogException);

    public UniTask MoveToXAsync(float worldX, float dur = 1f, CancellationToken token = default)
    {
        cameraZoom.Unfollow();
        return cameraZoom.TransitionFollowOffsetAsync(new Vector3(worldX, 0f, 0f), dur, ease, token);
    }

    /// <summary>현재 슬롯 기준으로 한 칸 옆(step: -1=왼쪽, +1=오른쪽) 슬롯으로 이동시킨다.</summary>
    public void MoveAdjacent(int step, float dur = 1f)
    {
        int next = Mathf.Clamp(_currentSlotIndex + step, 0, SlotOrder.Length - 1);
        MoveToSlot(SlotOrder[next], dur);
    }
}
