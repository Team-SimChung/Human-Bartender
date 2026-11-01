using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.Timeline;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 한 번의 드래그 기준값과 Undo 범위만 관리함. 기록 정책은 공통 편집 상태에 위임함.
    internal sealed class StudioPreviewKeyGesture : IDisposable
    {
        private readonly StudioCurrentKeyEditing editing;
        private readonly TimelineClip clip;
        private readonly Func<double, double> snap;
        private readonly Dictionary<StudioProperty, float> initial = new();
        private readonly Dictionary<StudioProperty, float> pending;
        private readonly int undoGroup;
        private double time;
        private bool recorded;
        private bool finished;

        internal StudioPreviewKeyGesture(StudioCurrentKeyEditing editing, TimelineClip clip, double time, Func<double, double> snap)
        {
            this.editing = editing; this.clip = clip; this.time = time; this.snap = snap;
            editing.Observe(clip, time);
            pending = editing.CopyPending();
            foreach (var property in StudioKeyframes.Properties((StudioClip)clip.asset)) initial.Add(property, editing.Value(property));
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
        }

        internal float? Apply(StudioProperty property, float delta, bool multiply = false)
        {
            if (finished || !initial.TryGetValue(property, out float value)) return null;
            var at = editing.Edit(clip, time, new Dictionary<StudioProperty, float>
                { [property] = multiply ? value * delta : value + delta }, snap);
            recorded |= at.HasValue;
            if (at.HasValue) time = clip.start + at.Value;
            return at;
        }

        internal bool Finish(bool cancel)
        {
            if (finished) return recorded;
            finished = true;
            if (cancel)
            {
                if (recorded) Undo.RevertAllDownToGroup(undoGroup);
                editing.Observe(clip, time);
                editing.RestorePending(pending);
            }
            else Undo.CollapseUndoOperations(undoGroup);
            Undo.IncrementCurrentGroup();
            return recorded;
        }

        public void Dispose() { if (!finished) Finish(true); }
    }
}
