using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DialogueFxRuntimeTests
{
    [UnityTest]
    [Explicit("Run in isolation: unrelated scene DOTween cancellation can fail during EnterPlayMode in the full EditMode batch.")]
    public IEnumerator PlayModeOneGlyphAndIndependentBubbleSessions()
    {
        // EnterPlayMode tears down any editor scene. Remove leftover editor tweens first;
        // their cancellation callbacks are unrelated to this empty-scene text test.
        DOTween.Clear(true);
        Debug.Log("[DialogueFX Runtime] before empty scene");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Debug.Log("[DialogueFX Runtime] before EnterPlayMode");
        yield return new EnterPlayMode();
        Debug.Log("[DialogueFX Runtime] after EnterPlayMode");
        var canvas = new GameObject("DialogueFX runtime canvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            DynamicSpeechBubble first = CreateBubble(canvas.transform, "first");
            DynamicSpeechBubble second = CreateBubble(canvas.transform, "second");
            var data = new TypingData("가<color=#FF5555><size=130%><pop><shake>또</shake></pop></size></color>나",
                "first", Vector3.zero, Color.white, true);
            UniTask playback = first.TextPlayer.PlayAsync(data, null, 0.5f);
            second.SetText("다<wave>른</wave>말", null);
            int secondId = second.TextPlayer.CurrentSessionId;
            yield return null;
            first.TextPlayer.Skip();
            yield return playback.ToCoroutine();
            Assert.That(first.TextPlayer.IsHolding, Is.True);
            Assert.That(first.textLabel.maxVisibleCharacters, Is.EqualTo(3));
            Assert.That(second.TextPlayer.CurrentSessionId, Is.EqualTo(secondId));
            Assert.That(second.textLabel.text, Is.EqualTo("다른말"));
            first.textLabel.ForceMeshUpdate(true, true);
            var info = first.textLabel.textInfo;
            var middle = info.characterInfo[1];
            var vertices = info.meshInfo[middle.materialReferenceIndex].vertices;
            Vector3 before = vertices[middle.vertexIndex];
            yield return new WaitForSeconds(0.03f);
            Vector3 after = vertices[middle.vertexIndex];
            Assert.That(after, Is.Not.EqualTo(before));
            first.TextPlayer.Stop();
            Assert.That(second.TextPlayer.CurrentSessionId, Is.EqualTo(secondId));

            using var external = new CancellationTokenSource();
            UniTask cancelled = first.TextPlayer.PlayAsync(data, null, 0.5f, external.Token);
            external.Cancel();
            yield return ExpectCancellation(cancelled).ToCoroutine();
            Assert.That(first.TextPlayer.CurrentSessionId, Is.Zero);
            Assert.That(first.textLabel.maxVisibleCharacters, Is.LessThan(3));

            UniTask replaced = first.TextPlayer.PlayAsync(data, null, 0.5f);
            first.SetText("새 문장");
            int replacementId = first.TextPlayer.CurrentSessionId;
            yield return ExpectCancellation(replaced).ToCoroutine();
            Assert.That(first.TextPlayer.CurrentSessionId, Is.EqualTo(replacementId));
            Assert.That(first.textLabel.text, Is.EqualTo("새 문장"));
        }
        finally { UnityEngine.Object.Destroy(canvas); }
        DOTween.Clear(true);
        Debug.Log("[DialogueFX Runtime] before ExitPlayMode");
        yield return new ExitPlayMode();
    }

    static async UniTask ExpectCancellation(UniTask operation)
    {
        try { await operation; }
        catch (OperationCanceledException) { return; }
        Assert.Fail("The replaced or externally cancelled reveal completed normally.");
    }

    static DynamicSpeechBubble CreateBubble(Transform parent, string name)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.SetActive(false);
        root.transform.SetParent(parent, false);
        var rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(400, 100);
        var content = new GameObject("text", typeof(RectTransform), typeof(TextMeshProUGUI));
        content.transform.SetParent(root.transform, false);
        var label = content.GetComponent<TextMeshProUGUI>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/06.Fonts/NeoDunggeunmo SDF.asset");
        var title = new GameObject("name", typeof(RectTransform), typeof(TextMeshProUGUI));
        title.transform.SetParent(root.transform, false);
        var bubble = root.AddComponent<DynamicSpeechBubble>();
        bubble.textLabel = label;
        bubble.nameLabel = title.GetComponent<TextMeshProUGUI>();
        bubble.bubble = rect;
        root.SetActive(true);
        return bubble;
    }
}
