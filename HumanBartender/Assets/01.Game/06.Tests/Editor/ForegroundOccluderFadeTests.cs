using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>QA-54: 루나가 앞 그림(간판·기둥·파이프) 뒤로 지나가면 그 조각만 웹처럼 흐려지는지 검사한다.</summary>
public sealed class ForegroundOccluderFadeTests
{
    static readonly Rect[] Square = { new Rect(0, 0, 1, 1) };

    [Test]
    public void FadeStartsWhenTheBodyTouchesAndPeaksAtAQuarterCovered()
    {
        var body = new Rect(1, 0, 1, 1);
        Assert.That(ForegroundOccluderFade.CoveredFraction(body, Square, .25f), Is.Zero, "가장자리만 맞닿으면 0");
        body.x = .99f;
        Assert.That(ForegroundOccluderFade.CoveredFraction(body, Square, .25f), Is.GreaterThan(0f), "조금만 겹쳐도 흐려지기 시작");
        body.x = .75f;
        Assert.That(ForegroundOccluderFade.CoveredFraction(body, Square, .25f), Is.EqualTo(1f).Within(1e-4f), "몸의 25%가 가려지면 최대");
        body.x = .9f;
        Assert.That(ForegroundOccluderFade.CoveredFraction(body, Square, .25f), Is.EqualTo(.4f).Within(1e-4f));
    }

    [Test]
    public void FadeFollowsNinetyPercentInEightyMillisecondsAndSettles()
    {
        float value = 0f;
        for (int i = 0; i < 8; i++) value = ForegroundOccluderFade.Approach(value, 1f, .01f, .08f, .005f);
        Assert.That(value, Is.EqualTo(.9f).Within(.001f), "웹: 80ms에 90%");
        for (int i = 0; i < 30; i++) value = ForegroundOccluderFade.Approach(value, 1f, .01f, .08f, .005f);
        Assert.That(value, Is.EqualTo(1f), "남은 차이가 0.005보다 작으면 정확히 멈춘다");
        Assert.That(ForegroundOccluderFade.Approach(.3f, 1f, 0f, .08f, .005f), Is.EqualTo(.3f), "시간이 멈추면 그대로");
    }

    [Test]
    public void OutsideSceneSplitsTheForegroundIntoThreeFadingParts()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var scene = EditorSceneManager.OpenScene(ForegroundOccluderFadeSetup.ScenePath, OpenSceneMode.Additive);
        try
        {
            var fade = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ForegroundOccluderFade>(true)).Single();
            Assert.That(fade.name, Is.EqualTo(ForegroundOccluderFadeSetup.ObjectName));
            Assert.That(fade.GetComponent<SpriteRenderer>(), Is.Null, "원본 한 장 대신 조각 세 개로 그린다.");
            Assert.That(fade.Target, Is.Not.Null);
            Assert.That(fade.Target.name, Is.EqualTo("Luna"));
            Assert.That(fade.PartCount, Is.EqualTo(3));
            var renderers = fade.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.That(renderers.Select(r => r.name), Is.EqualTo(new[] { "SignSprite", "ColumnSprite", "PipeSprite" }));
            foreach (var r in renderers)
            {
                Assert.That(r.sortingLayerName, Is.EqualTo("ForeFore"), r.name + "는 루나(Character)보다 앞에 그린다.");
                Assert.That(r.sprite, Is.Not.Null, r.name);
                Assert.That(r.color.a, Is.EqualTo(1f));
                Assert.That(r.transform.localScale, Is.EqualTo(Vector3.one));
            }

            // 루나를 기둥 조각 뒤로 옮기면 기둥만 흐려진다.
            var luna = fade.Target.transform;
            var column = renderers[1];
            luna.position = new Vector3(column.bounds.center.x, column.bounds.center.y, luna.position.z);
            for (int i = 0; i < 30; i++) fade.Step(.02f);
            Assert.That(column.color.a, Is.EqualTo(.24f).Within(.01f), "가장 옅을 때 24%");
            Assert.That(renderers[0].color.a, Is.EqualTo(1f), "멀리 있는 간판은 그대로");

            // 간판 다리 사이 빈 공간(원본 x 200px, 바닥에서 10px)에 몸이 있으면 흐려지지 않는다.
            var sign = renderers[0];
            var gap = sign.transform.TransformPoint(new Vector3(2f, .1f));
            luna.position = new Vector3(gap.x, gap.y, luna.position.z);
            var body = new SerializedBody(fade);
            body.Set(new Rect(-.1f, -.07f, .2f, .14f));
            for (int i = 0; i < 30; i++) fade.Step(.02f);
            Assert.That(sign.color.a, Is.EqualTo(1f), "다리 사이 빈 공간은 겹침으로 치지 않는다.");
            Assert.That(column.color.a, Is.EqualTo(1f), "기둥에서 벗어나면 다시 불투명");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    sealed class SerializedBody
    {
        readonly ForegroundOccluderFade fade;
        public SerializedBody(ForegroundOccluderFade fade) => this.fade = fade;
        public void Set(Rect rect)
        {
            using var data = new UnityEditor.SerializedObject(fade);
            data.FindProperty("body").rectValue = rect;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
