using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class DialogueInputPromptTests
{
    [Test]
    public void WaitingFloatsWithoutScaledTimeAndStopsWithoutPositionDrift()
    {
        var root = new GameObject("dialogue prompt test");
        float previousTimeScale = Time.timeScale;
        try
        {
            var child = new GameObject("indicator", typeof(RectTransform), typeof(Image));
            child.transform.SetParent(root.transform, false);
            var rect = (RectTransform)child.transform;
            rect.anchoredPosition = new Vector2(-13f, 10f);
            Vector2 origin = rect.anchoredPosition;
            var prompt = root.AddComponent<DialogueInputPrompt>();
            var data = new SerializedObject(prompt);
            data.FindProperty("indicator").objectReferenceValue = rect;
            data.ApplyModifiedPropertiesWithoutUndo();
            Invoke(prompt, "OnEnable");
            Assert.That(child.activeSelf, Is.False, "Typing must start without an input prompt.");

            Time.timeScale = 0f;
            prompt.SetWaiting(true);
            SetTime(prompt, 0.625);
            Invoke(prompt, "LateUpdate");
            Assert.That(child.activeSelf, Is.True);
            Assert.That(rect.anchoredPosition.x, Is.EqualTo(origin.x));
            Assert.That(rect.anchoredPosition.y, Is.EqualTo(origin.y + 2f).Within(0.001f));
            prompt.SetWaiting(true);
            SetTime(prompt, 1.25);
            Invoke(prompt, "LateUpdate");
            Assert.That(rect.anchoredPosition, Is.EqualTo(origin), "Repeated waiting notifications must not move the baseline.");

            SetTime(prompt, 0.625);
            Invoke(prompt, "LateUpdate");
            prompt.SetWaiting(false);
            Assert.That(child.activeSelf, Is.False);
            Assert.That(rect.anchoredPosition, Is.EqualTo(origin));
            prompt.SetWaiting(true);
            SetTime(prompt, 0.625);
            Invoke(prompt, "LateUpdate");
            Invoke(prompt, "OnDisable");
            Invoke(prompt, "OnEnable");
            Assert.That(child.activeSelf, Is.False);
            Assert.That(rect.anchoredPosition, Is.EqualTo(origin));
        }
        finally
        {
            Time.timeScale = previousTimeScale;
            Object.DestroyImmediate(root);
        }
    }

    [TestCase("Dialogue TextBox.prefab")]
    [TestCase("OutsideDialogue TextBox.prefab")]
    public void DialoguePrefabsUseTheActualNonBlockingNextSpriteAtBottomRight(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/01.Game/03.Content/08.Prefabs/03.UI/02.Dialogue/" + name);
        var prompt = prefab.GetComponent<DialogueInputPrompt>();
        Assert.That(prompt, Is.Not.Null);
        var data = new SerializedObject(prompt);
        var indicator = (RectTransform)data.FindProperty("indicator").objectReferenceValue;
        Assert.That(indicator.parent, Is.EqualTo(prefab.transform));
        Assert.That(indicator.anchorMin, Is.EqualTo(Vector2.right));
        Assert.That(indicator.anchorMax, Is.EqualTo(Vector2.right));
        var image = indicator.GetComponent<Image>();
        Assert.That(image.raycastTarget, Is.False);
        Assert.That(image.sprite, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(image.sprite), Does.EndWith("/IMG_textbox_Next.png"));
        Assert.That(data.FindProperty("floatHeight").floatValue, Is.GreaterThan(0f));
        Assert.That(data.FindProperty("floatPeriod").floatValue, Is.GreaterThan(0f));
    }

    private static void SetTime(DialogueInputPrompt prompt, double elapsed) =>
        typeof(DialogueInputPrompt).GetField("startedAt", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(prompt, Time.unscaledTimeAsDouble - elapsed);

    private static void Invoke(DialogueInputPrompt prompt, string method) =>
        typeof(DialogueInputPrompt).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(prompt, null);
}
