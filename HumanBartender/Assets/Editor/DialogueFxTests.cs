using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DialogueFxTests
{
    NewTextTagDataSO tags;
    GameObject textObject;
    TextMeshPro label;

    [SetUp]
    public void SetUp()
    {
        tags = ScriptableObject.CreateInstance<NewTextTagDataSO>();
        tags.textTagData = new Dictionary<string, NewTextTagData>
        {
            ["world"] = new() { Kind = "color", Value = "#6EC1FF" },
            ["name"] = new() { Kind = "color", Value = "#FFD479" },
            ["order"] = new() { Kind = "color", Value = "#8FE388" },
            ["slow"] = new() { Kind = "speed_ms", Value = 120 },
            ["fast"] = new() { Kind = "speed_ms", Value = 20 },
            ["big"] = new() { Kind = "size_pct", Value = 130 },
            ["small"] = new() { Kind = "size_pct", Value = 80 },
        };
        textObject = new GameObject("DialogueFX test", typeof(TextMeshPro));
        label = textObject.GetComponent<TextMeshPro>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/06.Fonts/NeoDunggeunmo SDF.asset");
        Assert.That(label.font, Is.Not.Null);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(textObject);
        UnityEngine.Object.DestroyImmediate(tags);
    }

    DialogueTextCompiler.Line Bind(string raw, float defaultSpeed = 0.05f,
        DialoguePresentationSettings settings = null)
    {
        var line = DialogueTextCompiler.Compile(raw, tags, settings, null, defaultSpeed);
        label.text = line.Text;
        label.ForceMeshUpdate(true, true);
        line.Bind(label.textInfo);
        return line;
    }

    [Test]
    public void OneGlyphCombinesStaticAndAnimatedEffects()
    {
        var line = Bind("또<color=#FF5555><size=130%><pop><shake>또</shake></pop></size></color>또");
        Assert.That(line.Characters.Length, Is.EqualTo(3));
        Assert.That(line.Characters[0].Shake, Is.False);
        Assert.That(line.Characters[1].Shake, Is.True);
        Assert.That(line.Characters[1].Pop, Is.True);
        Assert.That(line.Characters[2].Shake, Is.False);
        Assert.That(line.Text, Does.Contain("<size=130%>"));
        Assert.That(line.Text, Does.Contain("<color=#FF5555>"));
    }

    [Test]
    public void DayOneTouchyUrgeBarkFromCsvEmphasizesItsAccusation()
    {
        string directory = Path.Combine(Application.dataPath, "StreamingAssets", "csv");
        var barks = CsvDataReader.LoadDirectory(directory).Read<NewBarkData[]>("barks");
        string raw = barks.Single(b => b.VoiceId == "touchy" && b.Situation == "call_urge" &&
            b.Text.Ko.StartsWith("사람 ", StringComparison.Ordinal)).Text.Ko;
        var line = Bind(raw, settings: DialoguePresentationSettings.Shared);
        Assert.That(label.GetParsedText(), Is.EqualTo("사람 무시하나 지금..."));
        Assert.That(line.Characters.Length, Is.EqualTo(13));
        for (int i = 0; i < line.Characters.Length; i++)
            Assert.That(line.Characters[i].Shake, Is.EqualTo(i >= 3 && i <= 6), $"glyph {i}");

        var info = label.textInfo;
        var normal = info.characterInfo[0];
        var emphasized = info.characterInfo[3];
        Assert.That(emphasized.ascender - emphasized.descender,
            Is.GreaterThan((normal.ascender - normal.descender) * 1.1f));
        var color = info.meshInfo[emphasized.materialReferenceIndex].colors32[emphasized.vertexIndex];
        Assert.That(color.r, Is.GreaterThan(color.g));

        var animator = textObject.AddComponent<DialogueTextAnimator>();
        animator.Bind(label, line, false);
        Vector3 beforeEmphasis = info.meshInfo[emphasized.materialReferenceIndex].vertices[emphasized.vertexIndex];
        Vector3 beforeNormal = info.meshInfo[normal.materialReferenceIndex].vertices[normal.vertexIndex];
        animator.SetVisible(line.Characters.Length, 1f, false);
        animator.SetVisible(line.Characters.Length, 1.042f, false);
        Assert.That(info.meshInfo[emphasized.materialReferenceIndex].vertices[emphasized.vertexIndex],
            Is.Not.EqualTo(beforeEmphasis));
        Assert.That(info.meshInfo[normal.materialReferenceIndex].vertices[normal.vertexIndex],
            Is.EqualTo(beforeNormal));
        animator.Clear();
    }

    [Test]
    public void DayZeroChrisCsvKeepsWordsAndAppliesEffectsOnlyToEmotionalPhrases()
    {
        string directory = Path.Combine(Application.dataPath, "StreamingAssets", "csv");
        var script = CsvDataReader.LoadDirectory(directory).Read<NewDayScriptBase>("script/bar/day0");
        var steps = script.Scenes.SelectMany(scene => scene.Steps)
            .Where(step => step.DialogueId != null).ToDictionary(step => step.DialogueId);
        var cases = new[]
        {
            (id: "dlg_d1_tutorial_chris_003", plain: "방금 내가 무슨 말을 했는지 듣긴 한 거야?", phrase: "듣긴 한 거야?", shake: true, wave: false, enlarged: true),
            (id: "dlg_d1_tutorial_chris_012", plain: "또 딴생각하지.", phrase: "또", shake: true, wave: false, enlarged: false),
            (id: "dlg_d1_tutorial_chris_054", plain: "…생각보다 재능이 있군.", phrase: "재능이 있군", shake: false, wave: true, enlarged: false),
            (id: "dlg_d1_tutorial_chris_060", plain: "괜찮군.", phrase: "괜찮군", shake: false, wave: true, enlarged: false),
            (id: "dlg_d1_tutorial_chris_065", plain: "아직 손님에게 내놓기엔 완성도가 부족한데.", phrase: "부족한데", shake: true, wave: false, enlarged: true),
            (id: "dlg_d1_tutorial_chris_067", plain: "이런 식이면 곤란한데, 루나.\n제대로 만들어.", phrase: "제대로 만들어.", shake: true, wave: false, enlarged: true),
            (id: "dlg_d1_tutorial_wrap_011", plain: "좋아. 처음에는 그렇게 시작하는 거지.", phrase: "좋아.", shake: false, wave: true, enlarged: false),
        };

        foreach (var item in cases)
        {
            var step = steps[item.id];
            Assert.That(step.Actor, Is.EqualTo("chris"), item.id);
            var line = Bind(step.Text.Value.Ko, settings: DialoguePresentationSettings.Shared);
            string parsed = label.GetParsedText();
            Assert.That(parsed, Is.EqualTo(item.plain), item.id);
            int first = parsed.IndexOf(item.phrase, StringComparison.Ordinal);
            Assert.That(first, Is.GreaterThanOrEqualTo(0), item.id);
            for (int i = 0; i < line.Characters.Length; i++)
            {
                bool emphasized = i >= first && i < first + item.phrase.Length;
                Assert.That(line.Characters[i].Shake, Is.EqualTo(emphasized && item.shake), $"{item.id} glyph {i} shake");
                Assert.That(line.Characters[i].Wave, Is.EqualTo(emphasized && item.wave), $"{item.id} glyph {i} wave");
            }
            if (item.enlarged)
            {
                var normal = label.textInfo.characterInfo[0];
                var emphasized = label.textInfo.characterInfo[first];
                Assert.That(emphasized.ascender - emphasized.descender,
                    Is.GreaterThan((normal.ascender - normal.descender) * 1.1f), item.id);
            }
        }
    }

    [Test]
    public void DayZeroChrisCsvPacesItsEmotionalBeats()
    {
        string directory = Path.Combine(Application.dataPath, "StreamingAssets", "csv");
        var script = CsvDataReader.LoadDirectory(directory).Read<NewDayScriptBase>("script/bar/day0");
        var steps = script.Scenes.SelectMany(scene => scene.Steps)
            .Where(step => step.DialogueId != null).ToDictionary(step => step.DialogueId);
        var beats = new[]
        {
            (id: "dlg_d1_tutorial_chris_003", phrase: "듣긴", wait: 0.15f, speed: 0.02f),
            (id: "dlg_d1_tutorial_chris_012", phrase: "딴생각하지", wait: 0.10f, speed: 0.02f),
            (id: "dlg_d1_tutorial_chris_054", phrase: "재능이", wait: 0.18f, speed: 0.12f),
            (id: "dlg_d1_tutorial_chris_065", phrase: "부족한데", wait: 0.18f, speed: 0.12f),
            (id: "dlg_d1_tutorial_chris_067", phrase: "제대로", wait: 0.25f, speed: 0.02f),
            (id: "dlg_d1_tutorial_wrap_011", phrase: " 처음에는", wait: 0.12f, speed: 0.04f),
        };

        foreach (var beat in beats)
        {
            var line = Bind(steps[beat.id].Text.Value.Ko, 0.04f, DialoguePresentationSettings.Shared);
            int first = label.GetParsedText().IndexOf(beat.phrase, StringComparison.Ordinal);
            Assert.That(first, Is.GreaterThanOrEqualTo(0), beat.id);
            Assert.That(line.WaitBefore[first], Is.EqualTo(beat.wait).Within(0.001f), beat.id);
            Assert.That(line.Characters[first].SpeedSeconds, Is.EqualTo(beat.speed).Within(0.001f), beat.id);
        }
    }

    [Test]
    public void OneKoreanGlyphMovesWhileNeighborsKeepTheirVertices()
    {
        var line = Bind("가<color=#FF5555><size=130%><pop><shake>또</shake></pop></size></color>나");
        var animator = textObject.AddComponent<DialogueTextAnimator>();
        animator.Bind(label, line, false);
        var info = label.textInfo;
        Assert.That(info.characterInfo[1].ascender - info.characterInfo[1].descender,
            Is.GreaterThan((info.characterInfo[0].ascender - info.characterInfo[0].descender) * 1.1f));
        var colored = info.characterInfo[1];
        var color = info.meshInfo[colored.materialReferenceIndex].colors32[colored.vertexIndex];
        Assert.That(color.r, Is.GreaterThan(color.g));
        var before = new Vector3[3];
        for (int i = 0; i < 3; i++)
        {
            var ch = info.characterInfo[i];
            before[i] = info.meshInfo[ch.materialReferenceIndex].vertices[ch.vertexIndex];
        }
        animator.SetVisible(3, 10f, false);
        animator.SetVisible(3, 10.042f, false);
        for (int i = 0; i < 3; i++)
        {
            var ch = info.characterInfo[i];
            var after = info.meshInfo[ch.materialReferenceIndex].vertices[ch.vertexIndex];
            if (i == 1) Assert.That(after, Is.Not.EqualTo(before[i]));
            else Assert.That(after, Is.EqualTo(before[i]));
        }
        animator.Clear();
    }

    [Test]
    public void NestedSpeedRestoresOuterAndDefault()
    {
        var line = Bind("가<slow>나<fast>다</fast>라</slow>마");
        Assert.That(line.Characters.Length, Is.EqualTo(5));
        Assert.That(line.Characters[0].SpeedSeconds, Is.EqualTo(0.05f).Within(0.001f));
        Assert.That(line.Characters[1].SpeedSeconds, Is.EqualTo(0.12f).Within(0.001f));
        Assert.That(line.Characters[2].SpeedSeconds, Is.EqualTo(0.02f).Within(0.001f));
        Assert.That(line.Characters[3].SpeedSeconds, Is.EqualTo(0.12f).Within(0.001f));
        Assert.That(line.Characters[4].SpeedSeconds, Is.EqualTo(0.05f).Within(0.001f));
    }

    [Test]
    public void NamedPresetAppliesOnlyInsideItsSpanAndRestoresOuterStyle()
    {
        var settings = DialoguePresentationSettings.Shared;
        Assert.That(settings, Is.Not.Null);
        var line = Bind("가<fx=reproach>나<wave>다</wave></fx>라", settings: settings);
        Assert.That(label.GetParsedText(), Is.EqualTo("가나다라"));
        Assert.That(line.WaitBefore[1], Is.EqualTo(0.15f).Within(0.001f));
        Assert.That(line.Characters[1].Shake, Is.True);
        Assert.That(line.Characters[1].SpeedSeconds, Is.EqualTo(0.02f).Within(0.001f));
        Assert.That(line.Characters[2].Shake && line.Characters[2].Wave, Is.True);
        Assert.That(line.Characters[3].Shake || line.Characters[3].Wave, Is.False);
        Assert.That(line.Characters[3].SpeedSeconds, Is.EqualTo(0.05f).Within(0.001f));
    }

    [Test]
    public void UnknownNamedPresetDoesNotBecomeVisibleDialogue()
    {
        Assert.That(DialogueTextCompiler.TryCompile("<fx=missing>가</fx>", tags,
            DialoguePresentationSettings.Shared, null, 0.05f, out _, out string error), Is.False);
        Assert.That(error, Does.Contain("Unknown dialogue FX preset"));
    }

    [Test]
    public void WaitsBindAcrossRichTextAndAddAtSameBoundary()
    {
        var line = Bind("<color=#FF5555>아</color><100><200>!");
        Assert.That(line.Characters.Length, Is.EqualTo(2));
        Assert.That(line.WaitBefore[1], Is.EqualTo(0.3f).Within(0.001f));
        Assert.That(line.WaitBefore[0], Is.Zero);
    }

    [Test]
    public void SeparateEffectsAndPlaceholderLengthBindToCorrectGlyphs()
    {
        var line = DialogueTextCompiler.Compile("<wave>가</wave> {cocktail}<shake>!</shake>", tags, null, "긴 이름");
        label.text = line.Text;
        label.ForceMeshUpdate(true, true);
        line.Bind(label.textInfo);
        Assert.That(line.Characters[0].Wave, Is.True);
        Assert.That(line.Characters[line.Characters.Length - 1].Shake, Is.True);
        Assert.That(line.Characters[line.Characters.Length - 2].Shake, Is.False);
    }

    [Test]
    public void MalformedDecorativeTagProducesDiagnostic()
    {
        Assert.That(DialogueTextCompiler.TryCompile("<shake amp=-1>가</shake>", tags, null,
            null, 0.05f, out _, out string error), Is.False);
        Assert.That(error, Does.Contain("UTF-16 0"));
    }

    [Test]
    public void ZeroWaitAndLastBoundaryAreValid()
    {
        var line = Bind("<0>가<100>");
        Assert.That(line.WaitBefore[0], Is.Zero);
        Assert.That(line.WaitBefore[1], Is.EqualTo(0.1f).Within(0.001f));
    }

    [Test]
    public void UnicodeAndBrDoNotShiftTheFollowingEffect()
    {
        var line = Bind("한😀<br>줄<shake>!</shake>");
        Assert.That(line.Characters.Length, Is.GreaterThanOrEqualTo(4));
        Assert.That(line.Characters[line.Characters.Length - 1].Shake, Is.True);
        Assert.That(line.Characters[line.Characters.Length - 2].Shake, Is.False);
    }

    [Test]
    public void AnimatorRestoresBaseMeshAfterRepeatedFrames()
    {
        var line = Bind("A<shake>B</shake>C");
        var animator = textObject.AddComponent<DialogueTextAnimator>();
        animator.Bind(label, line, false);
        var ch = label.textInfo.characterInfo[1];
        var vertices = label.textInfo.meshInfo[ch.materialReferenceIndex].vertices;
        Vector3 baseline = vertices[ch.vertexIndex];
        animator.SetVisible(3, 1f, false);
        animator.SetVisible(3, 1.12f, false);
        animator.SetVisible(3, 1.12f, false);
        Vector3 once = vertices[ch.vertexIndex];
        animator.SetVisible(3, 1.12f, false);
        Assert.That(vertices[ch.vertexIndex], Is.EqualTo(once));
        animator.Clear();
        Assert.That(vertices[ch.vertexIndex], Is.EqualTo(baseline));
    }

    [Test]
    public void PopScalesFromAndReturnsToItsStaticTMPSize()
    {
        var line = Bind("A<size=150%><pop peak=1.5 duration=0.2>B</pop></size>C");
        var animator = textObject.AddComponent<DialogueTextAnimator>();
        animator.Bind(label, line, false);
        var ch = label.textInfo.characterInfo[1];
        var vertices = label.textInfo.meshInfo[ch.materialReferenceIndex].vertices;
        float baseline = vertices[ch.vertexIndex + 2].x - vertices[ch.vertexIndex].x;
        animator.SetVisible(3, 0f, false);
        animator.SetVisible(3, 0.07f, false);
        float peak = vertices[ch.vertexIndex + 2].x - vertices[ch.vertexIndex].x;
        Assert.That(peak, Is.GreaterThan(baseline * 1.3f));
        animator.SetVisible(3, 0.2f, false);
        float returned = vertices[ch.vertexIndex + 2].x - vertices[ch.vertexIndex].x;
        Assert.That(returned, Is.EqualTo(baseline).Within(0.001f));
    }

    [Test]
    public void ReducedMotionPreservesStaticColorAndSizeWithoutMovingVertices()
    {
        var line = Bind("가<color=#FF5555><size=130%><pop><shake>또</shake></pop></size></color>나");
        var animator = textObject.AddComponent<DialogueTextAnimator>();
        animator.Bind(label, line, true);
        var info = label.textInfo;
        var ch = info.characterInfo[1];
        var vertices = info.meshInfo[ch.materialReferenceIndex].vertices;
        Vector3 before = vertices[ch.vertexIndex];
        var color = info.meshInfo[ch.materialReferenceIndex].colors32[ch.vertexIndex];
        animator.SetVisible(3, 0f, false);
        animator.SetVisible(3, 0.07f, false);
        Assert.That(vertices[ch.vertexIndex], Is.EqualTo(before));
        Assert.That(color.r, Is.GreaterThan(color.g));
        Assert.That(ch.ascender - ch.descender,
            Is.GreaterThan((info.characterInfo[0].ascender - info.characterInfo[0].descender) * 1.1f));
    }

    [Test]
    public void TwoBubblesOwnIndependentInstantSessionsAndReplacement()
    {
        var canvas = new GameObject("FX canvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            DynamicSpeechBubble left = CreateBubble(canvas.transform, "left");
            DynamicSpeechBubble right = CreateBubble(canvas.transform, "right");
            left.SetText("왼<shake>쪽</shake>", tags);
            right.SetText("오<wave>른</wave>", tags);
            int rightId = right.TextPlayer.CurrentSessionId;
            Assert.That(left.TextPlayer.CurrentSessionId, Is.GreaterThan(0));
            Assert.That(rightId, Is.GreaterThan(0));
            left.SetText("교체", tags);
            Assert.That(left.textLabel.text, Is.EqualTo("교체"));
            Assert.That(right.textLabel.text, Is.EqualTo("오른"));
            Assert.That(right.TextPlayer.CurrentSessionId, Is.EqualTo(rightId));
            left.TextPlayer.Stop();
            Assert.That(right.TextPlayer.CurrentSessionId, Is.EqualTo(rightId));
        }
        finally { UnityEngine.Object.DestroyImmediate(canvas); }
    }

    [UnityTest, Timeout(10000)]
    public IEnumerator SkipExternalCancellationAndReplacementKeepTheirOwnSessions()
    {
        var canvas = new GameObject("FX canvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            DynamicSpeechBubble left = CreateBubble(canvas.transform, "left");
            DynamicSpeechBubble right = CreateBubble(canvas.transform, "right");
            var line = new TypingData("가<shake>또</shake>나", "speaker", Vector3.zero, Color.white, true);
            UniTask skipped = left.TextPlayer.PlayAsync(line, tags, 0.5f);
            right.SetText("다<wave>른</wave>말", tags);
            int rightId = right.TextPlayer.CurrentSessionId;
            left.TextPlayer.Skip();
            yield return skipped.ToCoroutine();
            Assert.That(left.TextPlayer.IsHolding, Is.True);
            Assert.That(left.textLabel.maxVisibleCharacters, Is.EqualTo(3));
            Assert.That(right.TextPlayer.CurrentSessionId, Is.EqualTo(rightId));

            using var external = new CancellationTokenSource();
            UniTask cancelled = left.TextPlayer.PlayAsync(line, tags, 0.5f, external.Token);
            external.Cancel();
            bool wasCancelled = false;
            yield return WasCancelled(cancelled).ToCoroutine(result => wasCancelled = result);
            Assert.That(wasCancelled, Is.True);
            Assert.That(left.TextPlayer.CurrentSessionId, Is.Zero);
            Assert.That(left.textLabel.maxVisibleCharacters, Is.LessThan(3));

            UniTask replaced = left.TextPlayer.PlayAsync(line, tags, 0.5f);
            left.SetText("새 문장", tags);
            int replacementId = left.TextPlayer.CurrentSessionId;
            bool oldWasCancelled = false;
            yield return WasCancelled(replaced).ToCoroutine(result => oldWasCancelled = result);
            Assert.That(oldWasCancelled, Is.True);
            Assert.That(left.TextPlayer.CurrentSessionId, Is.EqualTo(replacementId));
            Assert.That(left.textLabel.text, Is.EqualTo("새 문장"));
            Assert.That(right.TextPlayer.CurrentSessionId, Is.EqualTo(rightId));
        }
        finally { UnityEngine.Object.DestroyImmediate(canvas); }
    }

    static async UniTask<bool> WasCancelled(UniTask operation)
    {
        try { await operation; return false; }
        catch (OperationCanceledException) { return true; }
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
