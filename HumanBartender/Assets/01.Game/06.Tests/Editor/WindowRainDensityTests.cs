using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// QA-34·QA-45: 유리에 맺히는 작은 물방울이 너무 많아 환공포증을 일으킨다는 피드백.
/// 타이틀·집·거리 모두 같은 밀도로 줄였는지 검사한다.
/// </summary>
public sealed class WindowRainDensityTests
{
    [TestCase("Assets/01.Game/01.Scenes/Main.unity")]
    [TestCase("Assets/01.Game/01.Scenes/Home.unity")]
    [TestCase("Assets/01.Game/01.Scenes/OutSide.unity")]
    public void WindowRainKeepsSmallDropsSparse(string path)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var layer = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WindowRainLayer>(true)).Single();
            var settings = new SerializedObject(layer).FindProperty("settings");
            Assert.That(settings.FindPropertyRelative("spawnPerSecond").floatValue, Is.EqualTo(15), "초당 새로 맺히는 방울");
            Assert.That(settings.FindPropertyRelative("maxDrops").intValue, Is.EqualTo(180), "동시에 있는 방울");
            Assert.That(settings.FindPropertyRelative("spawnRadius").vector2Value.x, Is.EqualTo(.8f).Within(.001f), "가장 작은 방울");
            Assert.That(settings.FindPropertyRelative("trailChance").floatValue, Is.EqualTo(.35f).Within(.001f), "흐르며 남기는 방울");
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
}
