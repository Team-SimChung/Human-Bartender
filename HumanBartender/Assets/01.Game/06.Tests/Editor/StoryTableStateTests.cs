using System;
using NUnit.Framework;

public sealed class StoryTableStateTests
{
    [Test]
    public void CoastersBelongToVisitsAndAreIndependentForTwoActors()
    {
        var state = new StoryTableState();
        state.Enter("tom"); state.Enter("chris");
        state.PlaceCoaster("tom");
        Assert.That(state.Get("tom").HasCoaster, Is.True);
        Assert.That(state.Get("chris").HasCoaster, Is.False);
        state.PlaceCoaster("chris"); state.Leave("tom"); state.Enter("tom");
        Assert.That(state.Get("tom").HasCoaster, Is.False);
        Assert.That(state.Get("chris").HasCoaster, Is.True);
    }

    [Test]
    public void EmptyGlassDoesNotCreateADrinkAndRejectsMissingActor()
    {
        var state = new StoryTableState(); state.Enter("tom");
        state.EmptyGlass("tom"); state.EmptyGlass("tom");
        Assert.That(state.Get("tom").Drink, Is.Null);
        Assert.That(state.Get("tom").GlassEmpty, Is.False);
        Assert.Throws<InvalidOperationException>(() => state.EmptyGlass("chris"));
    }

    [Test]
    public void CancellationCleanupCannotLeaveVisitStateForNextRun()
    {
        var state = new StoryTableState(); state.Enter("tom"); state.PlaceCoaster("tom");
        state.Clear(); state.Enter("tom");
        Assert.That(state.Get("tom").HasCoaster, Is.False);
        Assert.That(state.Get("tom").Drink, Is.Null);
    }
}
