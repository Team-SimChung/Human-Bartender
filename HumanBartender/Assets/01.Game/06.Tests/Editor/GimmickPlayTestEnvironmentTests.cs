using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.SceneManagement;

/// <summary>실제 CSV의 즉시 플레이 목록과 Play 씬 실행 환경 사이의 연결을 검증한다.</summary>
public sealed class GimmickPlayTestEnvironmentTests
{
    private sealed class RecordingExecutor : ICraftExecutor
    {
        internal int BeginCount, EndCount;
        internal bool CheckFixedContext = true;
        internal CraftTimer Timer;
        internal readonly List<GimmickStep> Steps = new();
        internal readonly List<CraftContext> Contexts = new();

        public void Begin(CraftTimer timer, CraftRunDisplay display) { Timer = timer; BeginCount++; }
        public void End() => EndCount++;

        public UniTask<GimmickResult> ExecuteAsync(GimmickStep step, CraftContext context, CraftTimer timer, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Assert.That(timer, Is.SameAs(Timer));
            if (CheckFixedContext)
            {
                Assert.That(context.SelectedCocktailId, Is.EqualTo("test-cocktail"));
                Assert.That(context.GlassId, Is.EqualTo("test-glass"));
            }
            Steps.Add(step);
            Contexts.Add(context);
            if (!step.IsImmediateAddition) timer.Tick(2);
            if (step.Type == ECraftGimmick.Open)
                return UniTask.FromResult(GimmickResult.Open(step.IngredientId, 1, 0, true, timer.ElapsedSec));
            if (step.Type == ECraftGimmick.Stir || step.Type == ECraftGimmick.Shake)
                return UniTask.FromResult(GimmickResult.Mix(step.Type, 1, 0, 1, ECraftEndType.AutoTarget, timer.ElapsedSec));
            return UniTask.FromResult(GimmickResult.Quantity(step.Type, step.IngredientId, step.TargetValue, step.TargetUnit,
                step.TargetValue ?? 0, step.IsImmediateAddition ? ECraftEndType.AutoTarget : ECraftEndType.ManualNext, timer.ElapsedSec));
        }
    }

    private static CraftSession NewSession()
    {
        var session = new CraftSession("test-cocktail");
        session.Actual.SetGlass("test-glass");
        foreach (string id in new[] { "gin", "sugar_cube", "soda_water" }) session.Actual.SelectIngredient(id);
        return session;
    }

    private static GimmickStep[] WholeDrinkSteps() => new[]
    {
        new GimmickStep(ECraftGimmick.Pour, "gin", 30, ENewUnit.Ml, true),
        new GimmickStep(ECraftGimmick.Powder, "sugar_cube", 1, ENewUnit.Count, true, true),
        new GimmickStep(ECraftGimmick.FillUp, "soda_water", 90, ENewUnit.Ml, true)
    };

    [Test]
    public void QuickTestAutomaticallyPicksOneValidExamplePerGimmickInStableOrder()
    {
        var cases = GimmickPlayTestCatalog.Load();
        var selected = GimmickPlayTestCatalog.SelectQuickCases(cases);
        var reversed = GimmickPlayTestCatalog.SelectQuickCases(cases.Reverse());
        Assert.That(selected.Select(value => value.Step.Type), Is.EqualTo(new[]
            { ECraftGimmick.Open, ECraftGimmick.Pour, ECraftGimmick.Stir, ECraftGimmick.Shake }));
        Assert.That(selected.Select(value => value.Key), Is.EqualTo(reversed.Select(value => value.Key)));
        var pour = selected.Single(value => value.Step.Type == ECraftGimmick.Pour);
        Assert.That(pour.Step.HasTarget, Is.True);
        Assert.That(pour.Step.TargetUnit.HasValue, Is.True);
        Assert.That(selected.All(value => !value.Step.IsImmediateAddition), Is.True);
    }

