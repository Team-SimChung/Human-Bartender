using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

public sealed class ServeAffinityTests
{
    readonly List<Object> owned = new();
    readonly List<(string id, int delta)> changes = new();
    NewCharacterData[] originalCharacters;
    NewTasteData[] originalTastes;
    NewCocktailDataSO cocktails;
    NewBalanceDataSO balance;
    PlayerDataSO player;
    IConditionUtil conditions;
    DailySales sales;
    ServeProcessor processor;

    [SetUp]
    public void SetUp()
    {
        originalCharacters = NewDataLoadManager.StoryCharacters;
        originalTastes = NewDataLoadManager.StoryTastes;
        var csv = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        SetContent("StoryCharacters", csv.Read<NewCharacterData[]>("characters"));
        SetContent("StoryTastes", csv.Read<NewTasteData[]>("tastes"));
        cocktails = Own(ScriptableObject.CreateInstance<NewCocktailDataSO>());
        cocktails.cocktailData = csv.Read<NewCocktailData[]>("cocktails");
        balance = Own(ScriptableObject.CreateInstance<NewBalanceDataSO>());
        balance.balanceData = csv.Read<NewBalanceDataBase>("balance");
        player = Own(ScriptableObject.CreateInstance<PlayerDataSO>());
        player.AffinityAdded += (id, delta) => changes.Add((id, delta));
        conditions = new ConditionUtil(GameStateManager.Instance, player, player);
        sales = new DailySales();
        processor = new ServeProcessor(cocktails, balance, sales, player, conditions);
    }

    [TearDown]
    public void TearDown()
    {
        SetContent("StoryCharacters", originalCharacters);
        SetContent("StoryTastes", originalTastes);
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        changes.Clear();
    }

    [TestCase(ENewGrade.Excellent, 6)]
    [TestCase(ENewGrade.Good, 4)]
    [TestCase(ENewGrade.Decent, 1)]
    [TestCase(ENewGrade.Poor, -1)]
    [TestCase(ENewGrade.Sewage, -2)]
    public void PortGinFizzUsesCsvTasteAndFinalGrade(ENewGrade grade, int expected)
    {
        var result = Evaluate("port", "gin_fizz", grade);
        Assert.That(result.AffinityDelta, Is.EqualTo(expected));
        Assert.That(player.GetCurCharacterAffinityValue("port"), Is.Zero, "판정만으로는 진행을 변경하지 않는다.");
        Assert.That(changes, Is.Empty);
        processor.Apply(result);
        Assert.That(player.GetCurCharacterAffinityValue("port"), Is.EqualTo(expected));
        Assert.That(changes, Is.EqualTo(new[] { ("port", expected) }));
    }

    [TestCase(ENewGrade.Excellent, 2)]
    [TestCase(ENewGrade.Decent, 0)]
    [TestCase(ENewGrade.Poor, -1)]
    public void NoMatchingTasteUsesOkAndZeroDoesNotNotify(ENewGrade grade, int expected)
    {
        processor.Apply(Evaluate("tom", "gin_fizz", grade));
        Assert.That(player.GetCurCharacterAffinityValue("tom"), Is.EqualTo(expected));
        Assert.That(changes.Count, Is.EqualTo(expected == 0 ? 0 : 1));
    }

    [TestCase("shiba")]
    [TestCase("luna")]
    [TestCase("random_guest_1")]
    public void DisabledOrUnregisteredCharactersDoNotGainAffinity(string actor)
    {
        processor.Apply(Evaluate(actor, "gin_fizz", ENewGrade.Excellent));
        Assert.That(player.ReadAffinity(), Is.Empty);
        Assert.That(changes, Is.Empty);
        Assert.That(sales.Items.Count, Is.EqualTo(1), "매출 정산은 유지한다.");
    }

    [Test]
    public void WrongDrinkUsesMissAndMissingCoreUsesFinalGrade()
    {
        var wrong = processor.Evaluate(new OrderDetails("wrong", "port", "gin_fizz"), Drink("gin_tonic", ENewGrade.Excellent));
        Assert.That(wrong.FinalGrade, Is.EqualTo(ENewGrade.Sewage));
        Assert.That(wrong.AffinityDelta, Is.EqualTo(-4));
        var drink = Drink("gin_fizz", ENewGrade.Excellent);
        drink.Judgement.MissingCoreIngredientIds.Add("gin");
        var missing = processor.Evaluate(new OrderDetails("missing", "port", "gin_fizz"), drink);
        Assert.That(missing.FinalGrade, Is.EqualTo(ENewGrade.Sewage));
        Assert.That(missing.AffinityDelta, Is.EqualTo(-2));
    }

    [Test]
    public void KoreanTagsAndFlagDependentAbvConditionsUseFirstSequence()
    {
        Assert.That(Evaluate("chris", "gin_tonic", ENewGrade.Excellent).AffinityDelta, Is.EqualTo(4));
        SetContent("StoryTastes", NewDataLoadManager.StoryTastes.OrderByDescending(t => t.Seq).ToArray());
        string strong = cocktails.cocktailData.First(c => c.Abv >= 20).Id;
        Assert.That(Evaluate("samho", strong, ENewGrade.Excellent).AffinityDelta, Is.EqualTo(6));
        player.AddFlag("samho_calmed", true);
        Assert.That(Evaluate("samho", strong, ENewGrade.Excellent).AffinityDelta, Is.EqualTo(1));
        Assert.That(conditions.Result, Is.Null, "취향 판정이 대본의 결과 문맥을 덮어쓰지 않는다.");
    }

