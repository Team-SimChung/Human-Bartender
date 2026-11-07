using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WebNpcAnimationTests
{
    [Test]
    public void AuthoredHumanIdlesUseTheWebFrameCountsAndFacingConvention()
    {
        WithScenes((home, outside) =>
        {
            int checkedActors = 0;
            foreach (var scene in new[] { home, outside })
                foreach (var actor in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NpcActor>(true)))
                {
                    var target = actor.GetComponentInParent<WorldInteractionTarget>();
                    if (target == null || target.Content != null || actor.name.EndsWith("· boobi", StringComparison.Ordinal)) continue;
                    string role = actor.name.Split('·').Last().Trim();
                    int frames = role == "samho" || role == "gangmate" ? 8 : role == "dealer" ? 6 : role == "shiba" ? 10 : 9;
                    float duration = role == "samho" ? 8f / 12 : role == "shiba" ? 10f / 12 : .75f;
                    var clip = Clip(actor.GetComponent<Animator>(), "Idle");
                    var walking = ((AnimatorController)actor.GetComponent<Animator>().runtimeAnimatorController)
                        .layers[0].stateMachine.states.FirstOrDefault(value => value.state.name == "Walk").state;
                    if (walking != null) Assert.That(walking.motion, Is.SameAs(clip), "Walking must keep the web frame loop.");
                    Assert.That(Sprites(clip).Distinct().Count(), Is.EqualTo(frames), target.StorySceneId + " / " + role);
                    Assert.That(clip.length, Is.EqualTo(duration).Within(.001f), target.StorySceneId + " / " + role);
                    Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.True);
                    using var data = new SerializedObject(actor);
                    Assert.That(data.FindProperty("artworkFacesRight").boolValue, Is.False, actor.name);
                    if (role == "dealer") Assert.That(AssetDatabase.GetAssetPath(Sprites(clip)[0]), Does.EndWith("npc_m1_idle.png"));
                    checkedActors++;
                }
            Assert.That(checkedActors, Is.GreaterThanOrEqualTo(17));
        });
    }

    [Test]
    public void CatReactionsAndBoxUseFiniteSpriteAnimationsWithoutRootScalePulses()
    {
        WithScenes((home, outside) =>
        {
            foreach (var scene in new[] { home, outside })
            {
                var cat = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NpcActor>(true))
                    .Single(actor => actor.name.EndsWith("· boobi", StringComparison.Ordinal));
                foreach (var spec in new[] { ("Feed", 1.5f), ("Pet", 1.6f), ("Meow", 1f), ("RevealEyes", .55f) })
                {
                    var clip = Clip(cat.GetComponent<Animator>(), spec.Item1);
                    Assert.That(clip.length, Is.EqualTo(spec.Item2).Within(.001f));
                    Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.False);
                    Assert.That(AnimationUtility.GetCurveBindings(clip), Is.Empty, "Reaction geometry belongs to the web sprite frames.");
                    Assert.That(Sprites(clip).All(sprite => AssetDatabase.GetAssetPath(sprite).EndsWith("WebCatFrames.png", StringComparison.Ordinal)), Is.True);
                }
            }
            var box = outside.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Animator>(true))
                .Single(animator => animator.name == "Box animation");
            Assert.That(Clip(box, "Shake").length, Is.EqualTo(.9f).Within(.001f));
            Assert.That(box.GetComponent<NpcActor>(), Is.Null);
            Assert.That(box.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("BoxIdle_00"));
            using var interaction = new SerializedObject(box.GetComponentInParent<NpcInteraction>());
            var cue = interaction.FindProperty("proximityAnimation");
            Assert.That(cue.FindPropertyRelative("animator").objectReferenceValue, Is.EqualTo(box));
            Assert.That(cue.FindPropertyRelative("stateName").stringValue, Is.EqualTo("Shake"));
            Assert.That(cue.FindPropertyRelative("returnState").stringValue, Is.EqualTo("Idle"));
            Assert.That(cue.FindPropertyRelative("duration").floatValue, Is.EqualTo(.9f));
            Assert.That(cue.FindPropertyRelative("radius").floatValue, Is.EqualTo(.85f));
            Assert.That(cue.FindPropertyRelative("when").stringValue, Is.EqualTo("!flag.boobi_box_shaken && !flag.boobi_revealed"));
            Assert.That(cue.FindPropertyRelative("completionFlag").stringValue, Is.EqualTo("boobi_box_shaken"));
        });
    }

    [Test]
    public void LegacyCatStateNamesAliasWebClipsWithoutAnyTransformAnimation()
    {
        WithScenes((home, outside) =>
        {
            foreach (var scene in new[] { home, outside })
            {
                var actor = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NpcActor>(true))
                    .Single(value => value.name.EndsWith("· boobi", StringComparison.Ordinal));
                var animator = actor.GetComponent<Animator>();
                foreach (var alias in new[] { ("PulseFeed", "Feed"), ("PulsePet", "Pet"), ("PulseReveal", "RevealEyes") })
                {
                    Assert.That(Clip(animator, alias.Item1), Is.SameAs(Clip(animator, alias.Item2)), alias.Item1);
                    Assert.That(AnimationUtility.GetAnimationClipSettings(Clip(animator, alias.Item1)).loopTime, Is.False);
                }
                foreach (var clip in animator.runtimeAnimatorController.animationClips.Distinct())
                    Assert.That(AnimationUtility.GetCurveBindings(clip).Where(binding => binding.type == typeof(Transform)),
                        Is.Empty, clip.name + " must leave Inspector scale and speech-anchor pose untouched.");
            }
        });
    }


    [Test]
    public void TerraceKeepsEightOriginalFramesAtSixFramesPerSecond()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/01.Game/03.Content/07.Animations/01.Characters/01.Luna/03.terrace/Terrace_ani.anim");
        Assert.That(clip, Is.Not.Null);
        Assert.That(clip.frameRate, Is.EqualTo(6));
        Assert.That(Sprites(clip).Distinct().Count(), Is.EqualTo(8));
        Assert.That(clip.length, Is.EqualTo(8f / 6).Within(.001f));
    }

    private static AnimationClip Clip(Animator animator, string state) =>
        (AnimationClip)((AnimatorController)animator.runtimeAnimatorController).layers[0].stateMachine.states
            .Single(value => value.state.name == state).state.motion;

    private static Sprite[] Sprites(AnimationClip clip) => AnimationUtility.GetObjectReferenceCurveBindings(clip)
        .Where(binding => binding.propertyName == "m_Sprite")
        .SelectMany(binding => AnimationUtility.GetObjectReferenceCurve(clip, binding)).Select(key => (Sprite)key.value).ToArray();

    private static void WithScenes(Action<Scene, Scene> action)
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var home = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Home.unity", OpenSceneMode.Single);
            var outside = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/OutSide.unity", OpenSceneMode.Additive);
            action(home, outside);
        }
        finally
        {
            if (setup.Any(value => value.isLoaded && value.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
