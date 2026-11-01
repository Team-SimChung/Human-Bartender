using UnityEngine;

/// <summary>
/// 작업 영역을 누르면 따르는 자리로 병을 가져오고, 잔 위 고정점과 마우스의 방향으로 기울인다.
/// 놓으면 빠르게 세우면서 원래 자리로 돌아간다. 유량/수량 판정은 PourManager가 담당한다.
/// </summary>
public class BottleTiltController : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] Transform bottleVisual;

    [Header("Tilt")]
    [Tooltip("기존 따르기 기믹의 최대 기울기. 유량 판정 범위는 PourManager에서 유지한다.")]
    [SerializeField, Min(0f)] float maxTiltAngle = 120f;
    [Tooltip("마우스가 가리키는 각도로 따라가는 속도(도/초).")]
    [SerializeField, Min(0f)] float tiltSpeed = 420f;
    [Tooltip("놓았을 때 세워지는 속도(도/초).")]
    [SerializeField, Min(0f)] float returnSpeed = 420f;

    [Header("Pickup")]
    [Tooltip("따르는 자리로 가져오는 속도(초당 진행률). 4이면 0.25초.")]
    [SerializeField, Min(0f)] float pickupSpeed = 4f;
    [Tooltip("원래 자리로 내려놓는 속도(초당 진행률). 3이면 약 0.33초.")]
    [SerializeField, Min(0f)] float putDownSpeed = 3f;
    [Tooltip("병을 가져오는 진행률이 이 값을 넘으면 회전을 시작한다.")]
    [SerializeField, Range(0f, 0.99f)] float tiltReadyProgress = 0.6f;

    [Header("Pour Position")]
    [Tooltip("정지 위치에서 병 중심이 올라갈 수 있는 최대 높이(부모 로컬 단위).")]
    [SerializeField, Min(0f)] float liftHeight = 1.5f;
    [Tooltip("잔 윗면에서 입구의 고정점까지 높이(병 부모 로컬 단위).")]
    [SerializeField, Min(0f)] float pourPivotHeight = 0.9f;

    const float PointerDeadZone = 0.1f;
    Vector3 restingLocalPosition;
    Quaternion restingLocalRotation;
    Vector3 mouthLocalPosition = new Vector3(0f, 0.4f, 0f);
    Vector3 cupRimParentPosition;
    float currentAngle;
    float targetAngle;
    float pickupProgress;
    bool initialized;
    bool dragging;

    public Transform BottleVisual => bottleVisual;
    public float CurrentAngle => currentAngle;

    public Vector3 PourPivotWorldPosition
    {
        get
        {
            InitializePose();
            Vector3 pivot = PourPivotParentPosition();
            return bottleVisual.parent != null ? bottleVisual.parent.TransformPoint(pivot) : pivot;
        }
    }

    public Plane DragPlane
    {
        get
        {
            InitializePose();
            Transform parent = bottleVisual.parent;
            Vector3 pivot = parent != null ? parent.TransformPoint(restingLocalPosition) : restingLocalPosition;
            return new Plane(parent != null ? parent.forward : Vector3.forward, pivot);
        }
    }

    void Awake() => InitializePose();

    void InitializePose()
    {
        if (initialized) return;
        if (bottleVisual == null) bottleVisual = transform;
        restingLocalPosition = bottleVisual.localPosition;
        restingLocalRotation = bottleVisual.localRotation;
        cupRimParentPosition = restingLocalPosition + Vector3.up * (liftHeight * 0.4f);
        initialized = true;
    }

    public void ConfigurePourGeometry(Vector3 mouthPosition, Vector3 cupRimWorldPosition)
    {
        InitializePose();
        mouthLocalPosition = mouthPosition;
        cupRimParentPosition = ParentPoint(cupRimWorldPosition);
        ApplyPose();
    }

    Vector3 PourPivotParentPosition()
    {
        Vector3 pivot = cupRimParentPosition + Vector3.up * pourPivotHeight;
        Vector3 mouthOffset = RotationAtAngle(maxTiltAngle) * ScaledMouth;
        pivot.y = Mathf.Min(pivot.y, restingLocalPosition.y + liftHeight + mouthOffset.y);
        return pivot;
    }

    public void BeginDrag(Vector3 pointerWorldPosition)
    {
        InitializePose();
        dragging = true;
        SetPointerAngle(pointerWorldPosition);
    }

    public void DragTo(Vector3 pointerWorldPosition)
    {
        if (dragging) SetPointerAngle(pointerWorldPosition);
    }

    void SetPointerAngle(Vector3 pointerWorldPosition)
    {
        Vector2 direction = ParentPoint(pointerWorldPosition) - PourPivotParentPosition();
        if (direction.sqrMagnitude < PointerDeadZone * PointerDeadZone) return;

        // 참고 사이트와 동일하게 잔 위 고정점에서 아래쪽을 0도로 삼는다.
        // 오른쪽 아래는 직립, 오른쪽 위로 넘어가면 최대 각도를 유지한다.
        float angle = Mathf.Atan2(-direction.x, -direction.y) * Mathf.Rad2Deg;
        if (angle < -90f) angle = maxTiltAngle;
        else if (angle < 0f) angle = 0f;
        targetAngle = Mathf.Clamp(angle, 0f, maxTiltAngle);
    }

    public void EndDrag()
    {
        dragging = false;
        targetAngle = 0f;
    }

    Vector3 ParentPoint(Vector3 worldPosition) =>
        bottleVisual.parent != null ? bottleVisual.parent.InverseTransformPoint(worldPosition) : worldPosition;

    Vector3 ScaledMouth => Vector3.Scale(bottleVisual.localScale, mouthLocalPosition);

    Quaternion RotationAtAngle(float angle) =>
        restingLocalRotation * Quaternion.Euler(0f, 0f, -angle);

    void Update() => Tick(Time.deltaTime);

    void Tick(float deltaTime)
    {
        pickupProgress = Mathf.MoveTowards(pickupProgress, dragging ? 1f : 0f,
            (dragging ? pickupSpeed : putDownSpeed) * deltaTime);
        float angle = dragging && pickupProgress > tiltReadyProgress ? targetAngle : 0f;
        currentAngle = Mathf.MoveTowards(currentAngle, angle,
            (dragging ? tiltSpeed : returnSpeed) * deltaTime);
        ApplyPose();
    }

    void ApplyPose()
    {
        Vector3 restingMouth = restingLocalPosition + restingLocalRotation * ScaledMouth;
        Vector3 mouth = Vector3.Lerp(restingMouth, PourPivotParentPosition(),
            Mathf.SmoothStep(0f, 1f, pickupProgress));
        Quaternion rotation = RotationAtAngle(currentAngle);
        bottleVisual.localPosition = mouth - rotation * ScaledMouth;
        bottleVisual.localRotation = rotation;
    }

    void OnDisable()
    {
        EndDrag();
        if (!initialized) return;
        currentAngle = 0f;
        pickupProgress = 0f;
        bottleVisual.localPosition = restingLocalPosition;
        bottleVisual.localRotation = restingLocalRotation;
    }
}
