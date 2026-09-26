using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DialogueFxSceneWiringTests
{
    [TestCase("Play", true)]
    [TestCase("Home", false)]
    [TestCase("OutSide", false)]
    public void SceneKeepsDialogueViewAndBubbleReferences(string sceneName, bool isBar)
    {
        Scene scene = EditorSceneManager.OpenScene($"Assets/00.Scenes/{sceneName}.unity", OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var views = roots.SelectMany(root => root.GetComponentsInChildren<UIDialogueTextView>(true)).ToArray();
            Assert.That(views, Is.Not.Empty, $"{sceneName}: dialogue view");
            foreach (var view in views)
            {
                Assert.That(view.lunaSpeechBubble, Is.Not.Null, $"{sceneName}: player bubble");
                Assert.That(view.customerSpeechBubble, Is.Not.Null, $"{sceneName}: customer bubble");
                Assert.That(view.canvasRect, Is.Not.Null, $"{sceneName}: canvas");
                Assert.That(new SerializedObject(view).FindProperty("textTagData").objectReferenceValue,
                    Is.Not.Null, $"{sceneName}: CSV tag target");
            }

            if (isBar)
            {
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<InGameLifetimeScope>(true)),
                    Is.Not.Empty, "Play: in-game DI scope");
                var presenter = roots.SelectMany(root => root.GetComponentsInChildren<BarStoryPresenter>(true)).Single();
                Assert.That(views.Contains(new SerializedObject(presenter).FindProperty("textView").objectReferenceValue
                    as UIDialogueTextView), Is.True, "Play: story presenter view");
                var slots = roots.SelectMany(root => root.GetComponentsInChildren<GuestSlot>(true)).ToArray();
                Assert.That(slots.Length, Is.GreaterThanOrEqualTo(2));
                foreach (var slot in slots)
                {
                    var serialized = new SerializedObject(slot);
                    Assert.That(serialized.FindProperty("bubbleRoot").objectReferenceValue,
                        Is.Not.Null, "Play: Bark root");
                    Assert.That(serialized.FindProperty("speechBubble").objectReferenceValue,
                        Is.Not.Null, "Play: Bark bubble");
                }
            }
            else
            {
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<OutsideGameLifetimeScope>(true)),
                    Is.Not.Empty, $"{sceneName}: outside DI scope");
                var presenter = roots.SelectMany(root => root.GetComponentsInChildren<OutsideDialoguePresenter>(true)).Single();
                Assert.That(views.Contains(new SerializedObject(presenter).FindProperty("typer").objectReferenceValue
                    as UIDialogueTextView), Is.True, $"{sceneName}: presenter view");
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
}
