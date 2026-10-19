using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DG.Tweening;

public sealed class AudioGuideTests
{
    const string Prefab = "Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab";

    [TestCase(0, EGameFlow.CommuteOut, true)]
    [TestCase(1, EGameFlow.CommuteOut, true)]
    [TestCase(2, EGameFlow.CommuteIn, true)]
    [TestCase(0, EGameFlow.CommuteIn, false)]
    [TestCase(1, EGameFlow.CommuteIn, false)]
    [TestCase(2, EGameFlow.CommuteOut, false)]
    [TestCase(3, EGameFlow.CommuteIn, false)]
    [TestCase(0, EGameFlow.Home, false)]
    public void RainUsesOnlyTheThreeDocumentedRoutes(int day, EGameFlow route, bool expected)
        => Assert.That(GameAudioRules.IsRainy(day, route), Is.EqualTo(expected));

    [Test]
    public void DialogueAndPauseGainsDoNotMultiplyEachOther()
    {
        Assert.That(GameAudioRules.AmbienceGain(true, false), Is.EqualTo(.65f));
        Assert.That(GameAudioRules.AmbienceGain(true, true), Is.EqualTo(.45f));
        Assert.That(GameAudioRules.AmbienceGain(false, false), Is.EqualTo(1f));
        Assert.That(GameAudioRules.PourGain(.25f), Is.EqualTo(.5f).Within(.0001f));
        Assert.That(GameAudioRules.PourGain(10f), Is.EqualTo(Mathf.Sqrt(1.4f)).Within(.0001f));
    }

    [Test]
    public void NestedCutscenesRestoreOnlyAfterTheLastOwnerLeaves()
    {
        Assert.That(GameAudioContext.Cinematic, Is.False);
        var outer = GameAudioContext.Cutscene(true);
        var inner = GameAudioContext.Cutscene(false);
        try
        {
            Assert.That(GameAudioContext.MusicSuppressed, Is.True);
            outer.Dispose(); outer.Dispose();
            Assert.That(GameAudioContext.Cinematic, Is.True);
            Assert.That(GameAudioContext.MusicSuppressed, Is.False);
        }
        finally { outer.Dispose(); inner.Dispose(); }
        Assert.That(GameAudioContext.Cinematic, Is.False);
    }

    [Test]
    public void LoadAndDuplicateStateWritesDoNotEmitGameplayNotifications()
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var money = new List<int>(); var affinity = new List<int>(); var flags = new List<string>();
            player.MoneyAdded += money.Add;
            player.AffinityAdded += (_, delta) => affinity.Add(delta);
            player.FlagChanged += (key, _) => flags.Add(key);
            player.SetMoney(50);
            player.AddMoney(-100);
            player.AddMoney(-1);
            player.SetCharacterAffinityAmount("tom", 20);
            player.AddCharacterAffinityAmount("tom", 0);
            player.AddCharacterAffinityAmount("tom", -2);
            player.AddFlag("flag.tom_met", true);
            player.AddFlag("tom_met", true);
            player.ReplaceProgress(100, 0, new Dictionary<string, int>(), new Dictionary<string, bool>());
            Assert.That(money, Is.EqualTo(new[] { -50 }));
            Assert.That(affinity, Is.EqualTo(new[] { -2 }));
            Assert.That(flags, Is.EqualTo(new[] { "tom_met" }));
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }

    [Test]
    public void DefaultCraftingClipsNeverAlternateWithTestClips()
    {
        var manager = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab).GetComponentInChildren<SoundManager>(true);
        var list = (List<Sound>)typeof(SoundManager).GetField("seList", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        Assert.That(list.Select(sound => sound.name).Distinct().Count(), Is.EqualTo(list.Count));
        foreach (string key in new[] { "SFX_stir", "SFX_shaker_hit" })
            Assert.That(list.Single(sound => sound.name == key).alternatives, Is.Empty);
        Assert.That(list.Single(sound => sound.name == "SFX_stir_alternate").clip.name, Is.EqualTo("stir_loop_alternate"));
        Assert.That(list.Single(sound => sound.name == "SFX_shaker_hit_alternate").clip.name, Is.EqualTo("shaker_hit_alternate"));
        foreach (var sound in list) Assert.That(sound.clip, Is.Not.Null, sound.name);
    }

    [Test]
    public void PopupCueWaitsForVisibleMotionAndFiresOnlyOnce()
    {
        var root = new GameObject("popup visibility test", typeof(RectTransform), typeof(Canvas));
        UIPopupView view = null;
        Tween movement = null;
        try
        {
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var panel = new GameObject("panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 100);
            view = panel.AddComponent<UIPopupView>();
            typeof(UIPopupView).GetField("panel", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(view, panel.GetComponent<RectTransform>());
            int cues = 0;
            view.Shown += () => cues++;
            Assert.That(view.Popup(""), Is.True);
            Assert.That(cues, Is.Zero, "Enqueuing/starting an offscreen popup must stay silent.");
            movement = (Tween)typeof(UIPopupView).GetField("movement", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            movement.SetUpdate(UpdateType.Manual, true);
            DOTween.ManualUpdate(.4f, .4f);
            Assert.That(cues, Is.EqualTo(1));
            DOTween.ManualUpdate(.4f, .4f);
            Assert.That(cues, Is.EqualTo(1));
        }
        finally { movement?.Kill(); UnityEngine.Object.DestroyImmediate(root); }
    }
}
