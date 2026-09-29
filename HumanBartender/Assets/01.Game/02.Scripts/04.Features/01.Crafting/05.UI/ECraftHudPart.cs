using System;

/// <summary>
/// 공통 표시가 맡는 자리. 기믹이 그중 일부를 자기 방식으로 그리고 싶을 때 어느 자리인지 가리킨다.
/// </summary>
[Flags]
public enum ECraftHudPart
{
    None = 0,

    /// <summary>좌측 상단 — 지금까지 넣은 양과 목표.</summary>
    Progress = 1 << 0,

    /// <summary>상단 중앙 — 지금 다루는 재료 이름.</summary>
    Subject = 1 << 1,

    /// <summary>우측 상단 — 한 잔 전체의 경과 시간과 제한 시간.</summary>
    Time = 1 << 2,

    /// <summary>우측 하단 — 다음 버튼.</summary>
    NextButton = 1 << 3,

    All = Progress | Subject | Time | NextButton,
}

/// <summary>
/// 공통 표시의 일부를 자기가 직접 그리는 기믹. 구현하지 않으면 공통 표시가 네 자리를 다 맡는다.
///
/// 지금은 네 자리 모두 공통이 그린다. 나중에 어떤 기믹이 진행 상태를 숫자가 아니라 자기만의
/// 그림으로 보여주고 싶어지면, 그 자리만 여기서 가져가고 나머지는 공통에 맡기면 된다 —
/// 공통 표시를 통째로 끄거나 기믹마다 화면을 따로 만들 필요가 없다.
/// </summary>
public interface ICraftGimmickOwnHud
{
    /// <summary>이 기믹이 직접 그리는 자리. 공통 표시는 여기 적힌 자리를 비워 둔다.</summary>
    ECraftHudPart OwnedParts { get; }
}
