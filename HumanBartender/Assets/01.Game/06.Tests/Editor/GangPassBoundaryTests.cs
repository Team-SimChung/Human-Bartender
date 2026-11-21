using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 갱단 경비가 통행을 허락하기 전까지 아래층 왼쪽(갱단 구역)을 막는다
/// (엔진 미구현 리스트 · NPC 대화를 통한 구역 통행 개방, 웹 outside-neighborhood.js clampBoundary).
/// </summary>
public sealed class GangPassBoundaryTests
{
    const string ScenePath = "Assets/01.Game/01.Scenes/OutSide.unity";

    [TestCase(0, EGameFlow.CommuteIn, false, false, TestName = "Day0 first commute is open")]
    [TestCase(0, EGameFlow.CommuteOut, false, true, TestName = "Day0 way home is blocked")]
    [TestCase(1, EGameFlow.CommuteIn, false, true, TestName = "Day1 is blocked")]
    [TestCase(3, EGameFlow.CommuteOut, false, true, TestName = "Day3 without the pass is blocked")]
    [TestCase(2, EGameFlow.CommuteIn, true, false, TestName = "The guard's pass opens it")]
    public void BlockRuleMatchesTheGuardDialogue(int day, EGameFlow flow, bool granted, bool expected)
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var conditions = new ConditionUtil(GameStateManager.Instance, player, player);
            if (granted) conditions.ApplyRequired("flag.gang_pass_granted = true");
            Assert.That(OutsideStorySceneRules.IsGangPassBlocked(day, flow, conditions), Is.EqualTo(expected));
            Assert.That(OutsideStorySceneRules.IsAvailable("neighborhood-guardBlocked", day, flow, conditions), Is.EqualTo(expected),
                "경비의 '막힘' 대화와 이동 벽은 같은 조건을 쓴다.");
        }
        finally { Object.DestroyImmediate(player); }
    }

    [Test]
    public void AuthoredWallSitsWhereTheWebClampsLunaAndStartsOff()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var boundary = transforms.Select(t => t.GetComponent<GangPassBoundary>()).SingleOrDefault(b => b != null);
            Assert.That(boundary, Is.Not.Null, "OutSide에 GangPassBoundary가 있어야 합니다.");
            var context = transforms.Select(t => t.GetComponent<WorldInteractionSceneContext>()).Single(c => c != null);
            using (var data = new SerializedObject(context))
                Assert.That(data.FindProperty("gangPassBoundary").objectReferenceValue, Is.SameAs(boundary));

            using var serialized = new SerializedObject(boundary);
            var bounds = (GameObject)serialized.FindProperty("bounds").objectReferenceValue;
            var wall = (BoxCollider2D)serialized.FindProperty("wall").objectReferenceValue;
            Assert.That(bounds, Is.Not.Null);
            Assert.That(wall, Is.Not.Null);
            Assert.That(bounds.activeSelf, Is.False, "조건을 확인하기 전에는 길을 막지 않도록 꺼서 저장한다.");

            var guard = transforms.Single(t => t.name == "neighborhood-guardBlocked");
            var luna = context.Player.GetComponent<BoxCollider2D>();
            var movement = context.Player.GetComponent<PlayerMovement2D>();
            float halfWidth = luna.size.x * Mathf.Abs(luna.transform.lossyScale.x) * .5f;
            Assert.That(boundary.WallRightFace, Is.EqualTo(guard.position.x + .34f - halfWidth).Within(.01f),
                "웹은 루나 x를 경비 x + 0.34 이상으로 고정한다.");
            using (var move = new SerializedObject(movement))
            {
                int mask = move.FindProperty("groundLayer").intValue;
                Assert.That((mask & (1 << wall.gameObject.layer)) != 0, Is.True, "벽이 루나 이동 충돌 레이어에 있어야 막힌다.");
            }
            Assert.That(wall.transform.localScale, Is.EqualTo(Vector3.one));
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
}
