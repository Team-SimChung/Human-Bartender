/// <summary>
/// 새 쉐이킹의 기본 수치, 콤보 단계와 레인 패턴을 모은 규칙 파일.
/// 참고: https://junseo874.github.io/bar-playtest/shake-rhythm.js
/// 음악 BPM이나 오디오 재생 시간을 사용하지 않는 규칙이다.
///
/// 상수들은 ShakingRhythmManager의 초기 기본값으로 사용된다. 이미 씬/프리팹에 저장된
/// Inspector 값은 상수를 수정해도 자동으로 덮어써지지 않으므로 함께 확인해야 한다.
/// Tier/Multiplier/Lane은 매니저가 실행 중 직접 호출한다.
/// </summary>
public static class ShakingRhythmRules
{
    /// <summary>실제 생성할 노트의 기본 목표 수. 빈 타이밍 입력은 이 수를 소모하지 않는다.</summary>
    public const int NoteCount = 20;
    /// <summary>1배속에서 생성점부터 판정점까지 걸리는 기본 시간(초).</summary>
    public const float LeadSeconds = 1.8f;
    /// <summary>공통 이동 시계 기준의 생성 간격(초). 실제 간격은 배속에 반비례한다.</summary>
    public const float IntervalSeconds = 0.6f;
    /// <summary>Perfect의 앞/뒤 허용 오차, 초 단위. 0.075초 = 75ms.</summary>
    public const float PerfectSeconds = 0.075f;
    /// <summary>성공으로 처리할 최대 앞/뒤 오차, 초 단위. Perfect 바깥부터 여기까지는 Good.</summary>
    public const float HitWindowSeconds = 0.18f;
    // 0 = 왼쪽 A, 1 = 오른쪽 D. 참고 사이트의 패턴을 같은 생성 순서로 사용한다.
    // 길이는 15지만 노트 수는 기본 20이므로 마지막 5개는 이 패턴의 앞 5개를 반복한다.
    // 개별 노트의 속도를 지정하지 않는다. 생성 이후의 모든 노트 속도는 매니저가 공유한다.
    private static readonly int[] Pattern = { 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0 };

    /// <summary>연속 성공 수를 연출/속도 단계로 변환한다. 0~4: 0, 5~9: 1, 10 이상: 2.</summary>
    public static int Tier(int combo) => combo >= 10 ? 2 : combo >= 5 ? 1 : 0;
    /// <summary>
    /// 단계별 배속. 10콤보 이후에도 1.5배로 유지한다.
    /// 매니저가 실패 시 combo를 0으로 바꾸면 별도 속도 초기화 변수 없이 1배로 돌아간다.
    /// </summary>
    public static float Multiplier(int combo) => Tier(combo) == 2 ? 1.5f : Tier(combo) == 1 ? 1.25f : 1f;
    /// <summary>생성 순번에 해당하는 레인. 패턴 길이를 넘으면 나머지 연산으로 처음부터 반복한다.</summary>
    /// <param name="index">0부터 시작하는 생성 순번. 매니저의 spawned를 전달하며 음수는 받지 않는다.</param>
    public static int Lane(int index) => Pattern[index % Pattern.Length];
}
