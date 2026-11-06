using System.Linq;
using HumanBartender.CutsceneStudio;
using NUnit.Framework;
using UnityEngine;

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

    [Test]
    public void ProjectedMemoryShakesWhenTheFirstTaggedGlyphAppears()
    {
        var row = new NewDialogueStepData
        {
            Type = ENewStepType.Say,
            Text = new LocalizedText { Ko = "A <shake>B</shake> <wave>C</wave> continues." }
        };
        using var projection = new CsvStoryCutsceneProjection("memory", new[] { row }, ELanguage.Ko);
        var host = new GameObject("Memory Test", typeof(RectTransform), typeof(Canvas), typeof(StudioStage));
        var stage = host.GetComponent<StudioStage>();
        try
        {
            stage.Build(projection.Sequence);
            var entry = StudioEvaluation.Clips(projection.Sequence).Single();
            const string displayed = "A B C continues.";
            Assert.That(stage.Dialogue.text, Is.EqualTo(displayed));
            Assert.That(entry.Clip.duration, Is.EqualTo(displayed.Length / 35.0).Within(.0001));

            stage.Evaluate(2.5 / 35);
            Assert.That(stage.Frame.anchoredPosition.y, Is.Zero);
            stage.Evaluate(3.5 / 35);
            Assert.That(stage.Dialogue.maxVisibleCharacters, Is.EqualTo(3));
            Assert.That(stage.Frame.anchoredPosition.y, Is.GreaterThan(0));
            stage.Evaluate(3.5 / 35, revealDialogue: true);
            Assert.That(stage.Frame.anchoredPosition.y, Is.Zero);
            stage.Evaluate(8 / 35.0);
            Assert.That(stage.Frame.anchoredPosition.y, Is.Zero);
        }
        finally { Object.DestroyImmediate(host); }
    }
}