    [Test]
    public void RepeatedOrReentrantSettlementCannotApplyAffinityTwice()
    {
        var first = Evaluate("port", "gin_fizz", ENewGrade.Excellent);
        var sameId = Evaluate("port", "gin_fizz", ENewGrade.Excellent);
        player.AffinityAdded += (_, _) => Assert.Throws<InvalidOperationException>(() => processor.Apply(first));
        processor.Apply(first);
        Assert.Throws<InvalidOperationException>(() => processor.Apply(first));
        Assert.Throws<InvalidOperationException>(() => processor.Apply(sameId));
        Assert.That(player.GetCurCharacterAffinityValue("port"), Is.EqualTo(6));
        Assert.That(changes.Count, Is.EqualTo(1));
        Assert.That(sales.Items.Count, Is.EqualTo(1));
    }

    [Test]
    public void EvaluationAndDataFailureDoNotCommitProgress()
    {
        Evaluate("port", "gin_fizz", ENewGrade.Excellent);
        balance.balanceData.AffinityMatrix = Array.Empty<NewAffinityMatrixRow>();
        Assert.Throws<InvalidOperationException>(() => Evaluate("port", "gin_fizz", ENewGrade.Excellent));
        Assert.That(sales.Items, Is.Empty);
        Assert.That(player.ReadAffinity(), Is.Empty);
        Assert.That(changes, Is.Empty);
    }

    [Test]
    public void InjectedOrderProcessorRequestsExistingIncreaseAndDecreasePopups()
    {
        var host = Own(new GameObject("ServeAffinityTest"));
        var controller = host.AddComponent<OrderRequestController>();
        SetField(controller, "cocktailData", cocktails);
        SetField(controller, "balanceData", balance);
        var builder = new ContainerBuilder();
        builder.RegisterInstance(sales);
        builder.RegisterInstance<IPlayerDataWriter>(player);
        builder.RegisterInstance<IConditionUtil>(conditions);
        builder.RegisterComponent(controller);
        using var container = builder.Build();
        var injected = container.Resolve<OrderRequestController>();
        var wired = (IServeProcessor)typeof(OrderRequestController)
            .GetProperty("Processor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(injected);
        var popups = new PopupSpy();
        using var audio = new SceneAudioController(null, player, popups, conditions);
        audio.Start();
        wired.Apply(wired.Evaluate(new OrderDetails("up", "port", "gin_fizz"), Drink("gin_fizz", ENewGrade.Excellent)));
        wired.Apply(wired.Evaluate(new OrderDetails("down", "port", "gin_fizz"), Drink("gin_fizz", ENewGrade.Poor)));
        wired.Apply(wired.Evaluate(new OrderDetails("zero", "tom", "gin_fizz"), Drink("gin_fizz", ENewGrade.Decent)));
        Assert.That(popups.Types, Is.EqualTo(new[] { PopupType.AffinityIncreased, PopupType.AffinityDecreased }));
        Assert.That(popups.Names.All(name => !string.IsNullOrWhiteSpace(name)), Is.True);
    }

    [TestCase("cocktail.id == gin_fizz && cocktail.abv >= 0", true)]
    [TestCase("cocktail.id == 'gin_fizz'", true)]
    [TestCase("cocktail.id != gin_fizz", false)]
    [TestCase("cocktail.tag(클래식)", true)]
    [TestCase("cocktail.tag(없는태그)", false)]
    public void CocktailConditionsUseServedDataAndValidateWithoutRuntimeContext(string expression, bool expected)
    {
        var cocktail = cocktails.cocktailData.Single(c => c.Id == "gin_fizz");
        conditions.Result = new StoryResultContext(ENewGrade.Good, ENewGrade.Good, true,
            cocktail.Id, cocktail.Id, cocktail);
        Assert.That(ConditionUtil.ValidateConditionSyntax(expression), Is.Null);
        Assert.That(conditions.CheckRequired(expression), Is.EqualTo(expected));
        Assert.That(conditions.Evaluate("cocktail.abv == true").IsValid, Is.False);
        Assert.That(conditions.Evaluate("unknown_key == 1").IsValid, Is.False);
    }

    ServeResult Evaluate(string actor, string cocktail, ENewGrade grade) =>
        processor.Evaluate(new OrderDetails("order", actor, cocktail), Drink(cocktail, grade));

    CraftedDrink Drink(string id, ENewGrade grade) => new(null, new CraftJudgement { CraftGrade = grade },
        cocktails.cocktailData.Single(c => c.Id == id));

    T Own<T>(T item) where T : Object { owned.Add(item); return item; }
    static void SetContent(string name, object value) => typeof(NewDataLoadManager).GetProperty(name)
        .GetSetMethod(true).Invoke(null, new[] { value });
    static void SetField(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    sealed class PopupSpy : IPopupService
    {
        public readonly List<PopupType> Types = new();
        public readonly List<string> Names = new();
        public UniTask<bool> Popup(PopupType type, string data)
        {
            Types.Add(type);
            Names.Add(data);
            return UniTask.FromResult(true);
        }
    }
}
