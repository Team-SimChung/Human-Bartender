using System;
using UnityEngine;

/// <summary>
/// 잔 속 얼음. 젓는 속도에 끌려 돌고, 젓지 않으면 서서히 멈춘다 (명세 §2.1).
///
/// 물리(Rigidbody2D)를 쓰지 않는다. 판정이 입력만으로 결정되는 기믹이라 물리를 넣어도 게임적으로
/// 얻는 게 없는데, 얼음이 한쪽에 뭉치거나 벽에 끼는 그림이 나올 위험만 생긴다. 대신 잔 전체가
/// 공유하는 각속도 하나를 정답마다 밀어주고 매 프레임 감쇠시킨다 — 그 한 숫자가 명세의 문장
/// 그대로다.
///
/// 다만 겹침만은 그냥 둘 수 없다. 안쪽이 바깥보다 빨리 돌기 때문에 뒤 조각이 앞 조각을 계속
/// 따라잡는데, 막지 않으면 불투명한 얼음끼리 서로 통과해 지나간다. 그래서 각도를 굴린 뒤
/// 위치 구속(겹침 해소) 패스를 돌린다 — 질량도 반발도 없는, 순수하게 '겹치지 않게 밀어내기'다.
///
/// 그 결과 차등 회전의 역할이 바뀐다. 영구적인 속도 차이가 아니라 서로 부딪히는 원인이 되고,
/// 얼음은 뭉쳐서 같이 돌게 된다 — 실제로 믹싱 글라스를 저으면 그렇게 된다.
///
/// 조각의 배치(반지름·시작 각도)는 씬에 놓인 위치에서 그대로 읽는다. 눈으로 잡은 배치를 코드가
/// 덮어쓰지 않으므로, 아트가 들어오면 조각을 옮기기만 하면 된다.
/// </summary>
public class StirIceSwirl : MonoBehaviour
{
    [Serializable]
    public struct IceCube
    {
        [Tooltip("얼음 조각. 잔 중심을 원점으로 하는 좌표계에 놓여 있어야 한다.")]
        public RectTransform target;
        [Tooltip("자전 속도 배수. 부호를 섞으면 조각마다 다른 방향으로 굴러 훨씬 덜 기계적으로 보인다.")]
        public float spinFactor;
    }

    [SerializeField] IceCube[] cubes;

    [Header("Swirl")]
    [Tooltip("정답 하나마다 각속도에 더해지는 양(도/초). 스푼이 한 번에 90° 도는 것과 비슷한 체감으로 잡았다.")]
    [SerializeField] float impulse = 160f;
    [Tooltip("초당 감쇠 계수. 클수록 빨리 멈춘다. 1.2면 손을 놓고 약 2초 뒤 거의 정지한다.\n" +
             "impulse와 짝이다 — 한쪽만 만지면 '계속 도는데 멈추는 느낌이 없다' 또는 " +
             "'입력했는데 안 돈다'가 된다.")]
    [SerializeField] float decay = 1.2f;
    [Tooltip("각속도 상한. 연타로 폭주하는 걸 막는다.")]
    [SerializeField] float maxSwirl = 420f;

    [Header("Differential")]
    [Tooltip("안쪽 조각이 바깥보다 얼마나 빨리 도는지. 1이면 통째로 회전하는 것처럼 보인다.\n" +
             "충돌이 들어온 뒤로 이 값은 '얼마나 자주 부딪히는가'를 정한다 — 키울수록 서로 더 자주 밀친다.")]
    [SerializeField] float innerBoost = 1.35f;

