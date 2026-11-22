using System;
using System.Collections.Generic;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal enum StudioWaitMark
    {
        None,
        Bar, // 한 바퀴 칸 안에서 반복 위상을 따라 움직이는 막대
        Dot  // 주기가 없는 것(흔들림, 화면 필터, 한 번 재생 사운드의 남은 소리)
    }

    // 타임라인 반복 표시의 계산. 그리기와 분리해 테스트에서 직접 부른다.
    // 시간은 모두 클립 시작 기준 로컬 초이고, 한 바퀴의 위상은 (t + lead) / cycle의 소수부다.
    internal static class StudioLoopMarks
    {
        // 반복 한 바퀴의 타임라인 길이(cycle)와, 원본 시작 때문에 첫 눈금이 당겨지는 양(lead).
        // 그림은 StudioSpriteAnimation.Sample, 사운드는 StudioAudioPlayback의 식을 따른다. 반복하지 않으면 false.
        internal static bool TryCycle(StudioClip clip, out double cycle, out double lead)
        {
            cycle = lead = 0;
            if (clip == null || !clip.Loop)
                return false;
            if (clip.Kind == StudioKind.Visual && clip.Frames != null && clip.Frames.Length >= 2)
            {
                // age = 원본 시작 + t × 속도이고, age가 원본 한 바퀴의 배수일 때 첫 프레임으로 돌아감.
                float speed = StudioSpriteAnimation.Speed(clip);
                cycle = StudioSpriteAnimation.CycleLength(clip);
                lead = StudioExtensionValues.NonNegative(clip.AnimationOffset) / speed;
                return cycle > 0;
            }
            if (clip.Kind == StudioKind.Audio && clip.Sound != null && clip.Sound.length > 0)
            {
                // offset = t + 원본 시작, 재생 위치 = offset % 음원 길이. 미리보기 배속(pitch)은 스토리 시간도 같이 바꾸므로
                // 타임라인 시간으로 본 한 바퀴는 음원 길이 그대로다.
                cycle = clip.Sound.length;
                lead = clip.AudioOffset;
                return true;
            }
            return false;
        }

        // 한 칸이 너무 좁으면 그 클립의 눈금은 생략.
        internal static bool ShowsTicks(double cycle, float zoom) => cycle * zoom >= StudioGUI.LoopTickMinCell;

        // 클립 안(0 초과, 길이 미만)이면서 보이는 구간 [from, to] 안의 눈금 시각을 into에 채운다.
        internal static void Ticks(StudioClip clip, double duration, float zoom, double from, double to, List<double> into)
        {
            into.Clear();
            if (!TryCycle(clip, out double cycle, out double lead) || !ShowsTicks(cycle, zoom))
                return;
            from = Math.Max(from, 0);
            to = Math.Min(to, duration);
            for (double k = Math.Floor((from + lead) / cycle); k * cycle - lead < to; k++)
            {
                double time = k * cycle - lead;
                if (time > 0 && time >= from && time < duration)
                    into.Add(time);
            }
        }

        // 입력 대기 중에만 그리는 표시. StudioClock이 대기 시간을 쌓는 때(IsHolding)와 같은 조건을 쓴다.
        internal static StudioWaitMark WaitMark(StudioEntry entry, StudioClock clock, bool playing, bool muted)
        {
            if (!playing || muted || clock == null || !clock.IsHolding || !entry.ContinuesDuringWait || !entry.Contains(clock.DisplayTime))
                return StudioWaitMark.None;
            var clip = entry.Asset;
            if (TryCycle(clip, out _, out _))
                return StudioWaitMark.Bar;
            if (clip.Kind == StudioKind.Effect)
                return StudioWaitMark.Dot;
            if (clip.Kind == StudioKind.Audio && !clip.Loop && clip.Sound != null && clip.Sound.length > 0)
            {
                double offset = Math.Max(0, clock.MotionTime(entry, clock.DisplayTime) - entry.Clip.start + clip.AudioOffset);
                return offset < clip.Sound.length ? StudioWaitMark.Dot : StudioWaitMark.None;
            }
            return StudioWaitMark.None;
        }

        // 실제 재생 위치(MotionTime)의 한 바퀴 위상 0~1. 0이 첫 프레임(사운드는 처음)으로 돌아가는 순간.
        internal static double Phase(StudioEntry entry, StudioClock clock)
        {
            if (!TryCycle(entry.Asset, out double cycle, out double lead))
                return 0;
            double motion = clock.MotionTime(entry, clock.DisplayTime) - entry.Clip.start;
            return Fraction((motion + lead) / cycle);
        }

        // 막대의 로컬 시각: 재생선이 있는 칸의 시작 + 위상 × 칸 폭. 클립 밖(잘린 첫 / 끝 칸)이면 그리지 않는다.
        internal static bool TryBarTime(StudioEntry entry, StudioClock clock, out double time)
        {
            time = 0;
            if (!TryCycle(entry.Asset, out double cycle, out double lead))
                return false;
            double playhead = clock.DisplayTime - entry.Clip.start;
            double cellStart = Math.Floor((playhead + lead) / cycle) * cycle - lead;
            time = cellStart + Phase(entry, clock) * cycle;
            return time >= 0 && time <= entry.Clip.duration;
        }

        private static double Fraction(double value) => value - Math.Floor(value);
    }
}
