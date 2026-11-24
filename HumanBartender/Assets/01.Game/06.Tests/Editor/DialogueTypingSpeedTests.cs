using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>QA-51: 대사 타이핑이 프레임 속도와 상관없이 설정한 속도(바 60자/초, 거리 55자/초)로 나오고, 바에서만 태그 시간이 1/3인지 검사한다.</summary>
public sealed class DialogueTypingSpeedTests
{
    // 가짜 프레임: 기다릴 때마다 한 프레임이 지나고, 지난 프레임 수를 센다.
    static (DialogueTextPlayer.TypingClock clock, System.Func<int> frames) FakeClock(float frameSeconds)
    {
        int frames = 0;
        var clock = new DialogueTextPlayer.TypingClock(_ => { frames++; return UniTask.CompletedTask; }, () => frameSeconds);
        return (clock, () => frames);
    }

    static float SecondsToType(int characters, float delay, float frameSeconds)
    {
        var (clock, frames) = FakeClock(frameSeconds);
        for (int i = 0; i < characters; i++) clock.WaitAsync(delay, CancellationToken.None).GetAwaiter().GetResult();
        return frames() * frameSeconds;
    }

    [TestCase(60f, 1f / 60f)]
    [TestCase(60f, 1f / 30f)]
    [TestCase(60f, 1f / 120f)]
    [TestCase(55f, 1f / 60f)]
    [TestCase(55f, 1f / 30f)]
    public void TypingKeepsTheConfiguredRateAtAnyFrameRate(float charactersPerSecond, float frameSeconds)
    {
        // 1초 분량을 칠 때 실제로 걸린 시간이 한 프레임 안쪽 오차로 1초여야 한다(예전에는 55자/초가 60FPS에서 약 2초).
        float seconds = SecondsToType((int)charactersPerSecond, 1f / charactersPerSecond, frameSeconds);
        Assert.That(seconds, Is.EqualTo(1f).Within(frameSeconds + 1e-4f));
    }

    [Test]
    public void ShortIntervalsRevealSeveralCharactersInOneFrame()
    {
        var (clock, frames) = FakeClock(1f / 30f);
        for (int i = 0; i < 4; i++) clock.WaitAsync(1f / 120f, CancellationToken.None).GetAwaiter().GetResult();
        Assert.That(frames(), Is.EqualTo(1), "30FPS에서 120자/초면 한 프레임에 네 글자");
    }

    [Test]
    public void BarScalesOnlyTagTimingByAThird()
    {
        // settings/text_tags.csv의 slow(120ms) 정의와 같은 값.
        var tags = UnityEngine.ScriptableObject.CreateInstance<NewTextTagDataSO>();
        try
        {
            tags.textTagData = new System.Collections.Generic.Dictionary<string, NewTextTagData>
            {
                ["slow"] = new NewTextTagData { Kind = "speed_ms", Value = "120" }
            };
            const string line = "가<slow>나</slow><300>다";
            var bar = DialogueTextCompiler.Compile(line, tags, DialoguePresentationSettings.Shared, null, 1f / 60f, 1f / 3f);
            var street = DialogueTextCompiler.Compile(line, tags, DialoguePresentationSettings.Shared, null, 1f / 55f);
            Assert.That(bar.SourceStyles[0].SpeedSeconds, Is.EqualTo(1f / 60f).Within(1e-6f), "기본 간격은 배율과 상관없음");
            Assert.That(bar.SourceStyles[1].SpeedSeconds, Is.EqualTo(.04f).Within(1e-6f), "<slow> 120ms는 바에서 40ms");
            Assert.That(street.SourceStyles[1].SpeedSeconds, Is.EqualTo(.12f).Within(1e-6f), "거리는 그대로");
            Assert.That(bar.SourceWaits.Values.Single(), Is.EqualTo(.1f).Within(1e-6f), "<300> 쉼은 바에서 0.1초");
            Assert.That(street.SourceWaits.Values.Single(), Is.EqualTo(.3f).Within(1e-6f), "거리는 그대로");
        }
        finally { UnityEngine.Object.DestroyImmediate(tags); }
    }

    [Test]
    public void PlaySceneTypesBarDialogueAtSixtyCharactersPerSecond()
    {
        var scene = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Play.unity", OpenSceneMode.Additive);
        try
        {
            var view = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UIDialogueTextView>(true)).Single(v => v.name == "Typer");
            Assert.That(view.TypingDelay, Is.EqualTo(1f / 60f).Within(1e-5f), "웹 바 대사 초당 60자");
            Assert.That(view.TagTimeScale, Is.EqualTo(1f / 3f).Within(1e-5f));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
}
