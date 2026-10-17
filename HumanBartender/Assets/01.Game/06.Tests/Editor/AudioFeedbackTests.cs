using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

public sealed class AudioFeedbackTests
{
    const string PrefabPath = "Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab";
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    GameObject root;

    internal sealed class SoundSpy : ISoundEffectPlayer
    {
        public readonly List<string> Keys = new();
        public void PlaySE(string name) => Keys.Add(name);
    }

    [SetUp] public void SetUp() => root = new GameObject("audio feedback test");
    [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(root);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);

    [Test]
    public void SharedPrefabResolvesEveryConfiguredDialogueAndGameplaySound()
    {
        var manager = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponentInChildren<SoundManager>(true);
        var sounds = Get<List<Sound>>(manager, "seList");
        Assert.That(sounds.GroupBy(sound => sound.name).All(group => group.Count() == 1), Is.True);
        var settings = DialoguePresentationSettings.Shared;
        var keys = new[] { settings.TypingSoundKey, settings.AdvanceSoundKey, settings.ChoiceHoverSoundKey,
            settings.ChoiceConfirmSoundKey, "SFX_pour", "SFX_stir", "SFX_shaker_hit", "SFX_bottle_open",
            "SFX_fizz", "SFX_money_change", "SFX_goal_completed", "SFX_notification" };
        foreach (string key in keys)
        {
            Assert.That(key, Is.Not.Null.And.Not.Empty);
            var sound = sounds.Single(item => item.name == key);
            Assert.That(sound.clip, Is.Not.Null, key);
            Assert.That(sound.clip.length, Is.GreaterThan(0f), key);
            Assert.That(sound.volume, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f), key);
        }
        var typing = sounds.Single(sound => sound.name == settings.TypingSoundKey);
        var clips = new[] { typing.clip }.Concat(typing.alternatives).ToArray();
        Assert.That(clips.Distinct().Count(), Is.EqualTo(6));
        Assert.That(clips.All(clip => clip != null && clip.length < 0.1f), Is.True,
            "Use short individual blips instead of the long full_preview recording.");
        Assert.That(Get<AudioOutputSettings>(manager, "output"), Is.SameAs(AudioOutputSettings.Shared));
    }

    [Test]
    public void TypingVariantsCycleWithoutChangingGameplayRandomState()
    {
        root.SetActive(false);
        var manager = root.AddComponent<SoundManager>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponentInChildren<SoundManager>(true);
        var sound = Get<List<Sound>>(prefab, "seList").Single(item => item.name == DialoguePresentationSettings.Shared.TypingSoundKey);
        var next = typeof(SoundManager).GetMethod("NextSeClip", Private);
        var expected = new[] { sound.clip }.Concat(sound.alternatives).ToArray();
        var state = UnityEngine.Random.state;
        for (int i = 0; i < expected.Length * 2; i++)
            Assert.That(next.Invoke(manager, new object[] { sound }), Is.SameAs(expected[i % expected.Length]));
        Assert.That(UnityEngine.Random.state, Is.EqualTo(state));
    }

    [TestCase("가", true)]
    [TestCase("A", true)]
    [TestCase("1", true)]
    [TestCase(" ", false)]
    [TestCase("!", false)]
    [TestCase("…", false)]
    public void TypingAudioAcceptsOnlyLettersAndDigits(string text, bool audible)
    {
        var type = typeof(DialogueTextPlayer).Assembly.GetType("DialogueTypingAudio");
        type.GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        var sound = new SoundSpy();
        var glyph = new TMP_CharacterInfo { index = 0, elementType = TMP_TextElementType.Character };
        var method = type.GetMethod("TryPlay");
        method.Invoke(null, new object[] { sound, DialoguePresentationSettings.Shared, text, glyph });
        method.Invoke(null, new object[] { sound, DialoguePresentationSettings.Shared, text, glyph });
        Assert.That(sound.Keys.Count, Is.EqualTo(audible ? 1 : 0), "At most one blip per frame across all bubbles.");
        glyph.elementType = TMP_TextElementType.Sprite;
        type.GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        method.Invoke(null, new object[] { sound, DialoguePresentationSettings.Shared, text, glyph });
        Assert.That(sound.Keys.Count, Is.EqualTo(audible ? 1 : 0));
    }

    [Test]
    public void PointerAndKeyboardFocusShareOneHoverAndDisabledChoicesStaySilent()
    {
        var go = new GameObject("button", typeof(RectTransform), typeof(Button), typeof(UISelectionAudio));
        go.transform.SetParent(root.transform);
        var feedback = go.GetComponent<UISelectionAudio>();
        var sound = new SoundSpy();
        feedback.Configure(sound, "hover");
        feedback.OnPointerEnter(new PointerEventData(null));
        feedback.OnSelect(new BaseEventData(null));
        Assert.That(sound.Keys, Is.EqualTo(new[] { "hover" }));
        go.GetComponent<Button>().interactable = false;
        feedback.OnPointerEnter(new PointerEventData(null));
        Assert.That(sound.Keys.Count, Is.EqualTo(1));
        go.SetActive(false);
        feedback.OnSelect(new BaseEventData(null));
        Assert.That(sound.Keys.Count, Is.EqualTo(1));
    }

    [Test]
    public void ChoiceConfirmSoundWaitsForHoldCompletionAndDoesNotDuplicateAfterReopening()
    {
        EventSystem previous = EventSystem.current;
        var events = root.AddComponent<EventSystem>();
        typeof(EventSystem).GetMethod("OnEnable", Private).Invoke(events, null);
        EventSystem.current = events;
        try
        {
            var sound = new SoundSpy();
            var view = root.AddComponent<UIDialogueChoiceView>();
            var choices = new GameObject("choices");
            choices.transform.SetParent(root.transform);
            var panels = new List<ChoicePanel>();
            for (int i = 0; i < 2; i++)
            {
                var item = new GameObject("choice " + i, typeof(RectTransform), typeof(ChoiceHoldButton), typeof(ChoicePanel));
                item.transform.SetParent(choices.transform);
                var label = new GameObject("label", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(item.transform);
                var panel = item.GetComponent<ChoicePanel>();
                Set(panel, "panel", item);
                Set(panel, "panelText", label.GetComponent<Text>());
                Set(panel, "button", item.GetComponent<ChoiceHoldButton>());
                panels.Add(panel);
            }
            Set(view, "choicesPanel", choices);
            Set(view, "choicePanels", panels);
            var builder = new ContainerBuilder();
            builder.RegisterInstance<ISoundEffectPlayer>(sound);
            using var container = builder.Build();
            container.Inject(view);
            int selected = 0;
            void Open() => view.ShowChoice(new[] { "allowed", "blocked" }, new[] { true, false }, _ => selected++);
            Open();
            panels[1].GetButton().onClick.Invoke();
            Assert.That(selected, Is.Zero);
            var button = (ChoiceHoldButton)panels[0].GetButton();
            var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left, pointerId = 1 };
            button.OnPointerEnter(pointer);
            button.OnPointerDown(pointer);
            button.OnPointerUp(pointer);
            button.OnPointerClick(pointer);
            Assert.That(sound.Keys.Count(key => key == DialoguePresentationSettings.Shared.ChoiceConfirmSoundKey), Is.Zero);
            button.OnPointerDown(pointer);
            Set(button, "holdStartedAt", Time.unscaledTimeAsDouble - 1.1);
            typeof(ChoiceHoldButton).GetMethod("Update", Private).Invoke(button, null);
            Assert.That(selected, Is.EqualTo(1));
            Assert.That(sound.Keys.Count(key => key == DialoguePresentationSettings.Shared.ChoiceConfirmSoundKey), Is.EqualTo(1));
            Open();
            button.onClick.Invoke();
            Assert.That(selected, Is.EqualTo(2));
            Assert.That(sound.Keys.Count(key => key == DialoguePresentationSettings.Shared.ChoiceConfirmSoundKey), Is.EqualTo(2));
        }
        finally
        {
            typeof(EventSystem).GetMethod("OnDisable", Private).Invoke(events, null);
            if (previous != null) EventSystem.current = previous;
        }
    }
}
