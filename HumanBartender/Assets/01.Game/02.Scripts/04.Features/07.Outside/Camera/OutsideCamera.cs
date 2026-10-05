using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

/// <summary>실외 씬에서 카메라가 취할 수 있는 모드(플레이어 추적/엘리베이터 진입/엘리베이터 복귀).</summary>
public enum EOutsideCameraMode
{
    Follow = 0,
    Elevator = 1,
    ElevatorReturn = 2,
}

/// <summary>카메라 모드 하나에 대한 전환 설정(추적 대상/오프셋/해상도 및 각각의 전환 커브·시간).</summary>
[System.Serializable]
public class OutsideCameraOption
{
    public EOutsideCameraMode cameraMode;
    public AnimationCurve offsetCurve = AnimationCurve.Linear(0, 0, 1, 1);
    public AnimationCurve resolutionCurve = AnimationCurve.Linear(0, 0, 1, 1);

    public Transform cameraParent;
    public Vector3 cameraOffset;

    public float offsetDuration = 1f;
    public float resolutionDuration = 1f;

    public bool isChangeResolution;
    public ECameraZoomType targetResolution;
}

/// <summary>
/// 실외 씬 전용 카메라 모드 전환기. CameraControllerNew(cameraZoom)를 통해 추적 대상/오프셋/해상도를
/// 등록된 모드(cameraOptions) 값으로 전환시킨다.
/// </summary>
public class OutsideCamera : MonoBehaviour
{
    [Header("CameraOption")]
    [SerializeField] OutsideCameraOption[] cameraOptions = System.Array.Empty<OutsideCameraOption>();
    [SerializeField] CameraControllerNew cameraZoom;


    private Dictionary<EOutsideCameraMode, OutsideCameraOption> _cameraMap;
    

    void Awake() => EnsureOptions();

    void EnsureOptions()
    {
        if (_cameraMap != null) return;
        _cameraMap = new Dictionary<EOutsideCameraMode, OutsideCameraOption>(cameraOptions.Length);
        foreach (var slot in cameraOptions)
        {
            if (!_cameraMap.TryAdd(slot.cameraMode, slot))
                throw new System.InvalidOperationException("Duplicate camera mode: " + slot.cameraMode);
        }
    }

    /// <summary>정수 n을 EOutsideCameraMode로 변환해 해당 카메라 옵션을 적용한다 (애니메이션 이벤트 등에서 int로 호출하기 위함).</summary>
    public void ChangeCameraMode(int n)
    {
        EnsureOptions();
        if (!_cameraMap.TryGetValue((EOutsideCameraMode)n, out var option))
            throw new System.ArgumentOutOfRangeException(nameof(n));
        ExcuteCameraOption(option);
    }

    /// <summary>추적 대상 부모 전환, 오프셋 전환, 해상도 전환을 순서대로(또는 조건에 따라 순서 바꿔) 실행한다.</summary>
    public void ExcuteCameraOption(OutsideCameraOption mode)
    {
        // 오프셋은 선택한 부모의 로컬 좌표로 해석한다.
        cameraZoom.FollowTarget(mode.cameraParent);
        cameraZoom.TransitionFollowOffset(mode.cameraOffset, mode.offsetDuration, mode.offsetCurve);
        if (mode.isChangeResolution)
            cameraZoom.TransitionCameraZoom(mode.targetResolution, mode.resolutionDuration, mode.resolutionCurve);

    }
}
