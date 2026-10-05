using System;
using UnityEngine;

/// <summary>
/// SPH(Smoothed Particle Hydrodynamics) 시뮬레이션 튜닝값.
/// AlexandreSajus/Unity-Fluid-Simulation의 Config.cs(정적 필드)를 인스펙터에서 조절 가능한
/// 값 묶음으로 옮긴 것 — 여러 PourManager 인스턴스가 각자 다른 값을 가질 수 있게 static을 없앴다.
/// </summary>
[Serializable]
public class SphConfig
{
    [Tooltip("중력 가속도.")]
    public float gravity = 9f;

    [Tooltip("파티클 사이 월드 간격. 액체 알갱이의 굵기를 정하는 기준값 — 줄이면 더 곱고 촘촘한 액체가 되지만 " +
             "같은 부피를 채우는 데 파티클이 더 많이 들어간다(제곱으로 늘어나니 성능 주의). " +
             "이웃 반경과 렌더링 블롭 크기가 모두 이 값에서 유도되므로 이것만 조절하면 된다.")]
    public float spacing = 0.16f;

    [Tooltip("압력 계수(K). 액체가 중력에 눌리는 정도를 좌우한다 — 작으면 파티클을 아무리 늘려도 바닥에 " +
             "얇게 깔리고, 크면 부피를 유지한다. 참고 레포보다 훨씬 큰 값을 쓰는데, 저쪽은 타임스텝이 " +
             "우리(1/120초 고정)의 40배라 낮은 강성밖에 못 썼기 때문이다. 값을 키우면 액체가 덜 눌린다.\n" +
             "아래 StiffnessReferenceSpacing 기준으로 적는다 — spacing을 바꿔도 이 값은 그대로 두면 된다.")]
    public float pressureStiffness = 200f;

    [Tooltip("근접 압력 계수(K_NEAR). 파티클끼리 완전히 겹쳐 뭉치는 것을 막는다.")]
    public float nearPressureStiffness = 40f;

    /// <summary>위 두 계수를 튜닝한 기준 간격. 이 간격에서는 보정이 1배가 된다.</summary>
    const float StiffnessReferenceSpacing = 0.16f;

    /// <summary>
    /// 압력 계수는 spacing에 비례해야 한다. 밀도는 거리의 '비율'(d/r)로만 계산돼 spacing과 무관한데,
    /// 압력힘도 그래서 spacing과 무관하다 — 즉 같은 힘이 간격이 좁아진 만큼 파티클을 상대적으로 더
    /// 멀리 밀어낸다. 진동수가 1/sqrt(spacing)로 올라가 오일러 적분이 발산하고, 액체가 담기는 순간
    /// 사방으로 튄다. 비례시키면 진동수가 일정해져 어떤 알갱이 굵기에서도 같은 거동이 나온다.
    /// </summary>
    float StiffnessScale => spacing / StiffnessReferenceSpacing;

    public float PressureStiffness => pressureStiffness * StiffnessScale;
    public float NearPressureStiffness => nearPressureStiffness * StiffnessScale;

    [Tooltip("기준 밀도의 초기값. 실제로는 SphSimulation.CalibrateRestDensity()가 스폰 직후 실측값으로 " +
             "덮어쓰므로 보통 건드릴 필요 없다.")]
    public float restDensity = 3f;

    [Tooltip("이웃 반경을 spacing의 몇 배로 잡을지. 1.25면 격자에서 상하좌우 4개만 이웃이 되고, " +
             "1.5를 넘기면 대각선까지 들어와 액체가 더 끈끈해진다. 보통 건드릴 필요 없다.")]
    public float neighborRadiusScale = 1.25f;

    /// <summary>이웃 반경(R). 이 거리 안의 파티클만 서로 영향을 준다.
    /// spacing에서 유도해야 spacing을 바꿔도 이웃 관계가 깨지지 않는다(따로 두면 세로로 이웃이 끊겨
    /// 액체가 바닥으로 짓눌리는 문제가 재발한다).</summary>
    public float NeighborRadius => spacing * neighborRadiusScale;

    [Range(0f, 1f)]
    [Tooltip("응집력. 밀도가 기준보다 낮을 때 파티클끼리 얼마나 끌어당길지의 비율이다.\n" +
             "1이면 압력 공식 그대로 당긴다 — 고인 액체는 표면장력처럼 보여 좋지만, 공중의 물줄기는 " +
             "이웃이 적어 늘 '밀도 부족'으로 판정되므로 덩어리로 뭉쳤다가 그 사이가 끊어진다.\n" +
             "0이면 밀어내기만 하고 당기지 않아 줄기가 흩어진다. 0.2~0.4가 적당하다.\n" +
             "미는 쪽(밀도 초과)에는 영향이 없으므로 이 값을 낮춰도 고인 액체가 눌리지는 않는다.")]
    public float cohesion = 0.3f;

    [Tooltip("점성 계수(SIGMA). 클수록 액체가 끈적하게 뭉쳐 움직인다.")]
    public float viscosity = 0.2f;

    [Range(0f, 1f)]
    [Tooltip("매 스텝 속도를 이웃 평균 쪽으로 당기는 비율. 정지 상태에서 액체가 부르르 떨면 올리고, " +
             "너무 끈적하게 굳어 보이면 낮춘다. 0이면 평활화를 끈다.")]
    public float velocitySmoothing = 0.25f;

    [Tooltip("최대 속도. 불안정한 폭발적 힘을 방지하기 위한 클램프. 벽 터널링 방지를 위해 낮게 잡는다.")]
    public float maxVelocity = 4.5f;

}
