using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class CampaignStoryTests
{
    sealed class MissingCutscene : ICutScenePlayer
    {
        public int Cleared;
        public UniTask PlayCutScene(string id, UniTaskCompletionSource tcs = null, CancellationToken token = default)
            => throw new InvalidOperationException("missing timeline/prefab");
        public void ClearCutScene() => Cleared++;
        public void OnContinueTimeline() { }
    }

    sealed class TextPresenter : IDialoguePresenter
    {
        public readonly List<string> Lines = new();
        public int Closed;
        public void SkipTyping() { }
        public void HideDialogue() { }
        public void EndScene() => Closed++;
        public EActivationMode GetPlayMode() => EActivationMode.Interact;
        public UniTask ShowDialogueAsync(string actor, string text, string arg, CancellationToken token)
        { Lines.Add(text); return UniTask.CompletedTask; }
        public void ShowOutsideChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> selected) => selected(options[0]);
    }

    [UnityTest]
    public IEnumerator MissingCutsceneContinuesToClickableDialogueAndRestoresUI()
    {
        var routine = MissingCutsceneCase().ToCoroutine();
        while (routine.MoveNext()) { EditorApplication.QueuePlayerLoopUpdate(); yield return routine.Current; }
    }

    async UniTask MissingCutsceneCase()
    {
        var go = new GameObject("campaign dialogue test");
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var runner = go.AddComponent<DialogueRunner>();
            var presenter = new TextPresenter();
            var missing = new MissingCutscene();
            typeof(DialogueRunner).GetField("conditionUtil", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(runner, new ConditionUtil(GameStateManager.Instance, player, player));
            typeof(DialogueRunner).GetField("cutscenes", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(runner, missing);
            runner.Bind(presenter);
            var run = runner.PlayOutsideAsync(new[] {
                new Step { Seq=1, Type="timeline", Arg="missing", Sync="wait" },
                new Step { Seq=2, Type="say", Actor="luna", Text=new Texts { Ko="다음 일반 대사" }, Sync="wait" },
                new Step { Seq=3, Type="effect", Effects="flag.after_missing_cutscene = true" },
            });
            Assert.AreEqual(DialogueState.WaitingForInput, runner.CurrentState);
            Assert.AreEqual(UniTaskStatus.Pending, run.Status);
            Assert.AreEqual(new[] { "다음 일반 대사" }, presenter.Lines);
            runner.AdvanceInputOutside();
            Assert.IsTrue((await run).Completed);
            Assert.AreEqual(1, missing.Cleared);
            Assert.AreEqual(1, presenter.Closed);
            Assert.IsTrue(player.CheckFlag("after_missing_cutscene"));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(player); }
    }

    [Test]
    public void CancellationIsNotTreatedAsAnUnavailableCutscene()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var task = OptionalStoryCutscene.PlayAsync(new MissingCutscene(), "missing", canceled.Token);
        Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
    }

    [Test]
    public void MoreThanThreeChoicesRemainReachableAndKeepTheirOriginalSelectionIndex()
    {
        var root = new GameObject("choice pagination test");
        try
        {
            var view = root.AddComponent<UIDialogueChoiceView>();
            var panels = new List<ChoicePanel>();
            for (int i = 0; i < 3; i++)
            {
                var item = new GameObject("choice " + i, typeof(RectTransform), typeof(Button), typeof(Text));
                item.transform.SetParent(root.transform);
                var panel = item.AddComponent<ChoicePanel>();
                SetField(panel, "panel", item);
                SetField(panel, "panelText", item.GetComponent<Text>());
                SetField(panel, "button", item.GetComponent<Button>());
                panels.Add(panel);
            }
            SetField(view, "choicesPanel", root);
            SetField(view, "choicePanels", panels);
            int selected = -1;
            view.ShowChoice(new[] { "잠금", "두 번째", "세 번째", "네 번째", "대화 그만하기" },
                new[] { false, true, true, true, true }, index => selected = index);
            Assert.IsFalse(panels[0].GetButton().interactable);
            panels[2].GetButton().onClick.Invoke();
            Assert.AreEqual(-1, selected, "A page button must not select a story option.");
            panels[2].GetButton().onClick.Invoke();
            Assert.AreEqual("대화 그만하기", panels[0].GetComponent<Text>().text);
            panels[0].GetButton().onClick.Invoke();
            Assert.AreEqual(4, selected);
            Assert.IsFalse(root.activeSelf);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void SetField(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    [Test]
    public void LatestStoryHasExecutableRoutesOrdersAndNestedChoiceTargets()
    {
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
        var cocktailIds = catalog.Read<NewCocktailData[]>("cocktails").Select(c => c.Id).ToHashSet();
        var doors = catalog.Read<NewInteractPointData[]>("interact_points_outside");
        foreach (var id in new[] { "p_enter_bar", "p_enter_home" })
            Assert.AreEqual("day <= 3", doors.Single(p => p.Id == id).SpawnWhen, id);
        for (int day=0; day<=3; day++)
        {
            var script = catalog.Read<NewDayScriptBase>("script/bar/day" + day);
            Assert.IsTrue(script.Scenes.Any(s => s.Day == day && s.Phase == ENewScenePhase.Bar));
            if (day > 0) Assert.IsTrue(script.Scenes.Any(s => s.Phase == ENewScenePhase.BarOpen));
            foreach (var scene in script.Scenes)
                foreach (var step in scene.Steps)
                {
                    Assert.IsNull(ConditionUtil.ValidateConditionSyntax(step.When), scene.Id);
                    Assert.IsNull(ConditionUtil.ValidateEffectSyntax(step.Effects), scene.Id);
                    if (step.Type == ENewStepType.Order && step.Arg != "free")
                        Assert.IsTrue(cocktailIds.Contains(step.Arg.Substring("exact:".Length)), step.Arg);
                    if (step.Type == ENewStepType.Choice) Assert.IsTrue(script.Choices.ContainsKey(step.Arg));
                }
        }
        foreach (var id in new[] { "script/street", "script/home" })
        {
            var story = catalog.Read<NewStreetData>(id);
            var sceneIds = story.Scenes.Select(s => s.Id).ToHashSet();
            foreach (var scene in story.Scenes)
            {
                Assert.IsTrue(scene.RuntimeScene == "Home" || scene.RuntimeScene == "OutSide");
                Assert.IsNull(ConditionUtil.ValidateConditionSyntax(scene.When), scene.Id);
                CheckSteps(scene.Steps, sceneIds);
            }
        }
    }

    static void CheckSteps(Step[] steps, HashSet<string> scenes)
    {
        if (steps == null) return;
        // Legacy QA result steps omit seq (zero); the runner preserves their CSV order.
        var numbered = steps.Where(s => s.Seq != 0).ToArray();
        Assert.AreEqual(numbered.Length, numbered.Select(s => s.Seq).Distinct().Count());
        foreach (var step in steps)
        {
            Assert.IsNull(ConditionUtil.ValidateConditionSyntax(step.When));
            Assert.IsNull(ConditionUtil.ValidateEffectSyntax(step.Effects));
            if (step.Type == "say") Assert.IsFalse(string.IsNullOrEmpty(step.Text?.Ko));
            if (step.Type == "goto") Assert.IsTrue(scenes.Contains(step.SceneId), step.SceneId);
            foreach (var option in step.Options ?? Array.Empty<NewStreetOptionData>())
            {
                Assert.IsNull(ConditionUtil.ValidateConditionSyntax(option.When));
                CheckSteps(option.ResultSteps, scenes);
            }
        }
    }
}
