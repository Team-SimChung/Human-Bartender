using UnityEngine;

/// <summary>
/// 바의 평판.
///
/// 손님을 응대하지 못해 그냥 돌려보내면 깎인다. 코스터를 놓아 주지 못해 나간 손님(leave_coaster_rep)과
/// 잔을 받지 못하고 나간 손님(leave_serve_rep)이 각각 얼마를 깎는지는 balance.json이 정한다.
///
/// 지금은 이번 실행 동안만 들고 있는 값이다. 며칠에 걸쳐 이어지는 보관은 저장·불러오기 시스템이
/// 정본이라 여기서 정하지 않는다.
/// </summary>
public class BarReputation
{
    /// <summary>현재 평판.</summary>
    public int Value { get; private set; }

    public BarReputation(int startValue)
    {
        Value = startValue;
    }

    /// <summary>평판을 delta만큼 움직인다. reason은 어떤 이탈이 깎았는지 로그로 남기기 위한 값이다.</summary>
    public void Add(int delta, string reason)
    {
        if (delta == 0) return;

        Value += delta;
        Debug.Log($"[Reputation] {reason} {delta:+#;-#;0} → {Value}");
    }
}