    [Test]
    public void QuickTestRunsAllFourWithTheirOwnAutomaticPreparationAndClosesEveryStage()
    {
        var selected = GimmickPlayTestCatalog.SelectQuickCases(GimmickPlayTestCatalog.Load());
        var executor = new RecordingExecutor { CheckFixedContext = false };
        var sessions = new List<CraftSession>();
        GimmickPlayTestSession.ExecuteQuickCasesAsync(executor, selected, (_, session, _) => sessions.Add(session),
            CancellationToken.None).GetAwaiter().GetResult();
        Assert.That(executor.Steps.Select(value => value.Type), Is.EqualTo(GimmickPlayTestCatalog.QuickTypes));
        Assert.That(executor.BeginCount, Is.EqualTo(4));
        Assert.That(executor.EndCount, Is.EqualTo(4));
        Assert.That(sessions.All(value => value.Phase == ECraftPhase.Completed), Is.True);
        Assert.That(sessions.Select(value => value.JobId).Distinct().Count(), Is.EqualTo(4));
        for (int i = 0; i < selected.Length; i++)
        {
            Assert.That(executor.Contexts[i].SelectedCocktailId, Is.EqualTo(selected[i].Cocktail.Id));
            Assert.That(executor.Contexts[i].GlassId, Is.EqualTo(selected[i].Cocktail.Glass));
            Assert.That(executor.Contexts[i].ToolId, Is.EqualTo(selected[i].Recipe.ToolId));
            Assert.That(sessions[i].Actual.GimmickResults.Single().Type, Is.EqualTo(selected[i].Step.Type));
            Assert.That(sessions[i].Timer.IsRunning, Is.False);
        }
    }

