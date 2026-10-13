using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

namespace HumanBartender.CutsceneStudio
{
    public readonly struct StudioEntry
    {
        public readonly TimelineClip Clip;
        public readonly StudioClip Asset;
        public StudioEntry(TimelineClip clip, StudioClip asset)
        {
            Clip = clip;
            Asset = asset;
        }

        public bool Contains(double time) => time >= Clip.start && time < Clip.end;
        public float Progress(double time) => Mathf.Clamp01((float)((time - Clip.start) / Math.Max(.001, Clip.duration)));
    }

    public static class StudioEvaluation
    {
        public static bool UsesActor(StudioClip clip) => clip.Kind switch
        {
            StudioKind.Actor or StudioKind.Visual => true,
            StudioKind.State => !clip.StateCamera,
            StudioKind.Dialogue => !string.IsNullOrEmpty(clip.ActorId),
            _ => false
        };
        public static bool OwnsActor(StudioClip clip) => clip.Kind == StudioKind.Actor || clip.Kind == StudioKind.Visual;
        public static HashSet<string> ActorIds(StudioSequence sequence, bool includeMuted = true)
        {
            var ids = new HashSet<string>();
            foreach (var e in Clips(sequence, includeMuted))
                if (OwnsActor(e.Asset))
                    ids.Add(e.Asset.ActorId);
            return ids;
        }

        // 에디터·게임·내보내기 검사에서 같은 화면 맞춤 계산을 사용함.
        public static Rect Fit(Rect bounds)
        {
            float scale = Mathf.Min(bounds.width / StudioSequence.Width, bounds.height / StudioSequence.Height);
            var size = new Vector2(StudioSequence.Width, StudioSequence.Height) * Mathf.Max(0, scale);
            return new Rect(bounds.center - size * .5f, size);
        }

        public static List<StudioEntry> Clips(StudioSequence sequence, bool includeMuted = false)
        {
            var result = new List<StudioEntry>();
            CollectClips(sequence, result, includeMuted);
            return result;
        }

        public static void CollectClips(StudioSequence sequence, List<StudioEntry> result, bool includeMuted = false)
        {
            result.Clear();
            if (sequence == null || sequence.Timeline == null)
                return;
            foreach (var track in sequence.Timeline.GetOutputTracks())
            {
                if (!includeMuted && track.mutedInHierarchy)
                    continue;
                foreach (var clip in track.GetClips())
                    if (clip.asset is StudioClip asset)
                        result.Add(new StudioEntry(clip, asset));
            }

            // 시작 시간이 같으면 작성된 트랙 순서를 유지함.
            for (int i = 1; i < result.Count; i++)
            {
                var entry = result[i];
                int j = i - 1;
                while (j >= 0 && result[j].Clip.start > entry.Clip.start)
                {
                    result[j + 1] = result[j];
                    j--;
                }

                result[j + 1] = entry;
            }
        }

        public static Color Composite(Color background, Color foreground)
        {
            float alpha = foreground.a + background.a * (1 - foreground.a);
            if (alpha <= 0)
                return Color.clear;
            float remaining = background.a * (1 - foreground.a);
            return new Color((foreground.r * foreground.a + background.r * remaining) / alpha, (foreground.g * foreground.a + background.g * remaining) / alpha, (foreground.b * foreground.a + background.b * remaining) / alpha, alpha);
        }

        public static float AudioGain(StudioEntry entry, double time)
        {
            var c = entry.Asset;
            double age = time - entry.Clip.start;
            float fade = Mathf.Min(c.FadeIn <= 0 ? 1 : (float)(age / c.FadeIn), c.FadeOut <= 0 ? 1 : (float)((entry.Clip.end - time) / c.FadeOut));
            return Mathf.Clamp01(fade) * c.Volume;
        }
    }

    /// <summary>입력 대기 진행과 화면 평가를 분리해 탐색 중 이벤트가 발생하지 않게 함.</summary>
    public sealed class StudioClock
    {
        private readonly HashSet<StudioClip> passed = new();
        private readonly List<StudioEntry> entries = new();
        public double Time { get; private set; }
        public StudioEntry? Waiting { get; private set; }
        public bool Finished { get; private set; }

        public void Seek(double time)
        {
            Time = Math.Max(0, time);
            Waiting = null;
            Finished = false;
            passed.Clear();
        }

        public void Advance()
        {
            if (Waiting.HasValue)
                passed.Add(Waiting.Value.Asset);
            Waiting = null;
        }

        public void Tick(StudioSequence sequence, double delta, bool gates)
        {
            if (Finished || Waiting.HasValue)
                return;
            double next = Math.Min(sequence.Duration, Time + Math.Max(0, delta));
            if (gates)
            {
                StudioEntry? first = null;
                double earliest = double.PositiveInfinity;
                StudioEvaluation.CollectClips(sequence, entries);
                foreach (var e in entries)
                {
                    if (!(e.Asset.Kind == StudioKind.Wait || e.Asset.Kind == StudioKind.Dialogue && e.Asset.WaitForInput) || passed.Contains(e.Asset))
                        continue;
                    double gate = e.Asset.Kind == StudioKind.Wait ? e.Clip.start : e.Clip.end;
                    if (gate >= Time - .000001 && gate <= next && gate < earliest)
                    {
                        first = e;
                        earliest = gate;
                    }
                }

                if (first.HasValue)
                {
                    Time = earliest;
                    Waiting = first;
                    return;
                }
            }

            Time = next;
            Finished = Time >= sequence.Duration;
        }

        public double DisplayTime => Waiting.HasValue && Waiting.Value.Asset.Kind == StudioKind.Dialogue ? Math.Max(Waiting.Value.Clip.start, Time - .00001) : Time;
    }
}
