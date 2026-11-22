using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 반복 클립의 한 바퀴 눈금과, 입력 대기 중 계속 재생되는 클립의 주황 막대 / 점. 계산은 StudioLoopMarks에 맡김.
    internal sealed partial class StudioTimelinePanel
    {
        [NonSerialized] private readonly List<double> loopTicks = new();

        // 클립 배경 바로 위(썸네일 / 글자 아래)에 그림. 보이는 구간 밖의 눈금은 계산하지 않음.
        private void DrawLoopTicks(Rect rect, TimelineClip clip, StudioClip data, bool muted, float visibleLeft, float visibleRight)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            StudioLoopMarks.Ticks(data, clip.duration, zoom, (visibleLeft - rect.x) / zoom, (visibleRight - rect.x) / zoom, loopTicks);
            if (loopTicks.Count == 0)
                return;
            var color = StudioGUI.LoopTickColor;
            if (muted)
                color.a *= StudioGUI.MutedAlpha;
            foreach (double time in loopTicks)
                EditorGUI.DrawRect(new Rect(rect.x + (float)time * zoom, rect.y, StudioGUI.LoopTickWidth, rect.height), color);
        }

        // 입력 대기로 재생선이 멈춘 동안에만 그림. 재생선(파란 선)은 이후에 그려 막대보다 위에 보임.
        private void DrawWaitMark(Rect rect, TimelineClip clip, StudioClip data, bool muted)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var entry = new StudioEntry(clip, data);
            var mark = StudioLoopMarks.WaitMark(entry, host.Clock, host.Playing, muted);
            if (mark == StudioWaitMark.Bar && StudioLoopMarks.TryBarTime(entry, host.Clock, out double time))
            {
                float x = Mathf.Min(rect.x + (float)time * zoom, rect.xMax - StudioGUI.WaitBarWidth);
                EditorGUI.DrawRect(new Rect(x, rect.y, StudioGUI.WaitBarWidth, rect.height), StudioGUI.WaitMarkColor);
            }
            else if (mark == StudioWaitMark.Dot)
            {
                // 재생선(타임라인이 Clock.Time으로 그림) 바로 오른쪽, 클립 아래 가장자리. 클립 밖으로 나가지 않게 안쪽으로 붙임.
                float playhead = TrackHeaderWidth + (float)host.Clock.Time * zoom + 2;
                float x = Mathf.Clamp(playhead, rect.x, rect.xMax - StudioGUI.WaitDotSize);
                EditorGUI.DrawRect(new Rect(x, rect.yMax - StudioGUI.WaitDotSize, StudioGUI.WaitDotSize, StudioGUI.WaitDotSize), StudioGUI.WaitMarkColor);
            }
        }
    }
}
