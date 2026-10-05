using UnityEngine;

/// <summary>
/// 병 스프라이트의 기울기를 제어한다. 입력값(0~1)이 들어오는 동안 목표각으로 보간하고,
/// 입력이 없으면 직립 각도(0도)로 되돌아간다.
/// SPH 기반으로 바뀐 뒤에는 유량을 각도 공식으로 손계산하지 않는다 — bottleVisual 자식으로 붙은
/// U자 컨테이너 콜라이더가 병과 함께 회전하면서, 열린 쪽(주둥이 방향)이 기울기에 따라 중력 반대편을
/// 향하게 되면 SPH 파티클이 물리적으로 흘러나온다. 그래서 이 스크립트는 순수하게 회전 입력/시각만 담당한다.
/// </summary>
public class BottleTiltController : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] Transform bottleVisual;

    [Header("Tilt")]
    [Tooltip("누르고 있을 때 도달하는 최대 기울기(도).\n" +
             "PourManager의 pourFullOpenAngle보다 넉넉히 높아야 한다 — 게이트가 완전히 열린 뒤에도 " +
             "여유가 있어야 콸콸 따르는 구간이 생긴다.")]
    [SerializeField] float maxTiltAngle = 120f;
    [Tooltip("누르고 있는 동안 초당 몇 도씩 기울지. 낮출수록 조작 감도가 둔해져 미세 조절이 쉬워진다.")]
    [SerializeField] float tiltSpeed = 55f;
    [Tooltip("손을 뗐을 때 초당 몇 도씩 되돌아올지. tiltSpeed보다 조금 빨라야 '멈추고 싶을 때 바로 멈추는' 느낌이 난다.")]
    [SerializeField] float returnSpeed = 80f;

    [Header("Lift")]
    [Tooltip("최대 기울기에서 병이 올라가는 높이(부모 기준 로컬 단위).")]
    [SerializeField, Min(0f)] float liftHeight = 1.5f;

    Vector3 restingLocalPosition;
    Quaternion restingLocalRotation;

    void Awake()
    {
        if (bottleVisual == null) bottleVisual = transform;
        restingLocalPosition = bottleVisual.localPosition;
        restingLocalRotation = bottleVisual.localRotation;
    }

    float currentAngle;
    float inputHeld01;

    public Transform BottleVisual => bottleVisual;
    public float CurrentAngle => currentAngle;

    /// <summary>PourInputHandler가 매 프레임 드래그량을 0~1로 정규화해 전달한다.</summary>
    public void SetTiltInput01(float value01)
    {
        inputHeld01 = Mathf.Clamp01(value01);
    }

    void Update()
    {
        float targetAngle = inputHeld01 * maxTiltAngle;
        float speed = targetAngle > currentAngle ? tiltSpeed : returnSpeed;

        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, speed * Time.deltaTime);
        float liftRatio = maxTiltAngle > 0f ? Mathf.Clamp01(currentAngle / maxTiltAngle) : 0f;
        bottleVisual.localPosition = restingLocalPosition + Vector3.up * (liftHeight * liftRatio);
        bottleVisual.localRotation = restingLocalRotation * Quaternion.Euler(0f, 0f, -currentAngle);
    }
}
