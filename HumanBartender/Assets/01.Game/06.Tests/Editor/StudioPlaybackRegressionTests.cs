using HumanBartender.CutsceneStudio;
using HumanBartender.CutsceneStudio.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

// 미리보기와 실제 재생이 공유하는 프레임 샘플링과 곡선 평가의 회귀를 검증함.
public sealed class StudioPlaybackRegressionTests
{
    private TimelineAsset timeline;
    private StudioTrack track;
    private TimelineClip clip;
    private StudioClip asset;
    private Sprite[] sprites;

    [SetUp]
    public void SetUp()
    {
        timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        track = timeline.CreateTrack<StudioTrack>();
        clip = track.CreateClip<StudioClip>();
        clip.duration = 2;
        asset = (StudioClip)clip.asset;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(asset);
        Object.DestroyImmediate(track);
        Object.DestroyImmediate(timeline);
        if (sprites != null)
            foreach (var sprite in sprites)
                Object.DestroyImmediate(sprite);
        sprites = null;
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void AnimationFramesStayAtClipEndForBothTimingModes(bool sourceTiming, bool loop)
    {
        sprites = new Sprite[4];
        for (int i = 0; i < sprites.Length; i++)
            sprites[i] = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
        asset.Frames = sprites;
        asset.FramesPerSecond = 5;
        asset.Loop = loop;
        if (sourceTiming)
        {
            asset.FrameTimes = new[] { 0f, .2f, .4f, .6f };
            asset.AnimationLength = .8f;
        }
        Assert.AreSame(sprites[0], StudioSpriteAnimation.Sample(asset, -.1, .35));
        Assert.AreSame(sprites[1], StudioSpriteAnimation.Sample(asset, .25, .35));
        Assert.AreSame(sprites[1], StudioSpriteAnimation.Sample(asset, .35, .35));
        Assert.AreSame(sprites[1], StudioSpriteAnimation.Sample(asset, .81, .35));
        Assert.AreSame(sprites[1], StudioSpriteAnimation.Sample(asset, 5, .35));
    }

    [TestCase(StudioEffect.FadeIn, .75f)]
    [TestCase(StudioEffect.FadeOut, .25f)]
    [TestCase(StudioEffect.Flash, .75f)]
    [TestCase(StudioEffect.Shake, .75f)]
    [TestCase(StudioEffect.Tint, 1f)]
    public void EffectCurveMatchesBeforeAndAfterCreatingPropertyKeys(StudioEffect effect, float expectedOpacity)
    {
        asset.Kind = StudioKind.Effect;
        asset.Effect = effect;
        asset.Strength = .4f;
        asset.Curve = AnimationCurve.Linear(0, 0, 1, .5f);
        Assert.AreEqual(expectedOpacity, StudioKeyframes.Evaluate(asset, StudioProperty.Opacity, 1, 2), .0001f);
        Assert.AreEqual(.4f, StudioKeyframes.Evaluate(asset, StudioProperty.Strength, 1, 2), .0001f);

        var keys = StudioKeyframes.GetKeys(asset, StudioProperty.Opacity, 2);
        StudioKeyframeEditing.Set(clip, StudioProperty.Opacity, -1, keys[0].Time, keys[0].Value, false);
        Assert.AreEqual(expectedOpacity, StudioKeyframes.Evaluate(asset, StudioProperty.Opacity, 1, 2), .0001f);
    }

    [Test]
    public void NullEffectCurvePreservesLinearProgress()
    {
        asset.Kind = StudioKind.Effect;
        asset.Effect = StudioEffect.FadeOut;
        asset.Curve = null;
        Assert.AreEqual(.5f, StudioKeyframes.Evaluate(asset, StudioProperty.Opacity, 1, 2), .0001f);
    }
}
