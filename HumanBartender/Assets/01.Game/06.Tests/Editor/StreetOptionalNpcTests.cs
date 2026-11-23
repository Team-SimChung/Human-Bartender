using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

/// <summary>
/// 웹 선택 NPC(신문 읽는 남자, 200원 사기꾼과 경고하는 행인, 기신교 신도)의 등장 조건·대본 선택·씬 배치를 검사한다.
/// 기준: 웹 outside-newspaper.js, newspaper-progress.js, outside-beggar.js, outside-believer.js.
/// </summary>
public sealed class StreetOptionalNpcTests
{
    private const string OutsidePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    private const string Newspaper = OutsideStorySceneRules.NewspaperEntry;
    private const string Beggar = OutsideStorySceneRules.BeggarEntry;
    private const string Witness = OutsideStorySceneRules.BeggarWitnessEntry;
    private const string Believer = OutsideStorySceneRules.BelieverEntry;

    private CsvStoryTestContent content;
    private PlayerDataSO player;
    private IConditionUtil conditions;

    [OneTimeSetUp]
    public void LoadAuthoredContent() => content = new CsvStoryTestContent();

    [SetUp]
    public void CreateProgress()
    {
        player = ScriptableObject.CreateInstance<PlayerDataSO>();
        conditions = new ConditionUtil(GameStateManager.Instance, player, player).CreateExecutionScope();
    }

    [TearDown]
    public void DestroyProgress() => UnityEngine.Object.DestroyImmediate(player);

    private bool Available(string id, int day, EGameFlow flow) => OutsideStorySceneRules.IsAvailable(id, day, flow, conditions);

    private Step[] Entry(string id, EGameFlow flow = EGameFlow.CommuteIn) => OutsideStorySceneRules.ResolveEntry(
        new NewSceneData { Id = id, Steps = content.GetSceneSteps(id) }, 0, conditions, content, flow).Steps;

    private static string[] Keys(IEnumerable<Step> steps) => steps.Select(step => step.Type + "|" + step.Actor + "|" + step.Text?.Ko + "|" + step.Effects).ToArray();

    [TestCase(Newspaper, 0, EGameFlow.CommuteIn, false)]
    [TestCase(Newspaper, 0, EGameFlow.CommuteOut, false)]
    [TestCase(Newspaper, 1, EGameFlow.CommuteIn, true)]
    [TestCase(Newspaper, 1, EGameFlow.CommuteOut, true)]
    [TestCase(Newspaper, 2, EGameFlow.CommuteIn, false)]
    [TestCase(Beggar, 0, EGameFlow.CommuteIn, false)]
    [TestCase(Beggar, 0, EGameFlow.CommuteOut, true)]
    [TestCase(Beggar, 1, EGameFlow.CommuteOut, false)]
    [TestCase(Witness, 0, EGameFlow.CommuteOut, true)]
    [TestCase(Witness, 1, EGameFlow.CommuteIn, false)]
    [TestCase(Believer, 0, EGameFlow.CommuteIn, false)]
    [TestCase(Believer, 0, EGameFlow.CommuteOut, true)]
    [TestCase(Believer, 1, EGameFlow.CommuteIn, true)]
    [TestCase(Believer, 1, EGameFlow.CommuteOut, true)]
    [TestCase(Believer, 2, EGameFlow.CommuteIn, false)]
    public void OptionalNpcsAppearOnTheWebDaysAndRoutes(string id, int day, EGameFlow flow, bool expected)
    {
        Assert.That(OutsideStorySceneRules.IsNativeEntry(id), Is.True);
        Assert.That(Available(id, day, flow), Is.EqualTo(expected));
    }

