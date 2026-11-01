using UnityEngine;


/// <summary>UI 추적 요소의 종류(상호작용 버튼 / 말풍선). 종류별로 다른 오프셋(ButtonOffset/TextOffset)을 사용한다.</summary>
public enum ETrackerType
{
    InteractButton,
    TextBubble,
}

public interface UITracker
{
    void SetTrackedTarget(ITrackedble target);
}


/// <summary>
/// 월드 공간의 ITrackedble 대상을 화면 좌표로 변환해 UI(RectTransform)를 그 위치에 따라다니게 하는 베이스 클래스.
/// ITrackedbleEvent 채널을 구독해 추적 대상을 갱신받는다.
/// </summary>
public class UIOutsideTracker : MonoBehaviour, UITracker
{
    [SerializeField] protected ITrackedbleEvent OnTracked;

    [SerializeField] protected ETrackerType Type;
    [SerializeField] protected RectTransform target;
    [SerializeField] protected Camera cam;
    [SerializeField] protected float referenceOrthoSize = 1.35f;

    protected ITrackedble trackedTarget;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public virtual void OnEnable()
    {
        if(OnTracked != null)
            OnTracked.OnRaised += SetTrackedTarget;
    }

    public virtual void OnDisable()
    {
        if (OnTracked != null)
            OnTracked.OnRaised -= SetTrackedTarget;
    }


    /// <summary>추적 대상이 유효하지 않으면 UI를 숨기고 추적을 해제하며, 유효하면 위치를 갱신한다.</summary>
    public virtual void LateUpdate()
    {
        if (trackedTarget == null) return;

        if (!trackedTarget.IsAvaliable)
        {
            target.gameObject.SetActive(false);
            trackedTarget = null;
            return;
        }

        UpdatePosition();
    }

    /// <summary>추적을 중단하고 UI를 숨긴다.</summary>
    public void StopTracking()
    {
        trackedTarget = null;
        target.gameObject.SetActive(false);
    }
    public void SetTrackedTarget(ITrackedble target)
    {
        trackedTarget = target;
    }

    /// <summary>
    /// 대상의 월드 좌표를 화면 좌표로 변환해 트래커 타입별 오프셋(ButtonOffset/TextOffset)을 적용한다.
    /// 오소그래픽 카메라에서는 현재 orthographicSize에 비례해 오프셋을 보정한다(기준 크기 대비 축소/확대).
    /// </summary>
    public void UpdatePosition()
    {
        Vector2 offset =
            Type == ETrackerType.InteractButton ? trackedTarget.ButtonOffset : trackedTarget.TextOffset;

        if (cam.orthographic)
            offset *= referenceOrthoSize / cam.orthographicSize;

        Vector3 screenPos = cam.WorldToScreenPoint(trackedTarget.Transform.position);
        
        screenPos.x += offset.x;
        screenPos.y += offset.y;
        screenPos.z = 10f;

        target.position = screenPos;
    }
}
