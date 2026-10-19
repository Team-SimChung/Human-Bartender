using System;
using System.Collections;
using HumanBartender.CutsceneStudio;
using HumanBartender.CutsceneStudio.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Timeline;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public sealed class StudioPropertiesPanelTests
{
    private StudioSequence sequence;
    private StudioTrack track;
    private TimelineClip clip;
    private StudioClip asset;
    private StudioPropertiesPanel panel;
    private PropertiesHost host;
    private EditorWindow window;
    private VisualElement root;

    private sealed class PropertiesHost : IStudioPropertiesHost
    {
        public CutsceneStudioWindow Window => null;
        public StudioEditSession Session { get; } = new();
        public StudioSequence Sequence => Session.Sequence;
        public StudioClip Selected => Session.Selected;
        public int ActorIndex => Session.ActorIndex;
        public int KeyIndex => Session.KeyIndex;
        public StudioProperty KeyProperty => Session.KeyProperty;
        public StudioClip KeySelectionOwner => Session.KeyOwner;
        public bool KeyframeMode => true;
        internal int ChangeCount { get; private set; }
        public void Changed(bool rebuild = false) { ChangeCount++; }
        public void DeleteClip() { }
        public void DuplicateClip() { }
        public void DeleteSelectedKey()
        {
            StudioKeyframeEditing.Delete(FindSelected(), KeyProperty, KeyIndex);
            Session.ClearKey();
        }
        public TimelineClip FindSelected() => Session.FindSelected();
        public bool NativeTextEditing() => false;
        public double Snap(double value) => Math.Round(value * 10) / 10;
        public void Seek(double time) { }
    }

    [SetUp]
    public void SetUp()
    {
        sequence = ScriptableObject.CreateInstance<StudioSequence>();
        sequence.Timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        sequence.Timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        sequence.Timeline.fixedDuration = 10;
        sequence.EditableActors.Add(new StudioActor { Id = "a", Name = "배우" });
        sequence.EditableActors.Add(new StudioActor { Id = "b", Name = "배우" });
        track = sequence.Timeline.CreateTrack<StudioTrack>();
        clip = track.CreateClip<StudioClip>();
        clip.start = 1;
        clip.duration = 2;
        asset = (StudioClip)clip.asset;
        asset.Kind = StudioKind.Actor;
        asset.ActorId = "a";
        host = new PropertiesHost();
        host.Session.Load(sequence);
        host.Session.Select(asset);
        panel = new StudioPropertiesPanel();
        panel.Initialize(host);
        Undo.IncrementCurrentGroup();
    }

    [TearDown]
    public void TearDown()
    {
        panel.ReleaseToolkitBindings();
        if (window != null)
            window.Close();
        Undo.ClearUndo(asset);
        Undo.ClearUndo(track);
        Undo.ClearUndo(sequence.Timeline);
        Undo.ClearUndo(sequence);
        Object.DestroyImmediate(asset);
        Object.DestroyImmediate(track);
        Object.DestroyImmediate(sequence.Timeline);
        Object.DestroyImmediate(sequence);
    }

    private void OpenPanel()
    {
        window = ScriptableObject.CreateInstance<EditorWindow>();
        window.titleContent = new GUIContent("Properties Test");
        window.position = new Rect(40, 40, 300, 740);
        window.ShowUtility();
        root = panel.CreateGUI();
        root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
        root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(
            "Assets/01.Game/Editor/01.ContentTools/CutsceneStudio/StudioToolkitPanels.uss"));
        window.rootVisualElement.Add(root);
        panel.Sync(true);
    }

    [Test]
    public void UnkeyedEndpointEditsOnlyItsComponentAndSupportsUndoRedo()
    {
        asset.From = new Vector2(10, 20);
        asset.To = new Vector2(30, 40);
        Assert.IsTrue(StudioPropertyEditing.SetEndpoint(clip, StudioProperty.PositionX, false, 55));
        Assert.AreEqual(new Vector2(55, 20), asset.From);
        Assert.AreEqual(new Vector2(30, 40), asset.To);
        Assert.IsFalse(StudioKeyframes.HasOverrides(asset));
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Assert.AreEqual(new Vector2(10, 20), asset.From);
        Undo.PerformRedo();
        Assert.AreEqual(new Vector2(55, 20), asset.From);
    }

    [Test]
    public void KeyedEndpointPreservesInteriorKeysAndOtherChannels()
    {
        StudioKeyframeEditing.Set(clip, StudioProperty.PositionX, -1, 1, 70, false);
        StudioKeyframeEditing.Set(clip, StudioProperty.PositionY, -1, .6f, 30, false);
        string otherChannel = JsonUtility.ToJson(StudioKeyframes.Channel(asset, StudioProperty.PositionY));
        Assert.IsTrue(StudioPropertyEditing.SetEndpoint(clip, StudioProperty.PositionX, true, 90));
        var keys = StudioKeyframes.GetKeys(asset, StudioProperty.PositionX, clip.duration);
        Assert.AreEqual(3, keys.Length);
        Assert.AreEqual(70, keys[1].Value);
        Assert.AreEqual(1, keys[1].Time);
        Assert.AreEqual(90, keys[2].Value);
        Assert.AreEqual(otherChannel, JsonUtility.ToJson(StudioKeyframes.Channel(asset, StudioProperty.PositionY)));
        Undo.PerformUndo();
        Assert.AreEqual(0, StudioPropertyEditing.Endpoint(clip, StudioProperty.PositionX, true));
        Undo.PerformRedo();
        Assert.AreEqual(90, StudioPropertyEditing.Endpoint(clip, StudioProperty.PositionX, true));
    }

    [Test]
    public void LegacyStateKeysArePreservedWhenEditingOneChannel()
    {
        asset.Kind = StudioKind.State;
        asset.Keys = new[] { new StudioPoseKey { Time = 0, Position = new Vector2(10, 20) },
            new StudioPoseKey { Time = 1, Position = new Vector2(30, 40) } };
        StudioPropertyEditing.SetEndpoint(clip, StudioProperty.PositionX, true, 80);
        Assert.AreEqual(2, asset.Keys.Length);
        Assert.AreEqual(new Vector2(30, 40), asset.Keys[1].Position);
        Assert.AreEqual(3, StudioKeyframes.GetKeys(asset, StudioProperty.PositionX, clip.duration).Length);
        Assert.AreEqual(40, StudioKeyframes.Evaluate(asset, StudioProperty.PositionY, 2, clip.duration));
    }

    [Test]
    public void TimingAndEndpointsRejectNonFiniteInputAndClampRuntimeRanges()
    {
        Assert.IsFalse(StudioPropertyEditing.SetTiming(sequence, clip, double.NaN, 2));
        Assert.IsFalse(StudioPropertyEditing.SetTiming(sequence, clip, 0, double.PositiveInfinity));
        Assert.IsFalse(StudioPropertyEditing.SetEndpoint(clip, StudioProperty.Scale, false, float.NaN));
        Assert.AreEqual(1, clip.start);
        Assert.AreEqual(2, clip.duration);
        StudioPropertyEditing.SetTiming(sequence, clip, -4, -2);
        Assert.AreEqual(0, clip.start);
        Assert.AreEqual(StudioTiming.FrameDuration(sequence), clip.duration, 1e-8);
        StudioPropertyEditing.SetEndpoint(clip, StudioProperty.Scale, false, -1);
        StudioPropertyEditing.SetEndpoint(clip, StudioProperty.Opacity, true, 5);
        Assert.AreEqual(.01f, asset.ScaleFrom);
        Assert.AreEqual(1, asset.OpacityTo);
    }

    [Test]
    public void FrameListEditsRetainValidSourceTimingAndClearMismatchedTiming()
    {
        asset.Frames = new Sprite[2];
        asset.FrameTimes = new[] { 0f, .3f };
        asset.AnimationLength = .8f;
        StudioPropertyEditing.ValidateFrameTiming(asset);
        Assert.IsTrue(StudioPropertyEditing.UsesSourceTiming(asset));
        asset.Frames = new Sprite[3];
        StudioPropertyEditing.ValidateFrameTiming(asset);
        Assert.IsFalse(StudioPropertyEditing.UsesSourceTiming(asset));
        Assert.IsNull(asset.FrameTimes);
        Assert.AreEqual(0, asset.AnimationLength);
    }

    [UnityTest]
    public IEnumerator AllNineKindsBindWithoutChangingSavedData()
    {
        OpenPanel();
        foreach (StudioKind kind in Enum.GetValues(typeof(StudioKind)))
        {
            asset.Kind = kind;
            string before = EditorJsonUtility.ToJson(asset);
            int changes = host.ChangeCount;
            panel.Sync(true);
            yield return null;
            yield return null;
            Assert.AreEqual(before, EditorJsonUtility.ToJson(asset), kind.ToString());
            Assert.AreEqual(changes, host.ChangeCount, kind + " changed during binding");
            Assert.AreEqual(clip.start, root.Q<DoubleField>("clip-start").value);
            Assert.AreEqual(clip.duration, root.Q<DoubleField>("clip-duration").value);
            Assert.AreEqual(StudioClipSchema.Definition(kind).Label, root.Q<Label>("properties-clip-type").text);
        }
    }

    [UnityTest]
    public IEnumerator TargetPickerDisambiguatesNamesAndCanUndoTargetChange()
    {
        OpenPanel();
        yield return null;
        var picker = root.Q<PopupField<string>>("actor-picker");
        CollectionAssert.AreEqual(new[] { "배우 (a)", "배우 (b)" }, picker.choices);
        picker.index = 1;
        Assert.AreEqual("b", asset.ActorId);
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        panel.Sync(true);
        Assert.AreEqual("a", asset.ActorId);
        Assert.AreEqual("배우 (a)", root.Q<PopupField<string>>("actor-picker").value);
    }

    [UnityTest]
    public IEnumerator UnknownTargetIsPreservedUntilUserChoosesAnother()
    {
        asset.ActorId = "missing";
        OpenPanel();
        yield return null;
        StringAssert.Contains("missing", root.Q<PopupField<string>>("actor-picker").value);
        Assert.AreEqual("missing", asset.ActorId);
        Assert.AreEqual(0, host.ChangeCount);
    }

    [UnityTest]
    public IEnumerator SelectedKeyValueDoesNotResnapTimeAndExternalEditsRefresh()
    {
        int index = StudioKeyframeEditing.Set(clip, StudioProperty.PositionX, -1, .37f, 15, false);
        host.Session.SelectKey(asset, StudioProperty.PositionX, index);
        OpenPanel();
        yield return null;
        root.Q<FloatField>("key-value").value = 25;
        Assert.AreEqual(.37f, StudioKeyframes.GetKeys(asset, StudioProperty.PositionX, 2)[index].Time);
        Assert.AreEqual(25, StudioKeyframes.GetKeys(asset, StudioProperty.PositionX, 2)[index].Value);
        root.Q<FloatField>("key-time").value = .66f;
        Assert.AreEqual(.7f, StudioKeyframes.GetKeys(asset, StudioProperty.PositionX, 2)[index].Time);
        StudioKeyframeEditing.Set(clip, StudioProperty.PositionX, index, .8f, 35, false);
        panel.Sync(true);
        Assert.AreEqual(.8f, root.Q<FloatField>("key-time").value);
        Assert.AreEqual(35, root.Q<FloatField>("key-value").value);
    }

    [UnityTest]
    public IEnumerator ImportedAnimationDisablesIgnoredFpsAndSharedSpriteKeepsClipFrames()
    {
        asset.Kind = StudioKind.Visual;
        asset.Frames = new Sprite[2];
        asset.FrameTimes = new[] { 0f, .3f };
        asset.AnimationLength = .8f;
        OpenPanel();
        yield return null;
        yield return null;
        Assert.IsFalse(root.Q<PropertyField>("property-FramesPerSecond").enabledSelf);
        var imageField = root.Q<PropertyField>("property-Actors.Array.data[0].Sprite");
        Assert.IsNotNull(imageField.Q<ObjectField>());
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
        try
        {
            imageField.Q<ObjectField>().value = sprite;
            yield return null;
            Assert.AreSame(sprite, sequence.Actors[0].Sprite);
            Assert.AreEqual(2, asset.Frames.Length);
            Assert.IsNull(asset.Frames[0]);
            Assert.IsTrue(StudioPropertyEditing.UsesSourceTiming(asset));
        }
        finally { Object.DestroyImmediate(sprite); }
    }

    [UnityTest]
    public IEnumerator DialogueHeightRequiresResolvedSpeakerAndPreservesValue()
    {
        asset.Kind = StudioKind.Dialogue;
        asset.ActorId = "";
        asset.BubbleOffset = 60;
        OpenPanel();
        yield return null;
        yield return null;
        Assert.IsFalse(root.Q<PropertyField>("property-BubbleOffset").enabledSelf);
        Assert.AreEqual(60, asset.BubbleOffset);

        root.Q<PopupField<string>>("actor-picker").index = 1;
        panel.Sync(true);
        yield return null;
        yield return null;
        Assert.AreEqual("a", asset.ActorId);
        Assert.IsTrue(root.Q<PropertyField>("property-BubbleOffset").enabledSelf);
        Assert.AreEqual(60, asset.BubbleOffset);

        root.Q<PopupField<string>>("actor-picker").index = 0;
        panel.Sync(true);
        Assert.IsFalse(root.Q<PropertyField>("property-BubbleOffset").enabledSelf);
        Assert.AreEqual(60, asset.BubbleOffset);

        asset.ActorId = "missing";
        panel.Sync(true);
        Assert.IsFalse(root.Q<PropertyField>("property-BubbleOffset").enabledSelf);
        Assert.AreEqual(60, asset.BubbleOffset);
    }

    [UnityTest]
    public IEnumerator EffectAndStateTargetDisplayOnlyApplicableControls()
    {
        asset.Kind = StudioKind.Effect;
        asset.Effect = StudioEffect.Shake;
        OpenPanel();
        yield return null;
        Assert.IsNull(root.Q<PropertyField>("property-EffectColor"));
        asset.Effect = StudioEffect.Tint;
        panel.Sync(true);
        Assert.IsNotNull(root.Q<PropertyField>("property-EffectColor"));
        asset.Kind = StudioKind.State;
        panel.Sync(true);
        root.Q<PopupField<string>>("state-target").index = 1;
        panel.Sync(true);
        Assert.IsNull(root.Q<PopupField<string>>("actor-picker"));
        Assert.IsNull(root.Q<TextField>("key-count-Rotation"));
        Assert.IsNotNull(root.Q<TextField>("key-count-Scale"));
        Assert.AreEqual("a", asset.ActorId);
    }
}
