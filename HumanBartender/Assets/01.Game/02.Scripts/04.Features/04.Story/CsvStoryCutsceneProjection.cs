using System;
using System.Collections.Generic;
using HumanBartender.CutsceneStudio;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>현재 CSV 기억 대본에서 일시적 Studio 표시 데이터만 만든다. 원본 저장·플래그·재생 시계는 소유하지 않는다.</summary>
public sealed class CsvStoryCutsceneProjection : IDisposable
{
    readonly List<UnityEngine.Object> owned = new();
    public StudioSequence Sequence { get; private set; }

    public static bool TryGetSource(string id, string memoryKey, out string source)
    {
        source = id switch
        {
            "johnny-memory" => "johnny",
            "terrace-memory" when memoryKey == "terraceMemory3" => "terraceMemory3",
            "terrace-memory" => "terraceMemory",
            _ => null
        };
        return source != null;
    }

    public CsvStoryCutsceneProjection(string id, NewDialogueStepData[] rows, ELanguage language)
    {
        try
        {
            if (rows == null || rows.Length == 0) throw new InvalidOperationException("기억 대본이 비어 있습니다: " + id);
            Sequence = Own(ScriptableObject.CreateInstance<StudioSequence>());
            Sequence.Id = id;
            Sequence.AllowSkip = false;
            Sequence.Timeline = Own(ScriptableObject.CreateInstance<TimelineAsset>());
            Sequence.Timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            var track = Own(Sequence.Timeline.CreateTrack<StudioTrack>(null, "CSV 기억 대사"));
            double at = 0;
            foreach (var step in rows)
            {
                if (step.Type != ENewStepType.Say) throw new InvalidOperationException("표시 전용 기억 대본에 실행 명령이 있습니다: " + id);
                string text = language == ELanguage.En && !string.IsNullOrEmpty(step.Text?.En) ? step.Text?.En : step.Text?.Ko;
                if (string.IsNullOrEmpty(text)) throw new InvalidOperationException("기억 대사가 비어 있습니다: " + id);
                var timed = track.CreateClip<StudioClip>();
                var clip = Own((StudioClip)timed.asset);
                clip.Kind = StudioKind.Dialogue;
                clip.ActorId = "";
                clip.Text = string.IsNullOrEmpty(step.DisplayName) ? text : step.DisplayName + "\n" + text;
                clip.WaitForInput = true;
                clip.CharactersPerSecond = 35;
                clip.BubbleScreenHeight = 0;
                clip.UseBubbleLayout = true;
                clip.BubbleWidth = 820;
                timed.start = at;
                timed.duration = Math.Max(.15, clip.Text.Length / 35.0);
                at = timed.end;
            }
            Sequence.Timeline.fixedDuration = at;
        }
        catch { Dispose(); throw; }
    }

    T Own<T>(T value) where T : UnityEngine.Object { owned.Add(value); return value; }
    public void Dispose()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(owned[i]);
                else UnityEngine.Object.DestroyImmediate(owned[i]);
            }
        owned.Clear();
        Sequence = null;
    }
}
