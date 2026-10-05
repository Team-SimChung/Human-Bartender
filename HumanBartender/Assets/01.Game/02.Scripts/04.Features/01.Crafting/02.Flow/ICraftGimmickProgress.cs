/// <summary>
/// 기믹이 진행 중에 화면 위쪽에 보여줄 상태를 알려준다. 구현하지 않아도 기믹은 돌아가고,
/// 그때는 좌측 상단이 비어 있을 뿐이다.
///
/// 텍스트를 기믹이 직접 만든다. 기믹마다 재는 것이 달라서다 — 따르기는 oz, 셰이크는 스택,
/// 병따기는 시도 횟수다. 공통 HUD가 이 차이를 다 알게 만들면, 기믹이 하나 늘 때마다 HUD를
/// 고쳐야 한다. HUD는 어디에 어떤 크기로 놓을지만 안다.
/// </summary>
public interface ICraftGimmickProgress
{
    /// <summary>좌측 상단에 넣을 진행 상태. 예: "0.54 / 1.50 oz", "시도 3회째", "8 / 20".</summary>
    string ProgressText { get; }

    /// <summary>
    /// 목표에 도달했는지. 화면 가운데 O.K를 켜는 신호다.
    ///
    /// O.K는 기믹을 끝내지 않는다. 목표를 넘겨 계속 넣어도 켜진 채로 있고, 끝내는 시점은
    /// 플레이어가 정한다.
    /// </summary>
    bool ShowOkMark { get; }
}

/// <summary>
/// 플레이어가 다음 버튼으로 직접 끝낼 수 있는 기믹. 병따기처럼 성공해야만 끝나는 기믹은
/// 구현하지 않으며, 그때 HUD는 버튼을 감춘다.
/// </summary>
public interface ICraftGimmickManualEnd
{
    /// <summary>지금 끝낼 수 있는지. 아직 시작 전이거나 이미 끝났다면 false다.</summary>
    bool CanEndNow { get; }

    /// <summary>
    /// 지금까지의 결과로 기믹을 확정한다. 목표에 못 미쳤어도, 넘겼어도 그 시점 값이 결과다.
    /// 모자란 몫을 성공으로 채워 주지 않는다.
    /// </summary>
    void EndNow();
}
