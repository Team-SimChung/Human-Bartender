/// <summary>
/// 제조 기믹 한 종류.
///
/// 선언 순서가 곧 기믹 큐의 고정 실행 순서다 — 병따기 → 따르기 → 스퀴즈 → 파우더 → 믹스 → 필업.
/// 큐를 만들 때 이 열거형 값으로 정렬하므로, 순서를 바꾸면 제조 순서가 바뀐다.
/// 셰이크와 스터는 한 제조에 둘 중 하나만 존재하는 '믹스' 한 자리를 나눠 쓴다.
///
/// 필업은 따르기와 조작도 화면도 같다. 그럼에도 종류를 나눈 이유는 실행 위치가 달라서다 —
/// 따르기는 믹스 전, 필업은 믹스 후다. 실제 미니게임은 같은 것을 재사용한다.
/// </summary>
public enum ECraftGimmick
{
    Open,
    Pour,
    Squeeze,
    Powder,
    Shake,
    Stir,
    FillUp,
}

/// <summary>기믹이 어떻게 끝났는지. 결과를 확정한 순간이 언제인지 나중에 되짚기 위해 남긴다.</summary>
public enum ECraftEndType
{
    /// <summary>플레이어가 다음 버튼을 눌러 끝냈다. 목표에 못 미쳤든 넘겼든 그 시점 값이 결과다.</summary>
    ManualNext,

    /// <summary>목표 스택에 도달해 저절로 끝났다. 셰이크와 스터가 쓴다.</summary>
    AutoTarget,

    /// <summary>병뚜껑이 열려 저절로 끝났다. 병따기는 성공해야만 끝나므로 다음 버튼이 없다.</summary>
    AutoSuccess,
}

/// <summary>제조 시도 하나가 거치는 상태.</summary>
public enum ECraftPhase
{
    /// <summary>잔·도구·재료를 고르는 중. 아직 기믹이 시작되지 않아 전체 제조시간도 흐르지 않는다.</summary>
    Preparing,

    /// <summary>기믹 큐를 하나씩 수행하는 중.</summary>
    Playing,

    /// <summary>
    /// 마지막 기믹이 끝나 제조 기록이 확정됐다. 이 뒤로는 입력을 받지 않고 결과를 고칠 수 없다.
    /// </summary>
    Completed,

    /// <summary>
    /// 결과를 보고 버리기를 골랐다. 기록은 지우지 않고 '폐기한 시도'로 남긴다 —
    /// 같은 주문을 다시 만들더라도 이전 시도가 사라지면 안 된다.
    /// </summary>
    Discarded,
    Failed,
    Cancelled,
}
