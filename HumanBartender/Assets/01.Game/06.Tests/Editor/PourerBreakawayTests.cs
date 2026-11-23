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
        var body = Ref<SpriteRenderer>(pourer, "ingredientBottle");
        var attached = Ref<SpriteRenderer>(pourer, "attachedPourer");
        var detached = Ref<SpriteRenderer>(pourer, "detachedPourer");
        Assert.That(attached.transform.parent, Is.EqualTo(body.transform.parent));
        Assert.That(attached.transform.IsChildOf(bottle.BottleVisual), Is.True);
        Assert.That(detached.transform.IsChildOf(bottle.BottleVisual), Is.False);
        Assert.That(AssetDatabase.GetAssetPath(attached.sprite), Does.EndWith("/03.pour/pourer.png"));
        Assert.That(detached.sprite, Is.EqualTo(attached.sprite));
        var liquid = Ref<SphLiquidRenderer>(manager, "liquidRenderer");
        int liquidOrder = new SerializedObject(liquid).FindProperty("sortingOrder").intValue;
        Assert.That(body.sortingLayerID, Is.Zero);
        Assert.That(body.sortingOrder, Is.GreaterThan(liquidOrder), "병이 액체를 가려야 한다.");
        Assert.That(attached.sortingOrder, Is.GreaterThan(liquidOrder));
        Assert.That(attached.sortingOrder, Is.LessThan(body.sortingOrder), "삽입한 회색 부분은 병 앞면 뒤에 있어야 한다.");
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
    public void DetachSoundHasSavedClipAndSourceOnEffectsBus()
    {
        var clip = Ref<AudioClip>(pourer, "detachClip");
        Assert.That(clip, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(clip), Does.EndWith("/Crafting/pourer_detach.wav"));
        var source = Ref<AudioSource>(pourer, "detachSource");
        Assert.That(source, Is.Not.Null);
        Assert.That(source.transform, Is.EqualTo(root.transform));
        Assert.That(source.playOnAwake || source.loop, Is.False);
        Assert.That(source.spatialBlend, Is.Zero);
        Assert.That(source.outputAudioMixerGroup, Is.EqualTo(AudioOutputSettings.Shared.GetGroup(AudioBus.SoundEffects)));
        Assert.That(source.clip, Is.Null);
    }

    [Test]
    public void PourerSpritePivotMarksTheBlackCollarAboveTheGrayInsert()
    {
        var sprite = Ref<SpriteRenderer>(pourer, "attachedPourer").sprite;
        Assert.That(sprite.pivot.x, Is.EqualTo(sprite.rect.width * .5f).Within(.001f));
        Assert.That(sprite.pivot.y, Is.EqualTo(13f).Within(.001f), "현재 35픽셀 그림의 회색 부분은 아래쪽 13픽셀 안에 있다.");
        Assert.That(sprite.bounds.min.y, Is.LessThan(0f));
        Assert.That(sprite.bounds.max.y, Is.GreaterThan(0f));
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
    public void ContainersWithoutPourersKeepTheDefaultOutletAndFlow(string id)
    {
        var body = Ref<SpriteRenderer>(pourer, "ingredientBottle");
        body.gameObject.SetActive(true);
        body.transform.localPosition += Vector3.right;
        Begin(id);
        Tilt(170f, true);
        Assert.That(pourer.IsAttached || pourer.TryDetach() || pourer.IsDetached, Is.False);
        Assert.That(pourer.FlowMultiplier, Is.EqualTo(1f));
        Assert.That(pourer.StreamWidthScale, Is.EqualTo(1f));
        float mouthHeight = new SerializedObject(manager).FindProperty("bottleInteriorHalfExtents").vector2Value.y;
        Vector3 defaultMouth = bottle.BottleVisual.TransformPoint(Vector3.up * mouthHeight);
        Assert.That(manager.EmissionMouthWorldPosition, Is.EqualTo(defaultMouth));
    }

    [Test]
    public void HeldBottleDetachesOnceAtThresholdAndNextIngredientRestoresOriginalFlow()
    {
        var body = Ref<SpriteRenderer>(pourer, "ingredientBottle");
        Vector3 bodyPosition = body.transform.localPosition;
        Begin("gin");
        var cap = Ref<SpriteRenderer>(pourer, "attachedPourer");
        Assert.That(pourer.StreamWidthScale, Is.InRange(.1f, .99f));
        var liquid = Ref<SphLiquidRenderer>(manager, "liquidRenderer");
        var updateStreamShape = typeof(PourManager).GetMethod("UpdateStreamShape", Private);
        var streamMinRadius = typeof(SphLiquidRenderer).GetField("streamMinCrossRadius", Private);
        updateStreamShape.Invoke(manager, null);
        float attachedMinRadius = (float)streamMinRadius.GetValue(liquid);
        Vector3 mouth = bottle.BottleVisual.TransformPoint(Vector3.up * .4f);
        Vector3 tip = cap.transform.TransformPoint(new Vector3(cap.sprite.bounds.center.x, cap.sprite.bounds.max.y));
        Vector3 collar = cap.transform.TransformPoint(new Vector3(cap.sprite.bounds.center.x, 0f));
        Assert.That(Vector3.Distance(collar, mouth), Is.LessThan(.001f));
        Assert.That(Vector3.Distance(tip, mouth), Is.GreaterThan(.001f));
        Assert.That(body.transform.localPosition, Is.EqualTo(bodyPosition), "푸어러를 붙일 때도 병은 움직이지 않는다.");
        Vector3 attachedBodyPosition = body.transform.localPosition;
        Tilt(pourer.DetachAngle - .01f, true);
        Assert.That(pourer.TryDetach(), Is.False);
        Tilt(pourer.DetachAngle, false);
        Assert.That(pourer.TryDetach(), Is.False, "놓은 병은 분리되지 않는다.");
        Tilt(pourer.DetachAngle, true);
        Assert.That(pourer.TryDetach(), Is.True);
        Assert.That(pourer.StreamWidthScale, Is.EqualTo(1f));
        updateStreamShape.Invoke(manager, null);
        Assert.That((float)streamMinRadius.GetValue(liquid), Is.GreaterThan(attachedMinRadius),
            "최소 반지름이 푸어러 부착 중 두께 감소를 가리면 안 된다.");
        Assert.That(body.transform.localPosition, Is.EqualTo(attachedBodyPosition), "분리 때 병을 움직이지 않는다.");
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

    [TestCase("gin")]
    [TestCase("amaretto")]
    [TestCase("vodka")]
    [TestCase("whiskey")]
    [TestCase("wild_dog")]
    public void MouthPivotAndWorldSizeSurviveTiltDetachmentAndReset(string id)
    {
        using (var data = new SerializedObject(manager))
        {
            data.FindProperty("useTestBottleId").boolValue = true;
            data.FindProperty("testBottleId").stringValue = id;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        Assert.That(typeof(PourManager).GetMethod("ApplyIngredientBottle", Private).Invoke(manager, null), Is.EqualTo(true));
        var body = Ref<SpriteRenderer>(pourer, "ingredientBottle");
        Vector3 restPosition = body.transform.localPosition;
        Vector3 localScale = body.transform.localScale;
        const float originalBeerScale = 2.88f / 1.57f;
        Vector2 bodySize = body.sprite.bounds.size * originalBeerScale;
        AssertWorldSize(body, bodySize);
        Vector3 mouth = bottle.BottleVisual.TransformPoint(Vector3.up * .4f);
        Assert.That(Vector3.Distance(body.transform.position, mouth), Is.LessThan(.001f), "병 피벗은 입구여야 한다.");
        Begin(id);
        AssertWorldSize(body, bodySize);
        Assert.That(body.transform.localPosition, Is.EqualTo(restPosition));
        Vector3 attachedBodyPosition = body.transform.localPosition;
        bottle.BottleVisual.localRotation = Quaternion.Euler(0f, 0f, -pourer.DetachAngle);
        Tilt(pourer.DetachAngle, true);
        var attached = Ref<SpriteRenderer>(pourer, "attachedPourer");
        Vector2 capSize = attached.sprite.bounds.size * originalBeerScale;
        AssertWorldSize(attached, capSize);
        Vector3 capPosition = attached.transform.position;
        Vector3 bodyMouthBeforeDetach = body.transform.position;
        Vector3 collar = attached.transform.TransformPoint(new Vector3(attached.sprite.bounds.center.x, 0f));
        Assert.That(Vector3.Distance(collar, bodyMouthBeforeDetach), Is.LessThan(.001f), "검은 띠 기준점은 병 입구에 있어야 한다.");
        Vector3 grayBottom = attached.transform.TransformPoint(new Vector3(attached.sprite.bounds.center.x, attached.sprite.bounds.min.y));
        Assert.That(body.transform.InverseTransformPoint(grayBottom).y, Is.LessThan(body.sprite.bounds.max.y), "회색 부분은 입구 아래로 들어가야 한다.");
        using var outletSettings = new SerializedObject(pourer);
        outletSettings.FindProperty("outletInsetX").floatValue = .03f;
        outletSettings.ApplyModifiedPropertiesWithoutUndo();
        var offsets = new Vector3(-outletSettings.FindProperty("outletInsetX").floatValue,
            outletSettings.FindProperty("higtY").floatValue);
        Vector3 attachedOutlet = attached.transform.TransformPoint(
            new Vector3(attached.sprite.bounds.center.x, attached.sprite.bounds.max.y) + offsets);
        Vector3 bottleOutlet = body.transform.TransformPoint(new Vector3(body.sprite.bounds.center.x, body.sprite.bounds.max.y));
        Assert.That(Vector3.Distance(manager.EmissionMouthWorldPosition, attachedOutlet), Is.LessThan(.001f));
        Assert.That(Vector3.Distance(attachedOutlet, bodyMouthBeforeDetach), Is.GreaterThan(.001f));
        Assert.That(pourer.TryDetach(), Is.True);
        var detached = Ref<SpriteRenderer>(pourer, "detachedPourer");
        AssertWorldSize(detached, capSize);
        Assert.That(Vector3.Distance(detached.transform.position, capPosition), Is.LessThan(.001f));
        AssertWorldSize(body, bodySize);
        Assert.That(body.transform.localScale, Is.EqualTo(localScale));
        Assert.That(body.transform.localPosition, Is.EqualTo(attachedBodyPosition));
        Assert.That(Vector3.Distance(body.transform.position, bodyMouthBeforeDetach), Is.LessThan(.001f));
        Assert.That(Vector3.Distance(manager.EmissionMouthWorldPosition, bottleOutlet), Is.LessThan(.001f));
        pourer.Tick(.1f);
        AssertWorldSize(body, bodySize);
        AssertWorldSize(detached, capSize);
        Assert.That(Vector3.Distance(manager.EmissionMouthWorldPosition, bottleOutlet), Is.LessThan(.001f), "날아가는 푸어러를 따라가지 않는다.");
        pourer.ResetState();
        AssertWorldSize(body, bodySize);
        Assert.That(body.transform.localScale, Is.EqualTo(localScale));
        Assert.That(body.transform.localPosition, Is.EqualTo(restPosition));
    }

    static Vector2 WorldSpriteSize(SpriteRenderer renderer) => new Vector2(
        renderer.transform.TransformVector(Vector3.right * renderer.sprite.bounds.size.x).magnitude,
        renderer.transform.TransformVector(Vector3.up * renderer.sprite.bounds.size.y).magnitude);

    static void AssertWorldSize(SpriteRenderer renderer, Vector2 expected)
    {
        Vector2 actual = WorldSpriteSize(renderer);
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(.0001f), "가로 그림 크기가 바뀌면 안 된다.");
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(.0001f), "세로 그림 크기가 바뀌면 안 된다.");
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