    [Header("Collision")]
    [Tooltip("충돌 반지름을 조각 크기의 몇 배로 볼지. 0.5는 정사각형의 내접원이라 모서리가 " +
             "조금 겹칠 수 있고, 0.7에 가까울수록 외접원이 되어 멀찍이서부터 밀어낸다.")]
    [SerializeField] float collisionRadiusScale = 0.5f;
    [Tooltip("겹침 해소 반복 횟수. 조각이 6개뿐이라 2회면 충분하다. 늘리면 뭉쳤을 때 더 단단해진다.")]
    [SerializeField] int solverIterations = 2;
    [Tooltip("잔 안쪽 반지름(UI 단위). 조각은 이 안에 갇힌다. 0이면 부모 RectTransform 폭의 절반을 쓴다.")]
    [SerializeField] float glassRadius = 0f;
    [Tooltip("밀려난 조각이 원래 반지름으로 돌아오는 속도(초당 비율).\n" +
             "이게 약하면 스치는 충돌이 조각을 조금씩 안쪽으로 밀어 넣어, 오래 젓는 동안 " +
             "한 조각이 중심까지 파고들고 배치가 뭉개진다. 4면 10스택을 다 돌려도 " +
             "원래 반지름에서 1px 안쪽으로 유지된다.")]
    [SerializeField] float radiusRestore = 4f;

    /// <summary>잔 전체가 공유하는 각속도(도/초). 이 값 하나가 얼음의 움직임 전부를 정한다.</summary>
    float swirlSpeed;

    Vector2[] positions;
    float[] spins;
    float[] restRadii;
    float[] collisionRadii;
    float averageRadius;
    float layoutRadius = 1f;

    /// <summary>왼쪽 연출 영역이 붙을 때 같은 값을 구독하면 두 화면의 얼음이 저절로 맞물린다.</summary>
    public float SwirlSpeed => swirlSpeed;

    /// <summary>조각 수. 같은 얼음을 다른 시점으로 그리는 뷰(StirSideGlassView)가 읽어간다.</summary>
    public int CubeCount => positions?.Length ?? 0;

    /// <summary>가장 바깥 조각의 반지름. 좌표를 -1~1로 정규화하는 기준이라 배치를 바꿔도 뷰가 따라온다.</summary>
    public float LayoutRadius => layoutRadius;

    /// <summary>i번 조각의 잔 중심 기준 위치(UI 단위). 아직 초기화 전이면 원점을 준다.</summary>
    public Vector2 GetCubePosition(int index)
        => positions != null && (uint)index < (uint)positions.Length ? positions[index] : Vector2.zero;

    void Awake()
    {
        CaptureLayout();

        // 시작 배치에 조금이라도 겹침이 있으면 여기서 한 번 털어낸다.
        // dt를 0으로 넘겨 반지름 복귀는 일으키지 않는다 — 배치를 그대로 두려는 것이다.
        Resolve(0f);
        Apply();
    }

    /// <summary>씬에 놓인 위치에서 극좌표와 충돌 반지름을 뽑아둔다.</summary>
    void CaptureLayout()
    {
        int count = cubes?.Length ?? 0;

        positions = new Vector2[count];
        spins = new float[count];
        restRadii = new float[count];
        collisionRadii = new float[count];

        if (count == 0) return;

        if (glassRadius <= 0f && transform is RectTransform self)
        {
            glassRadius = self.rect.width * 0.5f;
        }

        float radiusSum = 0f;
        float radiusMax = 0f;
        int valid = 0;

        for (int i = 0; i < count; i++)
        {
            RectTransform target = cubes[i].target;
            if (target == null) continue;

            positions[i] = target.anchoredPosition;
            spins[i] = target.localEulerAngles.z;
            collisionRadii[i] = Mathf.Max(target.rect.width, target.rect.height) * collisionRadiusScale;

            // 원래 반지름이 잔 밖이면 복귀가 테두리 구속과 계속 싸우게 된다. 미리 안쪽으로 당겨둔다.
            restRadii[i] = Mathf.Min(positions[i].magnitude,
                                     Mathf.Max(0f, glassRadius - collisionRadii[i]));

            radiusSum += positions[i].magnitude;
            radiusMax = Mathf.Max(radiusMax, positions[i].magnitude);
            valid++;
        }

        averageRadius = valid > 0 ? radiusSum / valid : 1f;
        layoutRadius = Mathf.Max(radiusMax, 1f);   // 0으로 나누는 것만 막으면 된다
    }

    /// <summary>정답 입력마다 StirManager가 부른다. 젓는 힘이 한 번 들어가는 지점이다.</summary>
    public void AddImpulse() => swirlSpeed = Mathf.Min(swirlSpeed + impulse, maxSwirl);

