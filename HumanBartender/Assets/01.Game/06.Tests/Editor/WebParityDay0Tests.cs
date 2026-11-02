using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>웹 본편 0일차 흐름과 맞춘 Unity 규칙(시작 자리·튜토리얼 제조 인자·튜토리얼 문구 표)을 검사한다.</summary>
public sealed class WebParityDay0Tests
{
    [TestCase(0, null, "gin_fizz", false, "gin_fizz")]
    [TestCase(0, null, "gin_tonic", false, "gin_tonic")]
    [TestCase(0, "gin_tonic", "gin_tonic", false, "gin_tonic")]
    [TestCase(0, null, "gin_fizz", true, null)]
    [TestCase(0, "gin_tonic", "gin_tonic", true, null)]
    [TestCase(1, null, "gin_fizz", false, null)]
    [TestCase(1, "bottle_beer", "bottle_beer", false, "bottle_beer")]
    [TestCase(2, null, "gin_fizz", false, null)]
    [TestCase(0, null, "unknown", false, null)]
    public void CraftLessonsIncludePortsFirstOrderAndRespectSkipping(int day, string explicitCocktail,
        string orderedCocktail, bool skip, string expected)
    {
        Assert.That(StoryTutorialController.ResolveCraftLesson(day, explicitCocktail, orderedCocktail, skip), Is.EqualTo(expected));
    }

    [TestCase("OutSide", 0, EGameFlow.CommuteIn, EPlayerSpawn.FirstCommute)]
    [TestCase("OutSide", 1, EGameFlow.CommuteIn, EPlayerSpawn.Home)]
    [TestCase("OutSide", 0, EGameFlow.CommuteOut, EPlayerSpawn.Bar)]
    [TestCase("OutSide", 2, EGameFlow.CommuteOut, EPlayerSpawn.Bar)]
    [TestCase("Home", 0, EGameFlow.CommuteIn, EPlayerSpawn.Home)]
    [TestCase("Home", 0, EGameFlow.CommuteOut, EPlayerSpawn.Home)]
    public void FirstCommuteStartsAtTheBarWithoutTheElevator(string scene, int day, EGameFlow flow, EPlayerSpawn expected)
    {
        Assert.That(OutsideStorySceneRules.ResolveSpawn(scene, day, flow), Is.EqualTo(expected));
    }

    [TestCase("tutorial_recipe", "gin_tonic")]
    [TestCase("tutorial_beer", "bottle_beer")]
    [TestCase("tutorial:gin_fizz", "gin_fizz")]
    [TestCase(" tutorial_recipe ", "gin_tonic")]
    [TestCase("order", null)]
    [TestCase("", null)]
    [TestCase(null, null)]
    [TestCase("tutorial:", null)]
    [TestCase("tutorial_unknown", null)]
    public void CraftTutorialArgumentsResolveToCocktails(string arg, string expected)
    {
        Assert.That(CsvGameContentRules.ResolveCraftTutorialCocktail(arg), Is.EqualTo(expected));
    }

    [Test]
    public void ShippedCraftTutorialArgumentsResolveToKnownCocktails()
    {
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        var cocktails = catalog.Read<NewCocktailData[]>("cocktails").Select(cocktail => cocktail.Id).ToHashSet();
        var tutorialArgs = Enumerable.Range(0, 4)
            .SelectMany(day => StoryCsvCompiler.Compile(catalog.Read<NewDayScriptBase>("script/bar/day" + day)).Scenes)
            .SelectMany(scene => scene.Steps)
            .Where(step => step.Type == ENewStepType.Craft && step.Arg != null && step.Arg.StartsWith("tutorial", System.StringComparison.Ordinal))
            .Select(step => step.Arg).ToArray();
        Assert.That(tutorialArgs, Is.Not.Empty, "0~3일차 대본에 튜토리얼 제조가 있어야 합니다.");
        foreach (string arg in tutorialArgs)
        {
            string id = CsvGameContentRules.ResolveCraftTutorialCocktail(arg);
            Assert.That(id, Is.Not.Null, "해석하지 못한 craft 인자: " + arg);
            Assert.That(cocktails, Does.Contain(id), "cocktails.csv에 없는 칵테일: " + arg + " → " + id);
        }
    }

    [Test]
    public void DefaultTutorialCopyCoversEveryLessonStep()
    {
        Assert.That(StoryTutorialController.FindMissingCopy(StoryTutorialController.DefaultCopy()), Is.Empty);
        var keys = StoryTutorialController.RequiredCopyKeys;
        Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Count), "문구 키가 중복되었습니다.");
    }

    [Test]
    public void MissingTutorialCopyIsReported()
    {
        var table = StoryTutorialController.DefaultCopy().Where(entry => entry.Key != "recipeRead").ToArray();
        Assert.That(StoryTutorialController.FindMissingCopy(table), Is.EqualTo(new[] { "recipeRead" }));
    }
}
