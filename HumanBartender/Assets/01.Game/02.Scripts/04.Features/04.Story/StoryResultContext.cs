/// <summary>
/// 잔 하나를 낸 결과. 서빙이 확정된 순간 만들어져 그 뒤 대사들이 읽는다
/// (2부 운영 명세 §10.3).
///
/// 값이 아직 없을 때 0이나 Sewage 같은 것으로 메우지 않는다. 서빙 전에 결과를 묻는 조건식은
/// 데이터가 잘못된 것이지 "아직 나쁜 잔"이 아니다 — 메우면 그 잘못이 그럴듯한 분기로 둔갑한다.
///
/// 수명은 짧다. 같은 씬의 후속 조건과 effects가 쓰고, 새 주문이 시작되거나 씬이 끝나면 버린다.
/// </summary>
public class StoryResultContext
{
    /// <summary>주문 일치와 핵심 재료 강제 판정을 적용하기 전의 제조 등급.</summary>
    public ENewGrade CraftGrade { get; }

    /// <summary>주문 불일치·핵심 재료 누락의 강제 판정까지 반영한 최종 등급.</summary>
    public ENewGrade FinalGrade { get; }

    /// <summary>주문한 칵테일과 실제로 낸 칵테일이 같은지.</summary>
    public bool OrderMatch { get; }

    /// <summary>직전 order에서 정해진 주문 칵테일.</summary>
    public string OrderedCocktailId { get; }

    /// <summary>플레이어가 실제로 낸 칵테일.</summary>
    public string ServedCocktailId { get; }

    public StoryResultContext(ENewGrade craftGrade, ENewGrade finalGrade, bool orderMatch,
                              string orderedCocktailId, string servedCocktailId)
    {
        CraftGrade = craftGrade;
        FinalGrade = finalGrade;
        OrderMatch = orderMatch;
        OrderedCocktailId = orderedCocktailId;
        ServedCocktailId = servedCocktailId;
    }
}
