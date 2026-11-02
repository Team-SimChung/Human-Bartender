using System.Linq;
using HumanBartender.CutsceneStudio;
using NUnit.Framework;

public sealed class CsvStoryMemoryTests
{
    [TestCase("johnny-memory", null, "johnny")]
    [TestCase("terrace-memory", null, "terraceMemory")]
    [TestCase("terrace-memory", "terraceMemory3", "terraceMemory3")]
    public void MemoryUsesCurrentCsvRowsAndOwnsOnlyTemporaryStudioObjects(string id, string key, string source)
    {
        Assert.That(CsvStoryCutsceneProjection.TryGetSource(id, key, out var resolved), Is.True);
        Assert.That(resolved, Is.EqualTo(source));
        var rows = new CsvStoryTestContent().GetCutsceneSteps(source);
        var projection = new CsvStoryCutsceneProjection(id, rows, ELanguage.Ko);
        var sequence = projection.Sequence;
        var timeline = sequence.Timeline;
        var clips = StudioEvaluation.Clips(sequence);
        Assert.That(sequence.Validate(), Is.Empty);
        Assert.That(sequence.AllowSkip, Is.False);
        Assert.That(clips.Count, Is.EqualTo(rows.Length));
        for (int i = 0; i < rows.Length; i++)
        {
            Assert.That(clips[i].Asset.Text, Does.Contain(rows[i].Text?.Ko));
            Assert.That(clips[i].Asset.WaitForInput, Is.True);
        }
        projection.Dispose();
        Assert.That(sequence == null && timeline == null && clips.All(clip => clip.Asset == null), Is.True);
    }

    [Test]
    public void HomeMemoryKeySurvivesCsvCompilation()
    {
        var data = new CsvStoryTestContent();
        Assert.That(data.GetSceneSteps("ending").Any(step => step.MemoryKey == "terraceMemory3"), Is.True);
    }
}
