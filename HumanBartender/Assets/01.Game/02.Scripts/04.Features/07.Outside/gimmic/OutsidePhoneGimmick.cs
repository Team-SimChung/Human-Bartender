using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class OutsidePhoneGimmick : MonoBehaviour
{
    [SerializeField] private RectTransform dial;

    // 오른쪽 0도, 위 90도, 왼쪽 180도, 아래 -90도
    [SerializeField] private float endAngle = -30f;

    // 초당 복귀 각도
    [SerializeField, Min(1f)] private float returnSpeed = 180f;
    [SerializeField] private UnityEvent<string> onNumberEntered;


    private RectTransform reference;
    private Quaternion initialRotation;

    private bool isDragging;
    private bool isReturning;
    private bool validInput;
    private int activePointerId;
    private string selectedNumber;

    private float previousMouseAngle;
    private float turnedAngle;
    private float allowedAngle;

    private void Awake()
    {
        reference = (RectTransform)dial.parent;
        initialRotation = dial.localRotation;
    }

    public void BeginDial(
        string number, RectTransform hole, PointerEventData data)
    {
        if (isDragging || isReturning)
            return;

        if (!TryGetMouseAngle(data, out float mouseAngle))
            return;

        // 숫자 구멍 중심의 각도 → 고정 스토퍼까지의 거리
        Vector3 holeCenter = hole.TransformPoint(hole.rect.center);
        Vector2 direction = 
            (Vector2)reference.InverseTransformPoint(holeCenter)
            - GetCenter();

        float holeAngle =
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        allowedAngle = Mathf.Repeat(holeAngle - endAngle, 360f);

        selectedNumber = number;
        activePointerId = data.pointerId;
        previousMouseAngle = mouseAngle;
        turnedAngle = 0f;
        validInput = false;
        isDragging = true;
    }

    public void DragDial(PointerEventData data)
    {
        if (!isDragging || data.pointerId != activePointerId)
            return;

        if (!TryGetMouseAngle(data, out float currentAngle))
            return;

        float delta =
            Mathf.DeltaAngle(previousMouseAngle, currentAngle);

        // 시계 방향 입력을 양수로 누적
        turnedAngle = Mathf.Clamp(
            turnedAngle - delta, 0f, allowedAngle);

        // 제한에 걸려도 마우스 기준점은 갱신
        previousMouseAngle = currentAngle;

        ApplyRotation();
    }

    public void EndDial(PointerEventData data)
    {
        if (!isDragging || data.pointerId != activePointerId)
            return;

        isDragging = false;

        // 놓는 순간 스토퍼에 도착해 있어야 유효
        validInput = allowedAngle > 2f
            && turnedAngle >= allowedAngle - 1f;

        isReturning = true;
    }

    private void Update()
    {
        if (!isReturning)
            return;

        turnedAngle = Mathf.MoveTowards(
            turnedAngle, 0f, returnSpeed * Time.deltaTime);

        ApplyRotation();

        if (turnedAngle > 0f)
            return;

        isReturning = false;

        bool shouldNotify = validInput;
        validInput = false;

        if (shouldNotify)
            play(selectedNumber);
    }
    private void play(string val)
    {
        Debug.Log($"{val} 입력");
        onNumberEntered?.Invoke(val);
    }
    private void ApplyRotation()
    {
        dial.localRotation = initialRotation
            * Quaternion.Euler(0f, 0f, -turnedAngle);
    }

    private Vector2 GetCenter()
    {
        return reference.InverseTransformPoint(dial.position);
    }

    private bool TryGetMouseAngle(
        PointerEventData data, out float angle)
    {
        angle = 0f;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            reference, data.position, data.pressEventCamera,
            out Vector2 point))
            return false;

        Vector2 direction = point - GetCenter();

        // 중심에서는 각도를 안정적으로 구할 수 없음
        if (direction.sqrMagnitude < 0.01f)
            return false;

        angle = Mathf.Atan2(direction.y, direction.x)
            * Mathf.Rad2Deg;

        return true;
    }

    private void OnDisable()
    {
        isDragging = false;
        isReturning = false;
        validInput = false;
        turnedAngle = 0f;

        if (dial != null)
            dial.localRotation = initialRotation;
    }
}
