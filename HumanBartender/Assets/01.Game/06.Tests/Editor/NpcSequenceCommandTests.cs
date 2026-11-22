using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>이름으로 호출하는 공통 NPC 연출이 기존 CSV·대화 실행 순서와 취소 수명을 유지하는지 검사한다.</summary>
public sealed class NpcSequenceCommandTests
{
    private SceneSetup[] sceneSetup;
    private PlayerDataSO player;
    private DialogueRunner runner;
    private readonly List<string> order = new();

    private sealed class Presenter : IDialoguePresenter, IDialogueAutomaticPlayback
    {
        private readonly List<string> lines;
        public Presenter(List<string> lines) => this.lines = lines;
        public void SkipTyping() { }
        public void HideDialogue() { }
        public void EndScene() { }
        public EActivationMode GetPlayMode() => EActivationMode.Interact;
        public UniTask ShowDialogueAsync(string actor, string text, string arg, CancellationToken token)
        { token.ThrowIfCancellationRequested(); lines.Add(text); return UniTask.CompletedTask; }
        public void ShowOutsideChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> selected) => selected(options[0]);
        public UniTask WaitUntilReadyAsync(CancellationToken token) => UniTask.CompletedTask;
        public UniTask WaitAfterLineAsync(CancellationToken token) => UniTask.CompletedTask;
    }

    [SetUp]
    public void SetUp()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        player = ScriptableObject.CreateInstance<PlayerDataSO>();
        runner = new GameObject("Named NPC sequence CSV test").AddComponent<DialogueRunner>();
        var conditions = new ConditionUtil(GameStateManager.Instance, player, player);
        typeof(DialogueRunner).GetField("conditionUtil", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runner, conditions);
        typeof(DialogueRunner).GetField("storyEvents", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runner,
            new OutsideStoryEvents(new[] { new StreetStoryEvents(null, GameStateManager.Instance) }));
        order.Clear();
        runner.Bind(new Presenter(order));
    }

    [TearDown]
    public void TearDown()
    {
        if (runner != null) Object.DestroyImmediate(runner.gameObject);
        if (player != null) Object.DestroyImmediate(player);
        if (sceneSetup != null && sceneSetup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
    }

    private static Step Say(string text) => new() { Type = "say", Actor = "luna", Text = new Texts { Ko = text, En = text } };
    private static Step[] Compile(params Step[] steps) => StoryCsvCompiler.Compile(new NewStreetData
    { Scenes = new[] { new NewSceneData { Id = "named-sequence-test", Steps = steps } } }).Scenes[0].Steps;

    [TestCase(false)]
    [TestCase(true)]
    public void CsvCompilesArbitrarySequenceNamesInsideChoicesWithoutChangingTheSource(bool webMetadata)
    {
        var instruction = new Step
        {
            Type = webMetadata ? "effect" : "npc-sequence", Arg = "custom-greeting-42",
            WebFields = webMetadata ? new Dictionary<string, object> { ["type"] = "npc-sequence" } : null
        };
        var source = new Step { Type = "choice", Options = new[] { new NewStreetOptionData
            { Id = "greet", ResultSteps = new[] { instruction } } } };
        var compiled = Compile(source)[0].Options[0].ResultSteps[0];
        Assert.That(compiled.Command, Is.EqualTo(OutsideStoryCommand.NpcSequence));
        Assert.That(compiled.Arg, Is.EqualTo("custom-greeting-42"));
        Assert.That(compiled.Type, Is.EqualTo("event"));
        Assert.That(compiled.WebFields, Is.Null);
        Assert.That(source.Options[0].ResultSteps[0].Command, Is.EqualTo(OutsideStoryCommand.None));
        Assert.That(source.Options[0].ResultSteps[0].Type, Is.EqualTo(webMetadata ? "effect" : "npc-sequence"));
        if (webMetadata) Assert.That(source.Options[0].ResultSteps[0].WebFields["type"], Is.EqualTo("npc-sequence"));
        Assert.Throws<InvalidOperationException>(() => Compile(new Step { Type = "npc-sequence", Arg = " " }));
    }

    [Test]
    public void DialogueWaitsForNamedSequencesBeforeApplyingEffectsAndContinuingLines()
    {
        var release = new UniTaskCompletionSource();
        var script = Compile(Say("before"), new Step { Type = "npc-sequence", Arg = "approach",
            Effects = "flag.sequence_finished = true" }, new Step { Type = "npc-sequence", Arg = "leave" }, Say("after"));
        var operation = runner.PlayOutsideAsync(script, playNpcSequence: (key, token) =>
        {
            order.Add("sequence:" + key);
            return key == "approach" ? release.Task : UniTask.CompletedTask;
        });
        Assert.That(operation.Status, Is.EqualTo(UniTaskStatus.Pending));
        Assert.That(order, Is.EqualTo(new[] { "before", "sequence:approach" }));
        Assert.That(player.CheckFlag("sequence_finished"), Is.False);
        release.TrySetResult();
        var result = operation.GetAwaiter().GetResult();
        Assert.That(result.Completed, Is.True, result.Error);
        Assert.That(order, Is.EqualTo(new[] { "before", "sequence:approach", "sequence:leave", "after" }));
        Assert.That(player.CheckFlag("sequence_finished"), Is.True);
        Assert.That(runner.IsRunning, Is.False);
    }

    [Test]
    public void CancelledSequenceDoesNotApplyItsEffectsAndTheSameDialogueCanRetry()
    {
        using var cancellation = new CancellationTokenSource();
        CancellationTokenRegistration registration = default;
        var release = new UniTaskCompletionSource();
        var script = Compile(new Step { Type = "npc-sequence", Arg = "long-animation",
            Effects = "flag.sequence_finished = true" }, Say("after"));
        try
        {
            var operation = runner.PlayOutsideAsync(script, cancellation.Token, playNpcSequence: (key, token) =>
            {
                registration = token.Register(() => release.TrySetCanceled(token));
                return release.Task;
            });
            Assert.That(operation.Status, Is.EqualTo(UniTaskStatus.Pending));
            cancellation.Cancel();
            Assert.That(operation.GetAwaiter().GetResult().Status, Is.EqualTo(StoryExecutionStatus.Cancelled));
            Assert.That(player.CheckFlag("sequence_finished"), Is.False);
            Assert.That(order, Is.Empty);
            Assert.That(runner.IsRunning, Is.False);
            var retry = runner.PlayOutsideAsync(script, playNpcSequence: (key, token) => UniTask.CompletedTask).GetAwaiter().GetResult();
            Assert.That(retry.Completed, Is.True, retry.Error);
            Assert.That(player.CheckFlag("sequence_finished"), Is.True);
            Assert.That(order, Is.EqualTo(new[] { "after" }));
        }
        finally { registration.Dispose(); }
    }

    [Test]
    public void MissingSequenceBindingAndUnknownNamesFailWithoutApplyingLaterEffects()
    {
        var script = Compile(new Step { Type = "npc-sequence", Arg = "missing-key", Effects = "flag.sequence_finished = true" });
        var missingBinding = runner.PlayOutsideAsync(script).GetAwaiter().GetResult();
        Assert.That(missingBinding.Status, Is.EqualTo(StoryExecutionStatus.Failed));
        Assert.That(missingBinding.Error, Does.Contain("missing-key"));
        Assert.That(player.CheckFlag("sequence_finished"), Is.False);
        var missingName = runner.PlayOutsideAsync(script, playNpcSequence: (key, token) =>
            UniTask.FromException(new InvalidOperationException("Unknown sequence: " + key))).GetAwaiter().GetResult();
        Assert.That(missingName.Status, Is.EqualTo(StoryExecutionStatus.Failed));
        Assert.That(missingName.Error, Does.Contain("missing-key"));
        Assert.That(player.CheckFlag("sequence_finished"), Is.False);
        Assert.That(runner.IsRunning, Is.False);
    }
}