    [Test]
    public void CancellingQuickTestDoesNotStartTheRemainingGimmicks()
    {
        var selected = GimmickPlayTestCatalog.SelectQuickCases(GimmickPlayTestCatalog.Load());
        var executor = new RecordingExecutor { CheckFixedContext = false };
        var sessions = new List<CraftSession>();
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => GimmickPlayTestSession.ExecuteQuickCasesAsync(executor, selected,
            (_, session, index) =>
            {
                sessions.Add(session);
                if (index == 1) cancellation.Cancel();
            }, cancellation.Token).GetAwaiter().GetResult());
        Assert.That(executor.Steps.Select(value => value.Type), Is.EqualTo(new[] { ECraftGimmick.Open }));
        Assert.That(executor.BeginCount, Is.EqualTo(1));
        Assert.That(executor.EndCount, Is.EqualTo(1));
        Assert.That(sessions.Count, Is.EqualTo(2));
        Assert.That(sessions[0].Phase, Is.EqualTo(ECraftPhase.Completed));
        Assert.That(sessions[1].Phase, Is.EqualTo(ECraftPhase.Cancelled));
    }

    [Test]
    public void WholeCocktailUsesOneTimerAndRecordsImmediateAdditionsInOrder()
    {
        var executor = new RecordingExecutor();
        var session = NewSession();
        var steps = WholeDrinkSteps();
        var progress = new List<int>();
        GimmickPlayTestSession.ExecutePlanAsync(executor, session, new NewCocktailData { TimeLimitSec = 60 },
            steps, (index, _) => progress.Add(index), CancellationToken.None).GetAwaiter().GetResult();
        Assert.That(executor.BeginCount, Is.EqualTo(1));
        Assert.That(executor.EndCount, Is.EqualTo(1));
        Assert.That(executor.Steps.Select(value => value.Type), Is.EqualTo(steps.Select(value => value.Type)));
        Assert.That(progress, Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(session.Actual.GimmickResults.Select(value => value.CompletedAtSec), Is.EqualTo(new[] { 2f, 2f, 4f }));
        Assert.That(session.Actual.ElapsedManualSec, Is.EqualTo(4));
        Assert.That(session.Timer.IsRunning, Is.False);
        Assert.That(session.Phase, Is.EqualTo(ECraftPhase.Completed));
    }

    [Test]
    public void CancellingBetweenStepsStopsTheWholeDrinkAndAlwaysClosesTheExecutor()
    {
        var executor = new RecordingExecutor();
        var session = NewSession();
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => GimmickPlayTestSession.ExecutePlanAsync(executor, session,
            new NewCocktailData { TimeLimitSec = 60 }, WholeDrinkSteps(), (index, _) =>
            {
                if (index == 1) cancellation.Cancel();
            }, cancellation.Token).GetAwaiter().GetResult());
        Assert.That(executor.Steps.Count, Is.EqualTo(1));
        Assert.That(session.Actual.GimmickResults.Count, Is.EqualTo(1));
        Assert.That(executor.EndCount, Is.EqualTo(1));
        Assert.That(session.Timer.IsRunning, Is.False);
        Assert.That(session.Phase, Is.Not.EqualTo(ECraftPhase.Completed));
    }

    [Test]
    public void FullRecipesKeepTheStepIndexesOfIndividualGimmickCases()
    {
        var recipes = GimmickPlayTestCatalog.LoadRecipes();
        var cases = GimmickPlayTestCatalog.Cases(recipes);
        Assert.That(recipes.Select(value => value.Key).Distinct().Count(), Is.EqualTo(recipes.Length));
        foreach (var recipe in recipes)
        {
            var indexed = cases.Where(value => value.Recipe == recipe).ToArray();
            Assert.That(indexed.Length, Is.EqualTo(recipe.Steps.Count(value => !value.IsImmediateAddition)));
            foreach (var selected in indexed)
                Assert.That(selected.Step, Is.EqualTo(recipe.Steps[selected.Index]));
            if (recipe.Steps.Any(value => value.Type == ECraftGimmick.Open))
                Assert.That(recipe.CreateSession().Actual.ToolId, Is.EqualTo(NewToolIds.BottleOpener));
        }
    }

    [Test]
    public void LiveCatalogIncludesEveryCurrentInteractiveGimmickWithoutAutomaticAdditions()
    {
        var cases = GimmickPlayTestCatalog.Load();
        Assert.That(cases, Is.Not.Empty);
        Assert.That(cases.Select(value => value.Key).Distinct().Count(), Is.EqualTo(cases.Length));
        var types = cases.Select(value => value.Step.Type).Distinct().ToArray();
        foreach (var type in new[] { ECraftGimmick.Open, ECraftGimmick.Pour, ECraftGimmick.Shake, ECraftGimmick.Stir, ECraftGimmick.FillUp })
            Assert.That(types, Does.Contain(type), "실제 CSV로 준비할 수 있어야 하는 기믹: " + type);
        Assert.That(cases.All(value => !value.Step.IsImmediateAddition), Is.True,
            "정량 자동 투입을 실제 조작 미니게임처럼 표시하면 안 됩니다.");
    }

    [Test]
    public void ReplayingDoesNotReuseModifiedPreparationOrPreviousTimer()
    {
        var selected = GimmickPlayTestCatalog.Load().First(value => value.Step.Type == ECraftGimmick.Pour);
        var first = selected.CreateSession();
        var second = selected.CreateSession();
        var originalIngredients = second.Actual.IngredientIds.ToArray();
        first.Actual.SetGlass("changed-glass");
        foreach (string ingredient in first.Actual.IngredientIds.ToArray()) first.Actual.DeselectIngredient(ingredient);
        first.BeginGimmicks();
        first.Timer.Tick(12);
        Assert.That(second.Actual.GlassId, Is.EqualTo(selected.Cocktail.Glass));
        Assert.That(second.Actual.IngredientIds, Is.EqualTo(originalIngredients));
        Assert.That(second.Timer.ElapsedSec, Is.Zero);
        Assert.That(second.Timer.IsRunning, Is.False);
        Assert.That(second.JobId, Is.Not.EqualTo(first.JobId));
    }

    [TestCase(ECraftGimmick.Open)]
    [TestCase(ECraftGimmick.Pour)]
    [TestCase(ECraftGimmick.Shake)]
    [TestCase(ECraftGimmick.Stir)]
    [TestCase(ECraftGimmick.FillUp)]
    public void ActualPlayBindingsArePresentAndPreviewValidationPreservesOpenScenes(ECraftGimmick type)
    {
        var before = EditorSceneManager.GetSceneManagerSetup();
        GimmickPlayTestCatalog.ValidatePrefab(type);
        var after = EditorSceneManager.GetSceneManagerSetup();
        Assert.That(after.Select(value => (value.path, value.isLoaded, value.isActive)),
            Is.EqualTo(before.Select(value => (value.path, value.isLoaded, value.isActive))));
    }
}
