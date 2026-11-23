using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class BubiStoryAnimationTests
{
    const string Clips = "Assets/01.Game/03.Content/07.Animations/01.Characters/04.Bubi/01.Clips/06.StoryActor/";

    [Test]
    public void OutsideBoxReturnsToItsProgressStateAfterInterruptionAndReload()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/OutSide.unity");
        var state = GameStateManager.Instance;
        int previousDay = state.CurrentDay;
        var previousFlow = state.GameFlow;
        var playerData = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var scenario = HomeInteractionTestScenarios.Find(WorldInteractionTestPreparationCatalog.Prefix + "cat-reveal");
            scenario.Apply(playerData);
            state.CurrentDay = scenario.Day - 1;
            state.GameFlow = scenario.Flow;
            var authored = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true))
                .Single(value => value.StorySceneId == "neighborhood-reveal");
            var root = Object.Instantiate(authored.gameObject);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            root.SetActive(true);
            var interaction = root.GetComponent<NpcInteraction>();
            using var data = new SerializedObject(interaction);
            var actor = (NpcActor)data.FindProperty("actor").objectReferenceValue;
            var animator = actor.GetComponent<Animator>();
            var box = root.GetComponentsInChildren<NpcActor>(true).Single(value => value.name == "Sprite_after");
            var boxSprite = box.GetComponent<SpriteRenderer>();
            var initialPosition = actor.transform.position;
            var boxPosition = box.transform.position;
            var revealOffset = data.FindProperty("afterCommitPositionOffset").vector2Value;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var conditions = new ConditionUtil(state, playerData, playerData);
            var story = new NewSceneData { Id = "neighborhood-reveal", RuntimeScene = "OutSide", Route = "both" };
            interaction.Initialize(conditions, null, story, null, null);
            Assert.That(interaction.CanInteract, Is.False);
            Assert.That(actor.IsVisible, Is.False);
            Assert.That(boxSprite.enabled, Is.True, "부비가 등장하기 전에도 빈 박스가 있다.");

            state.CurrentDay = scenario.Day;
            interaction.EndInteraction();
            Assert.That(interaction.CanInteract, Is.True, "연출 중에 표시할 추가 배우도 활성 오브젝트여야 한다.");
            Assert.That(actor.IsVisible, Is.True);
            Assert.That(boxSprite.enabled, Is.False, "상호작용이 열리면 빈 박스를 부비가 들어 있는 박스로 교체한다.");
            Assert.That(boxSprite.sharedMaterial.HasProperty("_OutlineIntensity"), Is.False);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("BoxShake"), Is.True);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).loop, Is.False);

            // 점프 중 취소되어 Idle로 복귀했어도 발견 완료 전에는 흔들리는 상자로 돌아간다.
            actor.PlayIdleState("Idle");
            interaction.EndInteraction();
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("BoxShake"), Is.True);
            Assert.That(actor.transform.position, Is.EqualTo(initialPosition));
            Assert.That(boxSprite.enabled, Is.False);
            Assert.That(playerData.CheckFlag("boobi_revealed"), Is.False);

            playerData.AddFlag("boobi_revealed", true);
            interaction.EndInteraction();
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
            Assert.That(actor.transform.position, Is.EqualTo(initialPosition + (Vector3)revealOffset));
            Assert.That(boxSprite.enabled, Is.True);
            Assert.That(box.transform.position, Is.EqualTo(boxPosition));
            root.SetActive(false);
            root.SetActive(true);
            interaction.Initialize(conditions, null, story, null, null);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True,
                "발견 완료 기록으로 다시 등장할 때 상자 상태로 되돌아가지 않는다.");
            Assert.That(actor.transform.position, Is.EqualTo(initialPosition + (Vector3)revealOffset));
            Assert.That(boxSprite.enabled, Is.True);

            playerData.AddFlag("boobi_adopted", true);
            interaction.EndInteraction();
            Assert.That(interaction.CanInteract, Is.False);
            Assert.That(actor.IsVisible, Is.False);
            Assert.That(boxSprite.enabled, Is.True);
        }
        finally
        {
            state.CurrentDay = previousDay;
            state.GameFlow = previousFlow;
            Object.DestroyImmediate(playerData);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [TestCase("Home")]
    [TestCase("OutSide")]
    public void StoryActorPlaysEveryNewFrameOnItsAuthoredChildWithoutMovingIt(string sceneName)
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/" + sceneName + ".unity");
        try
        {
            var authored = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NpcActor>(true))
                .Single(value => value.name == "Story actor · boobi");
            var actor = Object.Instantiate(authored);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor.gameObject, scene);
            actor.gameObject.SetActive(true);
            var animator = actor.GetComponent<Animator>();
            using var data = new SerializedObject(actor);
            var renderer = (SpriteRenderer)data.FindProperty("spriteRenderer").objectReferenceValue;
            Assert.That(actor.GetComponentsInChildren<Animator>(true).Where(value => value.isActiveAndEnabled).ToArray(),
                Has.Length.EqualTo(1), "비활성 예비 오브젝트를 제외하고 활성 Animator 하나가 그림을 제어한다.");
            Assert.That(data.FindProperty("animator").objectReferenceValue, Is.SameAs(animator));
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            var states = controller.layers.Single().stateMachine.states.ToDictionary(value => value.state.name, value => value.state);
            var rootPosition = actor.transform.localPosition;
            var rootScale = actor.transform.localScale;
            var spritePosition = renderer.transform.localPosition;
            var spriteScale = renderer.transform.localScale;
            foreach (var spec in new[] { ("Walk", "Bubi_Walk", 6, true), ("BoxIdle", "Bubi_Box_Idle", 8, true),
                ("Eyes", "Bubi_Box_Eyes", 22, false), ("Jump", "Bubi_Box_Jump", 15, false),
                ("Meow", "Bubi_Box_Meow", 8, false), ("Shake", "Bubi_Box_Shake", 8, false),
                ("Sleep", "Bubi_Sleep", 12, true),
                ("Idle", sceneName == "Home" ? "Bubi_Sleep" : "Bubi_Box_Idle", sceneName == "Home" ? 12 : 8, true) })
            {
                var clip = (AnimationClip)states[spec.Item1].motion;
                Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(Clips + spec.Item2 + "_Story.anim"));
                Assert.That(clip.isLooping, Is.EqualTo(spec.Item4));
                Assert.That(AnimationUtility.GetCurveBindings(clip), Is.Empty, "연출은 배치·크기를 변경하지 않는다.");
                var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                Assert.That(binding.path, Is.EqualTo(AnimationUtility.CalculateTransformPath(renderer.transform, actor.transform)));
                Assert.That(binding.type, Is.EqualTo(typeof(SpriteRenderer)));
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                Assert.That(keys, Has.Length.EqualTo(spec.Item3));
                Assert.That(clip.length, Is.EqualTo(keys.Length / clip.frameRate).Within(.001f));
                for (int i = 0; i < keys.Length; i++)
                {
                    Assert.That(keys[i].value, Is.Not.Null, spec.Item1 + " frame " + i);
                    clip.SampleAnimation(actor.gameObject, (i + .25f) / clip.frameRate);
                    Assert.That(renderer.sprite, Is.SameAs(keys[i].value), spec.Item1 + " frame " + i);
                    Assert.That(actor.transform.localPosition, Is.EqualTo(rootPosition));
                    Assert.That(actor.transform.localScale, Is.EqualTo(rootScale));
                    Assert.That(renderer.transform.localPosition, Is.EqualTo(spritePosition));
                    Assert.That(renderer.transform.localScale, Is.EqualTo(spriteScale));
                }
            }
            foreach (string state in new[] { "Feed", "Pet", "RevealEyes", "PulseFeed", "PulsePet", "PulseReveal" })
                Assert.That(states[state].motion, Is.Not.Null, "기존 스토리 연출 유지: " + state);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            foreach (string state in new[] { "Idle", "Walk", "Sleep", "BoxIdle", "Eyes", "Jump", "Meow", "Shake" })
            {
                animator.Play("Base Layer." + state, 0, 0);
                animator.Update(0);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(state), Is.True);
                var clip = (AnimationClip)states[state].motion;
                var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                Assert.That(renderer.sprite, Is.SameAs(AnimationUtility.GetObjectReferenceCurve(clip, binding)[0].value));
            }
            animator.Play("Idle", 0, 0);
            animator.Update(0);
            animator.SetFloat("Speed", 1);
            animator.Update(.05f);
            animator.Update(.05f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"), Is.True);
            animator.SetFloat("Speed", 0);
            animator.Update(.05f);
            animator.Update(.05f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
