using System;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

public sealed class PopupCsvStepTests
{
    sealed class RecordingPopup : IPopupService
    {
        public bool Result = true;
        public int Calls;
        public PopupType Type;
        public string Data;
        public UniTask<bool> Popup(PopupType type, string data)
        {
            Calls++;
            Type = type;
            Data = data;
            return UniTask.FromResult(Result);
        }
    }

    [TestCase("ingredient_acquired", PopupType.IngredientAcquired)]
    [TestCase("character_registered", PopupType.CharacterRegistered)]
    [TestCase("affinity_increased", PopupType.AffinityIncreased)]
    [TestCase("affinity_decreased", PopupType.AffinityDecreased)]
    [TestCase("new_objective", PopupType.NewObjective)]
    [TestCase("objective_completed", PopupType.ObjectiveCompleted)]
    [TestCase("event_occurred", PopupType.EventOccurred)]
    public void CsvKindsSelectExpectedView(string arg, PopupType expected)
    {
        Assert.IsTrue(PopupCsvStep.TryParseType(arg, out var actual));
        Assert.AreEqual(expected, actual);
    }

    [Test]
    public void AwaitReturnsAfterStartAndPreservesIngredientId()
    {
        var popup = new RecordingPopup();
        var execution = PopupCsvStep.ExecuteAsync(popup, "ingredient_acquired", "sugar_cube", "nowait");
        Assert.AreEqual(UniTaskStatus.Succeeded, execution.Status);
        execution.GetAwaiter().GetResult();
        Assert.AreEqual(1, popup.Calls);
        Assert.AreEqual("sugar_cube", popup.Data);
        Assert.AreEqual(PopupType.IngredientAcquired, popup.Type);
    }

    [Test]
    public void FailedStartFailsTheStep()
    {
        var popup = new RecordingPopup { Result = false };
        Assert.Throws<InvalidOperationException>(() =>
            PopupCsvStep.ExecuteAsync(popup, "new_objective", null, "nowait").GetAwaiter().GetResult());
    }

    [TestCase("0", "nowait", "data")]
    [TestCase("tutorial", "nowait", "data")]
    [TestCase("event_occurred", "wait", "data")]
    [TestCase("affinity_increased", "nowait", "")]
    public void InvalidCsvDoesNotInvokeUi(string arg, string sync, string data)
    {
        var popup = new RecordingPopup();
        Assert.Throws<InvalidOperationException>(() =>
            PopupCsvStep.ExecuteAsync(popup, arg, data, sync).GetAwaiter().GetResult());
        Assert.Zero(popup.Calls);
    }
}
