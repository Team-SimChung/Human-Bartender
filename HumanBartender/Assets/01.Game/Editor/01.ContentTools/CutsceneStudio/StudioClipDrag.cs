using System;
using UnityEditor;
using UnityEngine.Timeline;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 선택과 실제 편집을 구분하고 한 번의 드래그를 한 번의 실행 취소로 묶음.
    internal sealed class StudioClipDrag
    {
        private readonly StudioSequence sequence;
        private readonly TimelineClip clip;
        private readonly int edge;
        private readonly double start, duration;
        private double pixels;
        private int undoGroup;
        private bool changed, completed;

        internal StudioClipDrag(StudioSequence sequence, TimelineClip clip, int edge)
        {
            this.sequence = sequence;
            this.clip = clip;
            this.edge = edge;
            start = clip.start;
            duration = clip.duration;
        }

        internal bool Move(float pixelDelta, float pixelsPerSecond, Func<double, double> snapTime)
        {
            if (completed || pixelsPerSecond <= 0)
                return false;
            pixels += pixelDelta;
            double delta = pixels / pixelsPerSecond;
            double nextStart = start, nextDuration = duration;
            double frame = StudioTiming.FrameDuration(sequence);
            switch (edge)
            {
                case 1:
                    nextDuration = Math.Max(frame, snapTime(duration + delta));
                    break;
                case -1:
                    nextStart = Math.Max(0, Math.Min(start + duration - frame, snapTime(start + delta)));
                    nextDuration = start + duration - nextStart;
                    break;
                default:
                    nextStart = Math.Max(0, snapTime(start + delta));
                    break;
            }

            if (nextStart == clip.start && nextDuration == clip.duration)
                return false;
            if (!changed)
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("클립 이동 / 길이 변경");
                Undo.RegisterCompleteObjectUndo(clip.GetParentTrack(), "클립 이동 / 길이 변경");
                Undo.RegisterCompleteObjectUndo(sequence.Timeline, "전체 길이 변경");
                changed = true;
            }

            clip.start = nextStart;
            clip.duration = nextDuration;
            return true;
        }

        internal bool Complete()
        {
            if (completed)
                return false;
            completed = true;
            if (!changed)
                return false;
            sequence.Timeline.fixedDuration = Math.Max(sequence.Duration, clip.end);
            Undo.CollapseUndoOperations(undoGroup);
            return true;
        }
    }
}