    [Test]
    public void FinishedNpcsLeaveExceptTheNewspaperOnTheRouteWhereHeWasFinished()
    {
        foreach (var step in Entry(Newspaper, EGameFlow.CommuteIn).Where(step => step.Type == "effect" && step.Effects?.Contains("talked") == true))
            conditions.ApplyRequired(step.Effects);
        player.AddFlag("newspaper_conversation_done", true);
        Assert.That(Available(Newspaper, 1, EGameFlow.CommuteIn), Is.True, "웹 available(flow): 대화를 끝낸 길에서는 남는다.");
        Assert.That(Available(Newspaper, 1, EGameFlow.CommuteOut), Is.False);

        player.AddFlag("street_beggar_paid", true);
        Assert.That(Available(Beggar, 0, EGameFlow.CommuteOut), Is.False, "200원을 받은 사기꾼은 떠난다.");
        Assert.That(Available(Witness, 0, EGameFlow.CommuteOut), Is.True);

        player.AddFlag("believer_conversation_done", true);
        Assert.That(Available(Believer, 1, EGameFlow.CommuteIn), Is.False);
    }

    [Test]
    public void NewspaperEntryFollowsTheGearProgressAndRecordsTheRoute()
    {
        var steps = Entry(Newspaper, EGameFlow.CommuteOut);
        Assert.That(steps[0].Effects, Is.EqualTo("flag.newspaper_talked_out = true"));
        Assert.That(Keys(steps.Skip(1)), Is.EqualTo(Keys(content.GetSceneSteps(Newspaper))));
        Assert.That(content.GetSceneSteps(Newspaper).Any(step => step.Options?.Length == 2), Is.True, "자살률 질문 선택지 두 개");

        player.AddFlag("newspaper_gear_received", true);
        Assert.That(Keys(Entry(Newspaper).Skip(1)), Is.EqualTo(Keys(content.GetSceneSteps("shared-newspaper-finish"))));
        player.AddFlag("newspaper_conversation_done", true);
        Assert.That(Keys(Entry(Newspaper).Skip(1)), Is.EqualTo(Keys(content.GetSceneSteps("shared-newspaper-repeat"))));
    }

    [Test]
    public void NewspaperCommonBranchGrantsTheElevatorGearOnce()
    {
        var grant = content.GetSceneSteps("shared-newspaper-common").Single(step => step.Effects?.Contains("has_gear_part") == true);
        Assert.That(grant.When, Is.EqualTo("!flag.newspaper_gear_received"));
        conditions.ApplyRequired(grant.Effects);
        Assert.That(player.CheckFlag("has_gear_part"), Is.True, "수리공에게 건넬 기어 부품");
    }

    [TestCase(false, false, false, false, "shared-beggar-warning-unmet")]
    [TestCase(true, false, false, false, "shared-beggar-warning-met")]
    [TestCase(true, false, true, false, "shared-beggar-warning-unpaid-repeat")]
    [TestCase(true, true, false, false, "shared-beggar-warning-paid")]
    [TestCase(true, true, true, true, "shared-beggar-warning-paid-repeat")]
    public void WitnessWarningMatchesWhatLunaDidWithTheBeggar(bool met, bool paid, bool witnessMet, bool paidSeen, string expected)
    {
        player.AddFlag("street_beggar_met", met);
        player.AddFlag("street_beggar_paid", paid);
        player.AddFlag("street_beggar_witness_met", witnessMet);
        player.AddFlag("street_beggar_witness_paid_seen", paidSeen);
        var steps = Entry(Witness);
        Assert.That(Keys(steps.Take(steps.Length - 1)), Is.EqualTo(Keys(content.GetSceneSteps(expected))));

        conditions.ApplyRequired(steps[^1].Effects);
        Assert.That(player.CheckFlag("street_beggar_warned"), Is.True, "경고를 들으면 사기꾼에게 질문할 수 있다.");
        Assert.That(player.CheckFlag("street_beggar_witness_met"), Is.True);
        Assert.That(player.CheckFlag("street_beggar_witness_paid_seen"), Is.EqualTo(paid));
    }

