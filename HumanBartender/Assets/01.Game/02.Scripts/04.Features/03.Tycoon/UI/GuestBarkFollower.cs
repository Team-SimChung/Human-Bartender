using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 영업(1부) 손님 말풍선을 화면 캔버스(Canvas_Dialogue/GuestBarkRoot)에 두고 좌석 기준점을 따라가게 한다.
///
/// 카메라가 그 프레임의 위치를 정한 뒤(CinemachineBrain의 LateUpdate) 자리를 잡고, 꼬리는 SpeechBubbleVisual(10002)이 그린다.
/// Camera.WorldToScreenPoint는 쓰지 않는다. PixelPerfectCamera가 그리기 직전에 반올림한 카메라 행렬을 넣어 두므로
/// LateUpdate에서는 지난 프레임 행렬이 남아 있어 이동 중 한 프레임씩 늦는다. 대신 카메라와 기준점을 그리기와 같은
/// 화소 격자(PixelPerfectCamera.RoundToPixel)로 맞춘 뒤 직접 옮겨, 손님 그림과 말풍선의 간격이 프레임마다 흔들리지 않게 한다.
/// 화면 밖으로 나가는 좌석의 말풍선도 숨기거나 붙잡지 않고 손님과 함께 밀려 나간다. 보간은 하지 않는다.
/// </summary>
[DefaultExecutionOrder(10001)]
public sealed class GuestBarkFollower : MonoBehaviour
{
    [Serializable]
    public struct Seat
    {
        [Tooltip("좌석의 말풍선 기준점(GuestSlots/…/Guest Visual Root/BarkAnchor). 손님과 함께 움직이는 월드 위치.")]
        public Transform anchor;
        [Tooltip("그 좌석의 말풍선(GuestBarkRoot/…GuestBark). 말풍선 피벗(위 가운데)이 기준점 + 보정 위치에 놓인다.")]
        public RectTransform bubble;
    }

    [Header("Seats")]
    [Tooltip("왼쪽·가운데·오른쪽 좌석의 기준점과 말풍선. 비어 있으면 아무것도 하지 않는다.")]
    [SerializeField] Seat[] seats = Array.Empty<Seat>();
    [Tooltip("말풍선이 놓이는 화면 전체 영역(Canvas_Dialogue/GuestBarkRoot).")]
    [SerializeField] RectTransform layer;
    [Tooltip("월드를 그리는 카메라. 비우면 Main Camera를 쓴다.")]
    [SerializeField] Camera worldCamera;

    [Header("Placement")]
    [Tooltip("기준점이 화면에 놓인 곳에서 말풍선 피벗(위 가운데)까지의 거리(960×540 캔버스 단위). " +
             "기본값은 옛 월드 말풍선과 같은 자리다: 기준점 17 아래에 위 가운데를 두고, 위로 향한 꼬리가 손님을 가리킨다.")]
    [SerializeField] Vector2Int offset = new(0, -17);

    PixelPerfectCamera pixelPerfect;
    Camera pixelPerfectOwner;

    public Vector2Int Offset => offset;

    void LateUpdate()
    {
        if (seats == null || seats.Length == 0 || layer == null) return;
        var view = worldCamera != null ? worldCamera : Camera.main;
        if (view == null || !view.orthographic || view.orthographicSize <= 0) return;
        if (pixelPerfectOwner != view)
        {
            pixelPerfectOwner = view;
            pixelPerfect = view.GetComponent<PixelPerfectCamera>();
        }

        Rect area = layer.rect;
        Rect viewArea = ViewInCanvas(view.pixelRect, new Vector2(Screen.width, Screen.height), area);
        Vector2 cameraPosition = Snap(view.transform.position);
        foreach (var seat in seats)
        {
            if (seat.anchor == null || seat.bubble == null || !seat.bubble.gameObject.activeInHierarchy) continue;
            Vector2 local = Place(Snap(seat.anchor.position), cameraPosition, view.orthographicSize, viewArea, offset);
            seat.bubble.position = layer.TransformPoint(local);
        }
    }

    /// <summary>그리기와 같은 화소 격자에 맞춘 위치. PixelPerfectCamera가 없으면 그대로 둔다.</summary>
    Vector3 Snap(Vector3 world) => pixelPerfect != null && pixelPerfect.isActiveAndEnabled ? pixelPerfect.RoundToPixel(world) : world;

    /// <summary>
    /// 월드 기준점을 말풍선 영역(layer)의 로컬 좌표로 옮긴 말풍선 피벗 위치.
    /// anchorWorld·cameraWorld는 그리기와 같은 격자에 이미 맞춘 값이다. viewArea는 카메라가 그리는 화면 영역을 캔버스 좌표로 옮긴 것이다.
    /// </summary>
    internal static Vector2 Place(Vector2 anchorWorld, Vector2 cameraWorld, float orthographicSize, Rect viewArea, Vector2Int offset) =>
        viewArea.center + (anchorWorld - cameraWorld) * (viewArea.height / (2f * orthographicSize)) + offset;

    /// <summary>카메라가 그리는 화면 영역(픽셀)을 화면 전체를 덮는 캔버스 영역의 로컬 좌표로 옮긴다.</summary>
    internal static Rect ViewInCanvas(Rect pixelRect, Vector2 screenSize, Rect canvasArea)
    {
        if (screenSize.x <= 0 || screenSize.y <= 0) return canvasArea;
        Vector2 scale = new(canvasArea.width / screenSize.x, canvasArea.height / screenSize.y);
        return new Rect(canvasArea.xMin + pixelRect.xMin * scale.x, canvasArea.yMin + pixelRect.yMin * scale.y,
            pixelRect.width * scale.x, pixelRect.height * scale.y);
    }
}
