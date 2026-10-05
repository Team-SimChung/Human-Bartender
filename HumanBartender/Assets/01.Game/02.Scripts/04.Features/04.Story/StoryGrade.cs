/// <summary>
/// 대본이 등급을 견주는 방식.
///
/// ENewGrade는 좋은 것부터 늘어놓은 열거형이라 Excellent가 0이고 Sewage가 4다. 그런데 대본은
/// <c>grade &gt;= excellent</c>를 "최고 등급일 때"라는 뜻으로 쓴다 — 큰 쪽이 좋다는 전제다.
///
/// 그래서 열거형 값을 그대로 비교하면 뜻이 정반대가 된다. Good(1) >= Excellent(0)이 참이 되어
/// "괜찮게 만든 것"이 "최고로 만든 것"의 분기를 타 버리고, 경고 한 줄 남지 않는다.
///
/// 여기서 서열을 뒤집어 매긴다. 대본이 읽히는 대로 동작하게 하려는 것이지 열거형이 틀린 것은 아니다 —
/// 1부의 ServeJudge가 <c>grade &lt;= Decent</c>로 적혀 있는 것도 같은 사정이다.
/// </summary>
public static class StoryGrade
{
    /// <summary>등급을 대본이 견주는 점수로 바꾼다. 클수록 좋다.</summary>
    public static int ToRank(ENewGrade grade) => grade switch
    {
        ENewGrade.Excellent => 4,
        ENewGrade.Good => 3,
        ENewGrade.Decent => 2,
        ENewGrade.Poor => 1,
        _ => 0, // Sewage
    };

    /// <summary>
    /// 대본에 적힌 등급 이름을 점수로 바꾼다. 등급 이름이 아니면 false —
    /// 그때는 부르는 쪽이 변수로 다시 찾아본다.
    /// </summary>
    public static bool TryParseRank(string token, out int rank)
    {
        switch (token)
        {
            case "excellent": rank = 4; return true;
            case "good": rank = 3; return true;
            case "decent": rank = 2; return true;
            case "poor": rank = 1; return true;
            case "sewage": rank = 0; return true;
            default: rank = 0; return false;
        }
    }
}
