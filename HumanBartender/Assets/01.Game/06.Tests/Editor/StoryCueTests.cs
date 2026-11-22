using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 바 대본 effect 행의 짧은 연출(웹 story_cue). 2일차 루나가 나가는 행은 문소리 뒤 0.9초 쉬고 효과를 적용한다
/// (엔진 수정 사항 38, 웹 story-flow.js).
/// </summary>
public sealed class StoryCueTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string ProjectScopePrefab = "Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab";

    [Test]
    public void EveryBarEffectCueIsOneThePresenterPlays()
    {
        string steps = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "csv", "story", "steps.csv"));
        var cues = steps.Split('\n')
            .Where(line => line.Contains("script/bar"))
            .Select(line => line.Split(','))
            .Where(cols => cols.Length > 4 && cols[2] == "effect" && cols[4] != "" && cols[4] != "\\N")
            .Select(cols => cols[4]).ToArray();
        Assert.That(cues, Is.Not.Empty, "2일차 루나 퇴장 행(arg=door)이 있어야 합니다.");
        Assert.That(cues.Distinct(), Is.EqualTo(new[] { BarStoryPresenter.DoorCue }), "바 화면이 모르는 대본 연출이 생겼습니다.");
    }

    [Test]
    public void DoorCuePlaysTheRegisteredDoorSoundAndUnknownCuesAreRejected()
    {
        var go = new GameObject("Bar Story Presenter Cue Test");
        try
        {
            var presenter = go.AddComponent<BarStoryPresenter>();
            var sound = new SoundSpy();
            typeof(BarStoryPresenter).GetField("sound", Private).SetValue(presenter, sound);
            string key = (string)typeof(BarStoryPresenter).GetField("doorSoundKey", Private).GetValue(presenter);
            Assert.That((float)typeof(BarStoryPresenter).GetField("doorHoldSeconds", Private).GetValue(presenter), Is.EqualTo(.9f).Within(.001f));
            typeof(BarStoryPresenter).GetField("doorHoldSeconds", Private).SetValue(presenter, 0f);

            Assert.That(presenter.PlayCueAsync(BarStoryPresenter.DoorCue, CancellationToken.None).GetAwaiter().GetResult(), Is.True);
            Assert.That(sound.Played, Is.EqualTo(new[] { key }));
            Assert.That(presenter.PlayCueAsync("unknown", CancellationToken.None).GetAwaiter().GetResult(), Is.False);
            Assert.That(sound.Played.Count, Is.EqualTo(1));

            Assert.That(File.ReadAllText(ProjectScopePrefab).Contains("name: " + key), Is.True,
                key + "가 ProjectLifeScope의 SoundManager 목록에 등록되어 있어야 합니다.");
        }
        finally { Object.DestroyImmediate(go); }
    }

    sealed class SoundSpy : ISoundEffectPlayer
    {
        public readonly List<string> Played = new();
        public void PlaySE(string name) => Played.Add(name);
    }
}
