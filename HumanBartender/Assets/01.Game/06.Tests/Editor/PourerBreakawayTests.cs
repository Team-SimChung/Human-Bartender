using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PourerBreakawayTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    GameObject root;
    PourManager manager;
    PourerBreakaway pourer;
    BottleTiltController bottle;

    static T Ref<T>(Object target, string field) where T : Object =>
        new SerializedObject(target).FindProperty(field).objectReferenceValue as T;

    [SetUp]
    public void SetUp()
    {
        root = PrefabUtility.LoadPrefabContents(PourHudPrefabSetup.PrefabPath);
        manager = root.GetComponentInChildren<PourManager>(true);
        pourer = Ref<PourerBreakaway>(manager, "pourer");
        Assert.That(pourer, Is.Not.Null, "실제 따르기 프리팹에 분리 기능을 저장해야 한다.");
        bottle = Ref<BottleTiltController>(manager, "bottle");
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) PrefabUtility.UnloadPrefabContents(root);
    }

    [Test]
    public void PrefabHasReachableThresholdExistingPourerResourceAndNonBlockingFeedback()
    {
        Assert.That(pourer.transform, Is.EqualTo(root.transform));
        Assert.That(bottle.MaxTiltAngle, Is.GreaterThan(pourer.DetachAngle));
        var attached = Ref<SpriteRenderer>(pourer, "attachedPourer");
        var detached = Ref<SpriteRenderer>(pourer, "detachedPourer");
        Assert.That(attached.transform.parent, Is.EqualTo(bottle.BottleVisual));
        Assert.That(detached.transform.IsChildOf(bottle.BottleVisual), Is.False);
        Assert.That(AssetDatabase.GetAssetPath(attached.sprite), Does.EndWith("/03.pour/pourer.png"));
        Assert.That(detached.sprite, Is.EqualTo(attached.sprite));
        var body = Ref<SpriteRenderer>(pourer, "ingredientBottle");
        var liquid = Ref<SphLiquidRenderer>(manager, "liquidRenderer");
        int liquidOrder = new SerializedObject(liquid).FindProperty("sortingOrder").intValue;
        Assert.That(body.sortingLayerID, Is.Zero);
        Assert.That(body.sortingOrder, Is.GreaterThan(liquidOrder), "병이 액체를 가려야 한다.");
        Assert.That(attached.sortingOrder, Is.GreaterThan(body.sortingOrder));
        Assert.That(detached.sortingOrder, Is.GreaterThan(body.sortingOrder));
        var silhouette = Ref<BottleSilhouette>(manager, "bottleSilhouette");
        var silhouetteRenderer = Ref<MeshFilter>(silhouette, "silhouetteFilter").GetComponent<MeshRenderer>();
        Assert.That(silhouetteRenderer.sortingOrder, Is.GreaterThan(liquidOrder));
        var group = Ref<CanvasGroup>(pourer, "feedbackRoot");
        var text = Ref<TMP_Text>(pourer, "feedbackText");
        Assert.That(group.transform.IsChildOf(manager.GimmickHud.transform), Is.True);
        pourer.ResetState(); // 편집용 미리보기 상태와 관계없이 재생 초기화 때 숨겨져야 한다.
        Assert.That(group.gameObject.activeSelf, Is.False);
        Assert.That(group.blocksRaycasts || group.interactable || text.raycastTarget || text.enableAutoSizing || text.richText, Is.False);
        Assert.That(text.text, Is.EqualTo("펑!"));
        Assert.That(text.font, Is.Not.Null);
        Assert.That(text.transform.localScale, Is.EqualTo(Vector3.one));
    }

    [Test]
    public void OptionalDetachSoundHasSavedSourceOnEffectsBus()
    {
        var source = Ref<AudioSource>(pourer, "detachSource");
        Assert.That(source, Is.Not.Null, "클립만 추가하면 사용할 수 있도록 출력을 프리팹에 저장한다.");
        Assert.That(source.transform, Is.EqualTo(root.transform));
        Assert.That(source.playOnAwake || source.loop, Is.False);
        Assert.That(source.spatialBlend, Is.Zero);
        Assert.That(source.outputAudioMixerGroup, Is.EqualTo(AudioOutputSettings.Shared.GetGroup(AudioBus.SoundEffects)));
        Assert.That(source.clip, Is.Null);
    }

    [TestCase("beer")]
    [TestCase("soda_water")]
    [TestCase("cola")]
    [TestCase("cranberry_juice")]
    [TestCase("milk")]
    [TestCase("orange_juice")]
    [TestCase("red_wine")]
    [TestCase("champagne")]
    [TestCase("")]
    public void ContainersWithoutPourersCannotDetachOrBoostFlow(string id)
    {
        Begin(id);
        Tilt(170f, true);
        Assert.That(pourer.IsAttached || pourer.TryDetach() || pourer.IsDetached, Is.False);
        Assert.That(pourer.FlowMultiplier, Is.EqualTo(1f));
        Assert.That(pourer.StreamWidthScale, Is.EqualTo(1f));
    }

    [Test]
    public void HeldBottleDetachesOnceAtThresholdAndNextIngredientRestoresOriginalFlow()
    {
        var body = Ref<SpriteRenderer>(pourer, "ingredientBottle");
        Vector3 bodyPosition = body.transform.localPosition;
        Begin("gin");
        var cap = Ref<SpriteRenderer>(pourer, "attachedPourer");
        Assert.That(pourer.StreamWidthScale, Is.InRange(.1f, .99f));
        Vector3 mouth = bottle.BottleVisual.TransformPoint(Vector3.up * .4f);
        Vector3 tip = cap.transform.TransformPoint(new Vector3(cap.sprite.bounds.center.x, cap.sprite.bounds.max.y));
        Assert.That(Vector3.Distance(tip, mouth), Is.LessThan(.001f));
        Assert.That(body.transform.localPosition.y, Is.LessThan(bodyPosition.y));
        Tilt(pourer.DetachAngle - .01f, true);
        Assert.That(pourer.TryDetach(), Is.False);
        Tilt(pourer.DetachAngle, false);
        Assert.That(pourer.TryDetach(), Is.False, "놓은 병은 분리되지 않는다.");
        Tilt(pourer.DetachAngle, true);
        Assert.That(pourer.TryDetach(), Is.True);
        Assert.That(pourer.StreamWidthScale, Is.EqualTo(1f));
        Assert.That(body.transform.localPosition, Is.EqualTo(bodyPosition));
        // 웹 비율(42 → 95)의 2배. 푸어러 없이 병째 쏟을 때 너무 느리다는 플레이테스트 피드백(2026-11-20)으로 올렸다.
        Assert.That(pourer.FlowMultiplier, Is.EqualTo(2f * 95f / 42f).Within(.0001f));
        Assert.That(manager.FullFlowRatio, Is.EqualTo(1f), "분리 후에는 HUD 경고 범위가 없어져야 한다.");
        Tilt(0f, true);
        Assert.That(pourer.TryDetach(), Is.False);
        Assert.That(pourer.IsDetached, Is.True);
        Begin("tonic_water");
        Assert.That(pourer.IsAttached, Is.True);
        Assert.That(pourer.StreamWidthScale, Is.LessThan(1f));
        Assert.That(pourer.FlowMultiplier, Is.EqualTo(1f));
        pourer.ResetState();
        Assert.That(pourer.StreamWidthScale, Is.EqualTo(1f));
        Assert.That(body.transform.localPosition, Is.EqualTo(bodyPosition));
    }

    [Test]
    public void DetachedSpriteKeepsItsOwnFlightPoseAndStopsWhenTimeDoesNotAdvance()
    {
        Begin("gin");
        Tilt(pourer.DetachAngle, true);
        pourer.TryDetach();
        var detached = Ref<SpriteRenderer>(pourer, "detachedPourer");
        Vector3 start = detached.transform.position;
        bottle.BottleVisual.Rotate(0f, 0f, 30f);
        Assert.That(detached.transform.position, Is.EqualTo(start));
        pourer.Tick(0f);
        Assert.That(detached.transform.position, Is.EqualTo(start));
        pourer.Tick(.1f);
        Assert.That(detached.transform.position.x, Is.GreaterThan(start.x));
        Assert.That(detached.transform.position.y, Is.GreaterThan(start.y));
        pourer.Tick(3f);
        Assert.That(detached.gameObject.activeSelf, Is.False);
        Assert.That(pourer.IsDetached, Is.True, "연출이 끝나도 푸어러 없는 유량은 유지한다.");
    }

    void Begin(string id)
    {
        // 시간 진행은 별도로 검사한다. EditMode에서는 전역 DOTween 실행기를 만들지 않는다.
        var serialized = new SerializedObject(pourer);
        serialized.FindProperty("feedbackRoot").objectReferenceValue = null;
        serialized.FindProperty("detachClip").objectReferenceValue = null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        pourer.BeginPour(id, Vector3.up * .4f);
    }

    void Tilt(float angle, bool held)
    {
        typeof(BottleTiltController).GetField("currentAngle", Private).SetValue(bottle, angle);
        typeof(BottleTiltController).GetField("dragging", Private).SetValue(bottle, held);
    }
}