    /// <summary>다시 시작할 때 얼음을 멈춘다. 배치는 건드리지 않는다.</summary>
    public void StopSwirl() => swirlSpeed = 0f;

    void Update()
    {
        if (positions == null || positions.Length == 0) return;

        float dt = Time.deltaTime;

        // 지수 감쇠. 프레임률이 달라져도 같은 속도로 잦아든다.
        swirlSpeed *= Mathf.Exp(-decay * dt);
        if (swirlSpeed < 0.05f) swirlSpeed = 0f;

        // 멈춰 있으면 새로 겹칠 일도 없다.
        if (swirlSpeed <= 0f) return;

        for (int i = 0; i < positions.Length; i++)
        {
            if (cubes[i].target == null) continue;

            // 시계 방향으로 돈다 — 스푼과 같은 방향이라야 끌려가는 것으로 보인다.
            float degrees = -swirlSpeed * SpeedFactor(positions[i].magnitude) * dt;

            positions[i] = Rotate(positions[i], degrees);
            spins[i] -= swirlSpeed * cubes[i].spinFactor * dt;
        }

        Resolve(dt);
        Apply();
    }

    /// <summary>안쪽일수록 빠르게. 그대로 반지름 비를 쓰면 중심 근처 조각이 튀어서 폭을 제한한다.</summary>
    float SpeedFactor(float radius)
    {
        if (radius <= 0.01f) return innerBoost;

        float ratio = Mathf.Pow(averageRadius / radius, 0.8f);
        return Mathf.Clamp(ratio, 1f / innerBoost, innerBoost);
    }

    /// <summary>
    /// 겹침을 밀어내고 잔 안에 가둔다. 질량도 반발도 없는 위치 구속이라 '부딪혀서 튕긴다'가 아니라
    /// '서로 파고들지 않는다'에 가깝다 — 탑다운 연출에는 이걸로 충분하다.
    /// </summary>
    void Resolve(float dt)
    {
        int count = positions.Length;

        for (int iteration = 0; iteration < Mathf.Max(1, solverIterations); iteration++)
        {
            for (int i = 0; i < count; i++)
            {
                if (cubes[i].target == null) continue;

                for (int j = i + 1; j < count; j++)
                {
                    if (cubes[j].target == null) continue;

                    Vector2 delta = positions[j] - positions[i];
                    float minDistance = collisionRadii[i] + collisionRadii[j];
                    float distance = delta.magnitude;

                    if (distance >= minDistance) continue;

                    // 완전히 겹쳐 방향을 못 구하는 경우엔 아무 방향으로나 떼어낸다.
                    Vector2 normal = distance > 0.0001f ? delta / distance : Vector2.right;
                    Vector2 push = normal * ((minDistance - distance) * 0.5f);

                    positions[i] -= push;
                    positions[j] += push;
                }
            }

            // 테두리 안으로. 밀려서 잔 밖으로 나간 조각을 되돌린다.
            for (int i = 0; i < count; i++)
            {
                if (cubes[i].target == null) continue;

                float limit = Mathf.Max(0f, glassRadius - collisionRadii[i]);
                float radius = positions[i].magnitude;

                if (radius > limit && radius > 0.0001f) positions[i] *= limit / radius;
            }
        }

        if (dt <= 0f || radiusRestore <= 0f) return;

        // 밀려난 조각을 제자리 반지름으로 아주 천천히 되돌린다. 없으면 오래 젓는 동안
        // 조각들이 한쪽 반지름으로 뭉개져 프로토타입의 층이 사라진다.
        float t = 1f - Mathf.Exp(-radiusRestore * dt);

        for (int i = 0; i < count; i++)
        {
            if (cubes[i].target == null) continue;

            float radius = positions[i].magnitude;
            if (radius <= 0.0001f) continue;

            positions[i] *= Mathf.Lerp(radius, restRadii[i], t) / radius;
        }
    }

    void Apply()
    {
        for (int i = 0; i < positions.Length; i++)
        {
            RectTransform target = cubes[i].target;
            if (target == null) continue;

            target.anchoredPosition = positions[i];
            target.localRotation = Quaternion.Euler(0f, 0f, spins[i]);
        }
    }

    static Vector2 Rotate(Vector2 value, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);

        return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
    }
}
