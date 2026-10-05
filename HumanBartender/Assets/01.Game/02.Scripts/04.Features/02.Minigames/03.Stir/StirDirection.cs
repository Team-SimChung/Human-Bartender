/// <summary>
/// 스터의 4방위. 값 순서가 곧 시계 방향이라 (dir + 1) % 4가 항상 '다음 정답'이 된다 —
/// 스터의 정답 규칙 전체가 이 한 줄로 끝나므로 순서를 바꾸면 안 된다.
/// </summary>
public enum EStirDirection
{
    Up = 0,     // W / ↑
    Right = 1,  // D / →
    Down = 2,   // S / ↓
    Left = 3,   // A / ←
}

/// <summary>방위 관련 공용 상수와 변환. 매니저·입력·뷰가 같은 규칙을 보게 모아둔다.</summary>
public static class StirDirections
{
    public const int Count = 4;

    /// <summary>한 바퀴에 필요한 정답 입력 수. 4방위를 도는 게임이라 방위 수와 같은 고정값이다.</summary>
    public const int StepsPerAttempt = Count;

    /// <summary>시계 방향 이웃. 지금 방위에서 이 방위만 정답이다.</summary>
    public static int Next(int direction) => (direction + 1) % Count;

    /// <summary>키 배지에 표시할 이름.</summary>
    public static string KeyLabel(int direction) => direction switch
    {
        (int)EStirDirection.Up => "W",
        (int)EStirDirection.Right => "D",
        (int)EStirDirection.Down => "S",
        (int)EStirDirection.Left => "A",
        _ => "?",
    };

    /// <summary>로그·안내 문구에 쓰는 화살표.</summary>
    public static string ArrowLabel(int direction) => direction switch
    {
        (int)EStirDirection.Up => "↑",
        (int)EStirDirection.Right => "→",
        (int)EStirDirection.Down => "↓",
        (int)EStirDirection.Left => "←",
        _ => "?",
    };

    /// <summary>
    /// 방위가 가리키는 화면 각도(Z 회전, 도). 위가 0이고 시계 방향으로 음수가 된다 —
    /// Unity의 Z 회전은 반시계가 양수이므로 시계 방향으로 돌리려면 빼야 한다.
    /// </summary>
    public static float ToAngle(int direction) => -90f * direction;
}
