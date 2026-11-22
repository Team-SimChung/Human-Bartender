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

        // 계속할 수 있는 종류이고, 클립이 '계속 재생'이거나 '트랙 설정 따름'이면서 트랙이 켜져 있을 때. '멈춤'은 트랙보다 우선함.
        public bool ContinuesDuringWait => StudioEvaluation.CanContinueDuringWait(Asset) && Asset.WaitPlayback switch
        {
            StudioWaitPlayback.Continue => true,
            StudioWaitPlayback.Pause => false,
            _ => Clip.GetParentTrack() is StudioTrack track && track.ContinueDuringWait
        };
        public bool Contains(double time) => time >= Clip.start && time < Clip.end;
        public float Progress(double time) => Mathf.Clamp01((float)((time - Clip.start) / Math.Max(.001, Clip.duration)));
    }

    public static class StudioEvaluation
    {
        public static bool IsSupported(StudioKind kind) => Enum.IsDefined(typeof(StudioKind), kind);

        public static bool UsesActor(StudioClip clip) => clip.Kind switch
        {
            StudioKind.Visual => true,
            StudioKind.Dialogue => !string.IsNullOrEmpty(clip.ActorId),
            _ => false
        };
        public static bool OwnsActor(StudioClip clip) => clip.Kind == StudioKind.Visual;
        // 입력 대기 중에도 계속할 수 있는 종류: 사운드, 반복 그림, 화면 머티리얼이나 흔들림 효과. 그 밖은 클립 / 트랙 설정과 상관없이 멈춤.
        public static bool CanContinueDuringWait(StudioClip clip) => clip.Kind == StudioKind.Audio || clip.Kind == StudioKind.Visual && clip.Loop ||
            clip.Kind == StudioKind.Effect && (clip.ScreenMaterial != null || clip.Effect == StudioEffect.Shake);
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
                    if (clip.asset is StudioClip asset && IsSupported(asset.Kind))
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
            return Mathf.Clamp01(fade) * StudioKeyframes.Evaluate(c, StudioProperty.Volume, (float)age, entry.Clip.duration);
        }
    }

}
