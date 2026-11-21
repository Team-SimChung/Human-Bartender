using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 개수 단위 재료(조니 올드 패션드의 각설탕 1개)는 ml로 바꾸지 않고 개수끼리 비교한다(웹 craft-rules.js scoreCraft).
/// 바꾸려다 실패하면 판정이 데이터 오류로 끝나 제공 화면에 '판정 불가'가 뜨고, 손님에게 내도 다음으로 넘어가지 않았다.
/// </summary>
public sealed class CraftJudgeCountUnitTests
{
    const string CocktailId = "johnny_old_fashioned";
    const string SugarCube = "sugar_cube";

    [TestCase(1f, true)]
    [TestCase(2f, false)]
    public void OldFashionedSugarCubeIsScoredByCount(float cubes, bool exact)
    {
        var csv = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        var cocktail = csv.Read<NewCocktailData[]>("cocktails").Single(c => c.Id == CocktailId);
        var balance = csv.Read<NewBalanceDataBase>("balance");
        Assert.That(cocktail.Recipe.Any(s => s.Ingredient == SugarCube && s.Unit == ENewUnit.Count), Is.True,
            "각설탕이 개수 단위가 아니면 이 테스트가 지키는 경우가 아니다.");

        var session = new CraftSession(CocktailId);
        var actual = session.Actual;
        actual.SetGlass(cocktail.Glass);
        actual.SetTool(cocktail.TargetToolId);
        foreach (var step in cocktail.Recipe.Where(s => s.IsSelectable)) actual.SelectIngredient(step.Ingredient);
        foreach (var step in cocktail.Recipe)
        {
            var type = step.Action == ENewRecipeAction.Pour ? ECraftGimmick.Pour : ECraftGimmick.Powder;
            float value = step.Ingredient == SugarCube ? cubes : step.Qty.Value;
            actual.Record(GimmickResult.Quantity(type, step.Ingredient, step.Qty, step.Unit, value, ECraftEndType.ManualNext, 1f));
        }
        actual.Record(GimmickResult.Mix(ECraftGimmick.Stir, 3, 0, 3, ECraftEndType.AutoTarget, 2f));

        var judged = CraftJudge.Evaluate(session, cocktail, null, balance);

        Assert.That(judged.Status, Is.Not.EqualTo(ENewScoreStatus.DataError), judged.ErrorCode);
        Assert.That(judged.CraftGrade, Is.Not.Null, "등급이 없으면 '판정 불가'가 뜨고 제공해도 결과가 나오지 않는다.");
        if (exact) Assert.That(judged.CraftScore, Is.EqualTo(100f));
        else Assert.That(judged.CraftScore, Is.LessThan(100f), "개수가 틀리면 수량 오차로 깎인다.");
    }
}