    [Test]
    public void BeggarAndBelieverRepeatAfterTheFirstMeeting()
    {
        Assert.That(Keys(Entry(Beggar)), Is.EqualTo(Keys(content.GetSceneSteps(Beggar))));
        player.AddFlag("street_beggar_met", true);
        Assert.That(Keys(Entry(Beggar)), Is.EqualTo(Keys(content.GetSceneSteps("shared-beggar-repeat"))));

        Assert.That(content.GetSceneSteps(Believer).Any(step => step.Arg == "machine_church_emblem"), Is.True);
        Assert.That(Keys(Entry(Believer)), Is.EqualTo(Keys(content.GetSceneSteps(Believer))));
        player.AddFlag("believer_emblem_received", true);
        Assert.That(Keys(Entry(Believer)), Is.EqualTo(Keys(content.GetSceneSteps("shared-believer-finish"))));
    }

    [Test]
    public void BeggarChoiceUsesTheWebLabelsAndLocks()
    {
        var events = new StreetStoryEvents(content, GameStateManager.Instance);
        NewStreetOptionData[] shown = null;
        OutsideStoryExecution Pick(string id) => new(conditions, (options, token) =>
        {
            shown = options;
            return UniTask.FromResult(options.Single(option => option.Id == id));
        }, null);
        Step[] Run(string id) => events.ExecuteAsync(OutsideStoryCommand.BeggarChoice, null, Pick(id), default).GetAwaiter().GetResult();

        player.SetMoney(100);
        var refused = Run("refuse");
        Assert.That(shown.Select(option => option.Text.Ko), Is.EqualTo(new[] { "네, 믿을게요 · 200원", "싫어요.", "???" }));
        Assert.That(shown[0].LockReason.Ko, Is.EqualTo("돈이 부족합니다."));
        Assert.That(shown[2].LockReason.Ko, Is.EqualTo("관련 정보가 필요합니다."));
        Assert.That(refused[0].Text.Ko, Is.EqualTo("싫어요."));
        Assert.That(player.CheckFlag("street_beggar_met"), Is.True);

        player.AddFlag("street_beggar_warned", true);
        refused = Run("refuse");
        Assert.That(shown[1].Text.Ko, Is.EqualTo("아니요."), "두 번째 만남부터는 \"아니요.\"");
        Assert.That(shown[2].Text.Ko, Is.EqualTo("혹시 사기꾼인가요?"));
        Assert.That(refused[0].Text.Ko, Is.EqualTo("아니요."));
        Assert.That(content.GetSceneSteps("shared-beggar-refused")[0].Text.Ko, Is.EqualTo("싫어요."), "원본 대본은 바뀌지 않는다.");

        player.SetMoney(500);
        Run("pay");
        Assert.That(player.HasMoney(), Is.EqualTo(300));
        Assert.That(player.CheckFlag("street_beggar_paid"), Is.True);
    }

    [Test]
    public void OutsideSceneHasOneNpcForEveryOptionalEncounter()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var scene = SceneManager.GetSceneByPath(OutsidePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(OutsidePath, OpenSceneMode.Additive);
        try
        {
            var targets = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true)).ToList();
            foreach (string id in new[] { Newspaper, Beggar, Witness, Believer })
            {
                var target = targets.Single(value => value.StorySceneId == id);
                Assert.That(target.Participant, Is.InstanceOf<NpcInteraction>(), id);
                var actor = target.GetComponentInChildren<NpcActor>(true);
                Assert.That(actor, Is.Not.Null, id);
                var animator = actor.GetComponent<Animator>();
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null, id);
                Assert.That(animator.runtimeAnimatorController.animationClips, Is.Not.Empty, id);
                Assert.That(target.GetComponentInChildren<SpriteRenderer>(true).sprite, Is.Not.Null, id);
                foreach (var child in target.GetComponentsInChildren<Transform>(true))
                {
                    Assert.That(child.name.Contains(' '), Is.False, child.name);
                    Assert.That(child.localScale, Is.EqualTo(Vector3.one), child.name);
                }
            }
            var newspaper = targets.Single(value => value.StorySceneId == Newspaper).GetComponentInChildren<Animator>(true).runtimeAnimatorController;
            Assert.That(newspaper.animationClips.Select(clip => clip.name), Is.SupersetOf(new[] { "Idle", "Down", "Lowered", "Up" }));
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }
}
