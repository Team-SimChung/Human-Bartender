using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class BirdAnimationAssetsTests
{
    const string Art = "Assets/01.Game/03.Content/01.ArtResources/01.Characters/02.Outside/05.StreetNPC/";
    const string Animations = "Assets/01.Game/03.Content/07.Animations/01.Characters/StreetNPC/Birds/";
    const string Prefabs = "Assets/01.Game/03.Content/08.Prefabs/04.Shared/02.Birds/";

    [TestCase("Crow", "Crow_idle", "01.Idle/Crow_idle.png")]
    [TestCase("Pigeon", "Pigeon_idle", "01.Idle/Pigeon_idle.png")]
    [TestCase("Pigeon", "Pigeon_fly", "02.Action/pigeon_fly.png")]
    public void ClipsPlayEverySourceFrameOnThePrefabRoot(string actor, string clipName, string source)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations + actor + "/" + clipName + ".anim");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + actor + ".prefab");
        var frames = AssetDatabase.LoadAllAssetsAtPath(Art + source).OfType<Sprite>()
            .OrderBy(sprite => int.Parse(sprite.name[(sprite.name.LastIndexOf('_') + 1)..])).ToArray();
        Assert.That(clip, Is.Not.Null);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(frames, Is.Not.Empty);
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art + source);
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100));
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        Assert.That(clip.isLooping, Is.EqualTo(clipName == "Pigeon_fly"), "Idle은 공통 파츠가 대기·반복을 관리하고 비행 클립은 계속 반복한다.");
        Assert.That(clip.length, Is.EqualTo(frames.Length / clip.frameRate).Within(.001));
        var preview = EditorSceneManager.NewPreviewScene();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
        try
        {
            instance.GetComponent<Animator>().enabled = false;
            var authoredPosition = new Vector3(1, 2, 3);
            instance.transform.localPosition = authoredPosition;
            var renderer = instance.GetComponent<SpriteRenderer>();
            for (int i = 0; i < frames.Length; i++)
            {
                clip.SampleAnimation(instance, (i + .25f) / clip.frameRate);
                Assert.That(renderer.sprite, Is.EqualTo(frames[i]), clipName + ": frame " + i);
                Assert.That(instance.transform.localPosition, Is.EqualTo(authoredPosition), "애니메이션은 배치한 위치를 덮어쓰지 않는다.");
            }
            Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one));
        }
        finally { Object.DestroyImmediate(instance); EditorSceneManager.ClosePreviewScene(preview); }
    }

    [TestCase("Crow")]
    [TestCase("Pigeon")]
    public void BirdPrefabsAreIndependentAndReadyToAnimate(string actor)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + actor + ".prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.name, Is.EqualTo(actor));
        Assert.That(prefab.transform.childCount, Is.Zero);
        Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
        {
            var loop = prefab.GetComponents<MonoBehaviour>().Single() as RandomIdleAnimation;
            Assert.That(loop, Is.Not.Null);
            using var data = new SerializedObject(loop);
            Assert.That(data.FindProperty("animator").objectReferenceValue, Is.EqualTo(prefab.GetComponent<Animator>()));
            Assert.That(data.FindProperty("idleClip").objectReferenceValue,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations + actor + "/" + actor + "_idle.anim")));
            Assert.That(data.FindProperty("minimumStartFrame").intValue, Is.EqualTo(1));
            Assert.That(data.FindProperty("maximumStartFrame").intValue, Is.EqualTo(4));
            Assert.That(data.FindProperty("playOnEnable").boolValue, Is.EqualTo(actor == "Crow"));
        }
        var renderer = prefab.GetComponent<SpriteRenderer>();
        Assert.That(renderer.sprite, Is.Not.Null);
        Assert.That(renderer.sortingLayerName, Is.EqualTo("Character"));
        Assert.That(renderer.sharedMaterial, Is.Not.Null);
        var controller = prefab.GetComponent<Animator>().runtimeAnimatorController as AnimatorController;
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.layers.Single().stateMachine.defaultState.name, Is.EqualTo("Idle"));
        Assert.That(controller.animationClips.All(clip => clip != null), Is.True);
        foreach (var clip in controller.animationClips)
        {
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
            Assert.That(binding.path, Is.Empty);
            Assert.That(binding.type, Is.EqualTo(typeof(SpriteRenderer)));
            Assert.That(binding.propertyName, Is.EqualTo("m_Sprite"));
        }
    }

    [Test]
    public void PigeonCanSwitchBetweenIdleAndFlightWithoutExitTime()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Animations + "Pigeon/Pigeon.controller");
        Assert.That(controller.parameters.Single().name, Is.EqualTo("IsFlying"));
        Assert.That(controller.parameters.Single().type, Is.EqualTo(AnimatorControllerParameterType.Bool));
        Assert.That(controller.parameters.Single().defaultBool, Is.False);
        var states = controller.layers.Single().stateMachine.states.ToDictionary(value => value.state.name, value => value.state);
        Assert.That(states.Keys, Is.EquivalentTo(new[] { "Idle", "Fly" }));
        foreach (string name in new[] { "Idle", "Fly" })
        {
            var transition = states[name].transitions.Single();
            Assert.That(transition.destinationState, Is.SameAs(states[name == "Idle" ? "Fly" : "Idle"]));
            Assert.That(transition.hasExitTime, Is.False);
            Assert.That(transition.duration, Is.Zero);
            Assert.That(transition.conditions.Single().parameter, Is.EqualTo("IsFlying"));
            Assert.That(transition.conditions.Single().mode,
                Is.EqualTo(name == "Idle" ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot));
        }
    }
}
