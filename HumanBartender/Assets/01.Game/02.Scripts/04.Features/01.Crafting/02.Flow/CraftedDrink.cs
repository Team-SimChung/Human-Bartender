using UnityEngine;

/// <summary>
/// 제조를 마친 잔 하나.
///
/// 다 만든 잔이 곧바로 손님에게 가지는 않는다. 완성한 잔은 트레이에 놓였다가 플레이어가 누구에게
/// 낼지 정하는 순간에야 손님과 이어지므로, 그 사이 동안 잔 하나를 가리킬 것이 필요하다.
///
/// 손님을 알지 못한다. 주문과 맞는 잔인지, 최종 등급이 무엇인지는 낼 때 정해지는 값이라
/// 여기에 담으면 아직 정해지지 않은 값을 들고 트레이에 놓이게 된다. 그 판단은 ServeJudge가 한다.
/// </summary>
public class CraftedDrink
{
    /// <summary>만들어진 칵테일. 오선택으로 주문과 다른 것을 만들었더라도 이 잔의 정체성은 만든 쪽이다.</summary>
    public string CocktailId { get; }

    public string DisplayName { get; }

    /// <summary>cocktails.json의 color. 잔 그림이 아직 없어 임시로 이 색을 칠해 잔을 표시한다.</summary>
    public Color Color { get; }

    /// <summary>그라데이션 칵테일의 두 번째 색. 단색이면 null이다.</summary>
    public Color? Color2 { get; }

    /// <summary>이 잔을 만든 시도. 무엇을 얼마나 넣었는지 되짚을 때 쓴다.</summary>
    public CraftSession Session { get; }

    /// <summary>제조 판정. 데이터 오류로 채점하지 못했으면 null일 수 있다.</summary>
    public CraftJudgement Judgement { get; }

    /// <summary>제조 등급. 채점하지 못한 잔은 null이다.</summary>
    public ENewGrade? CraftGrade => Judgement?.CraftGrade;

    /// <summary>핵심 재료를 빠뜨린 잔인지. 서빙 시점에 등급이 뒤집히는 조건이다.</summary>
    public bool HasMissingCore => Judgement != null && Judgement.MissingCoreIngredientIds.Count > 0;

    public CraftedDrink(CraftSession session, CraftJudgement judgement, NewCocktailData cocktail)
    {
        Session = session;
        Judgement = judgement;

        CocktailId = cocktail.Id;
        DisplayName = string.IsNullOrEmpty(cocktail.Name.Ko) ? cocktail.Id : cocktail.Name.Ko;
        Color = ParseColor(cocktail.Color);
        Color2 = string.IsNullOrEmpty(cocktail.Color2) ? null : ParseColor(cocktail.Color2);
    }

    /// <summary>cocktails.json의 색 표기(hex)를 읽는다. 읽지 못하면 흰색으로 둔다.</summary>
    static UnityEngine.Color ParseColor(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out UnityEngine.Color color) ? color : UnityEngine.Color.white;
    }
}
