using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ChoiceExampleTests
{
    const string ScenePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    const string StoryId = "outside_interaction_money_example";

    [Test]
    public void ExampleHasUniqueStoryAndPlayableSpriteAnimation()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var targets = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true)).ToArray();
            Assert.IsEmpty(targets.GroupBy(target => target.RegistryKey).Where(group => group.Count() > 1), "Each authored target must own a unique CSV story");
            var example = targets.Single(target => target.StorySceneId == StoryId);
            Assert.AreEqual("ChoiceExample", example.name);
            Assert.AreEqual(WorldInteractionOutput.SpeechBubble, example.Output);
            Assert.IsNull(example.GetComponent<Animation>(), "A non-legacy clip needs an Animator");
            var animator = example.GetComponent<Animator>();
            Assert.NotNull(animator.runtimeAnimatorController);
            var clip = animator.runtimeAnimatorController.animationClips.Single();
            Assert.IsFalse(clip.legacy);
            Assert.IsTrue(clip.isLooping);
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
            Assert.AreEqual(typeof(SpriteRenderer), binding.type);
            Assert.AreEqual("m_Sprite", binding.propertyName);
            Assert.AreEqual("", binding.path, "Animate the renderer on the same object");
            var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            Assert.AreEqual(6, frames.Length);
            Assert.AreEqual(6, frames.Select(frame => frame.value).Distinct().Count());
            Assert.That(frames.All(frame => frame.value is Sprite));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest, Explicit("Run alone: opens OutSide and checks the authored money-test NPC without changing the saved wallet.")]
    public IEnumerator ExampleAnimatesAndRunsMoneyChoices()
    {
        EditorSceneManager.OpenScene(ScenePath);
        yield return new EnterPlayMode();
        yield return RunAfterDomainReload();
        yield return new ExitPlayMode();
    }

    static IEnumerator RunAfterDomainReload()
    {
        float deadline = Time.realtimeSinceStartup + 45;
        WorldInteractionController controller;
        while ((controller = UnityEngine.Object.FindFirstObjectByType<WorldInteractionController>()) == null || !controller.IsReady)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "Interaction initialization timed out");
            yield return null;
        }
        Exception failure = null;
        bool done = false;
        controller.StartCoroutine(InPlayerLoop());
        IEnumerator InPlayerLoop()
        {
            var routine = Exercise(controller).ToCoroutine(error => failure = error);
            while (routine.MoveNext()) yield return routine.Current;
            done = true;
        }
        while (!done) yield return null;
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

    static async UniTask Exercise(WorldInteractionController controller)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var token = timeout.Token;
        var target = WorldInteractionTarget.InScene(controller.gameObject.scene).Single(t => t.StorySceneId == StoryId);
        Assert.IsTrue(controller.TryResolve(target, out var story), "Authored object resolves without the duplicate-ID error");
        var pay = story.Steps.Single(step => step.Type == "choice").Options.Single(option => option.Id == "money-example-pay");
        Assert.AreEqual("money >= 500", pay.When);
        Assert.AreEqual("선택할 수 없습니다. (소지금 부족)", pay.LockReason.Ko);
        var renderer = target.GetComponent<SpriteRenderer>();
        var animator = target.GetComponent<Animator>();
        var frames = new HashSet<Sprite>();
        float until = Time.realtimeSinceStartup + 1.2f;
        do { frames.Add(renderer.sprite); await UniTask.NextFrame(token); } while (Time.realtimeSinceStartup < until);
        Assert.AreEqual(6, frames.Count, "All six sprite frames actually play");
        Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).loop);
        Assert.Greater(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1, "Idle continues past one loop");

        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        var runner = (DialogueRunner)Field(typeof(WorldInteractionController), "runner").GetValue(controller);
        var readerField = Field(typeof(WorldInteractionController), "playerData");
        var controllerConditions = Field(typeof(WorldInteractionController), "conditions");
        var runnerConditions = Field(typeof(DialogueRunner), "conditionUtil");
        var originalReader = readerField.GetValue(controller);
        var originalControllerConditions = controllerConditions.GetValue(controller);
        var originalRunnerConditions = runnerConditions.GetValue(runner);
        var originalPosition = player.transform.position;
        var wallet = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var conditions = new ConditionUtil(GameStateManager.Instance, wallet, wallet);
            readerField.SetValue(controller, wallet);
            controllerConditions.SetValue(controller, conditions);
            runnerConditions.SetValue(runner, conditions);
            player.transform.position = target.transform.position + new Vector3(.5f, -.25f);
            await UniTask.NextFrame(token);
            Assert.AreEqual(target, controller.FocusedTarget);
            foreach (int money in new[] { 300, 500 })
            {
                wallet.SetMoney(money);
                var run = controller.PlayAsync(target);
                await UniTask.WaitUntil(() => runner.CurrentState == DialogueState.WaitingForInput, cancellationToken: token);
                runner.AdvanceInputOutside();
                await UniTask.WaitUntil(() => controller.View.IsChoosing && !controller.View.Choices.IsRevealing, cancellationToken: token);
                var menu = controller.View.Choices;
                Assert.AreEqual(money >= 500, menu.Rows[0].Button.interactable);
                Assert.AreEqual(money < 500, menu.Rows[0].LockReasonLabel.gameObject.activeSelf);
                if (money < 500)
                {
                    menu.Rows[0].Button.onClick.Invoke();
                    Assert.AreEqual(DialogueState.WaitingForChoice, runner.CurrentState);
                    Assert.IsTrue(menu.Rows[1].IsFocused);
                }
                float animationTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                await UniTask.Delay(200, cancellationToken: token);
                Assert.Greater(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, animationTime, "Idle also runs during dialogue");
                await SavePreview(money == 300 ? "choice-example-locked" : "choice-example-affordable", token);
                menu.Confirm();
                if (money >= 500)
                {
                    await UniTask.WaitUntil(() => runner.CurrentState == DialogueState.WaitingForInput, cancellationToken: token);
                    Assert.AreEqual("500원을 지불했습니다.", controller.View.Bubble.textLabel.text);
                    runner.AdvanceInputOutside();
                }
                Assert.IsTrue((await run).Completed);
                Assert.AreEqual(money < 500 ? money : money - 500, wallet.HasMoney());
                Assert.AreEqual(EInteractorState.None, player.State);
            }
            var canceled = controller.PlayAsync(target);
            await UniTask.NextFrame(token);
            controller.Cancel();
            Assert.AreEqual(StoryExecutionStatus.Cancelled, (await canceled).Status);
            Assert.AreEqual(EInteractorState.None, player.State);
        }
        finally
        {
            controller.Cancel();
            await runner.StopAsync();
            readerField.SetValue(controller, originalReader);
            controllerConditions.SetValue(controller, originalControllerConditions);
            runnerConditions.SetValue(runner, originalRunnerConditions);
            player.transform.position = originalPosition;
            UnityEngine.Object.Destroy(wallet);
        }
    }

    static UniTask SavePreview(string name, CancellationToken token) => (UniTask)typeof(WorldInteractionTests)
        .GetMethod("SavePreview", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { name, token });
}
