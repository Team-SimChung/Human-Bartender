using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>0일차 출근길은 맑고 퇴근길에 처음 나오면 비가 내리는지, 거리 비 파티클이 빗소리와 같은 규칙을 따르는지 검사한다.</summary>
public sealed class RainScheduleTests
{
    [TestCase(0, EGameFlow.CommuteIn, false)]
    [TestCase(0, EGameFlow.CommuteOut, true)]
    [TestCase(1, EGameFlow.CommuteIn, true)]
    [TestCase(1, EGameFlow.CommuteOut, true)]
    [TestCase(2, EGameFlow.CommuteIn, true)]
    [TestCase(2, EGameFlow.CommuteOut, false)]
    [TestCase(3, EGameFlow.CommuteIn, true)]
    [TestCase(3, EGameFlow.CommuteOut, true)]
    public void RainFollowsTheDayAndRoute(int day, EGameFlow flow, bool rain)
    {
        Assert.That(GameAudioRules.IsRainy(day, flow), Is.EqualTo(rain));
    }

    [Test]
    public void StreetRainParticlesFollowTheSchedule()
    {
        var state = GameStateManager.Instance;
        int day = state.CurrentDay; var flow = state.GameFlow;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var scene = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/OutSide.unity", OpenSceneMode.Additive);
        try
        {
            var schedule = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RainSchedule>(true)).Single();
            Assert.That(schedule.name, Is.EqualTo("FX_Rain"));
            var systems = schedule.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(systems, Is.Not.Empty);

            state.CurrentDay = 0; state.GameFlow = EGameFlow.CommuteIn;
            schedule.Apply();
            Assert.That(schedule.IsRaining, Is.False, "0일차 출근길은 맑다.");
            Assert.That(systems.All(s => !s.isEmitting), Is.True);

            state.GameFlow = EGameFlow.CommuteOut;
            schedule.Apply();
            Assert.That(schedule.IsRaining, Is.True, "퇴근길에 처음 나오면 비가 내린다.");
        }
        finally
        {
            state.CurrentDay = day; state.GameFlow = flow;
            EditorSceneManager.CloseScene(scene, true);
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }
}
