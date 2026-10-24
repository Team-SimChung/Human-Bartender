using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 미기록 입력과 자동 키 설정은 에디터 상태이며, 확정값은 기존 PropertyKeys만 사용함.
    internal sealed class StudioCurrentKeyEditing
    {
        private StudioClip owner;
        private string targetId;
        private StudioKind? ownerKind;
        private StudioEffect? effectKind;
        private bool? stateCamera;
        private double sequenceTime = double.NaN;
        private readonly Dictionary<StudioProperty, float> pending = new();
        private readonly Dictionary<StudioProperty, float> sampled = new();
        private readonly HashSet<StudioProperty> armed = new();

        internal void Observe(TimelineClip clip, double time)
        {
            var asset = clip?.asset as StudioClip;
            StudioEffect? nextEffect = asset != null && asset.Kind == StudioKind.Effect ? asset.Effect : null;
            bool? nextStateCamera = asset != null && asset.Kind == StudioKind.State ? asset.StateCamera : null;
            if (owner != asset || targetId != asset?.ActorId || ownerKind != asset?.Kind || effectKind != nextEffect || stateCamera != nextStateCamera)
            {
                owner = asset;
                targetId = asset?.ActorId;
                ownerKind = asset?.Kind;
                effectKind = nextEffect;
                stateCamera = nextStateCamera;
                armed.Clear();
                sampled.Clear();
                pending.Clear();
            }
            if (sequenceTime != time)
                pending.Clear();
            sequenceTime = time;
            if (asset == null || !StudioPropertyEditing.IsFinite(time))
                return;
            foreach (var property in StudioKeyframes.Properties(asset))
            {
                float value = StudioKeyframes.Evaluate(asset, property, (float)(time - clip.start), clip.duration);
                if (sampled.TryGetValue(property, out float previous) && previous != value)
                    pending.Remove(property);
                sampled[property] = value;
            }
        }

        internal float Value(StudioProperty property) => pending.TryGetValue(property, out float value) ? value : sampled[property];
        internal bool HasPending(StudioProperty property) => pending.ContainsKey(property);
        internal bool IsArmed(StudioProperty property) => armed.Contains(property);
        internal void Arm(StudioProperty property, bool enabled)
        {
            if (enabled) armed.Add(property);
            else armed.Remove(property);
        }
        internal bool SetPending(StudioProperty property, float value)
        {
            if (!StudioPropertyEditing.IsFinite(value) || !sampled.ContainsKey(property)) return false;
            pending[property] = StudioKeyframes.Limit(property, value);
            return true;
        }
        internal void Recorded(IEnumerable<StudioProperty> properties)
        {
            foreach (var property in properties) pending.Remove(property);
        }
        internal void ClearPending() => pending.Clear();
        internal void Reset()
        {
            owner = null;
            targetId = null;
            ownerKind = null;
            effectKind = null;
            stateCamera = null;
            sequenceTime = double.NaN;
            pending.Clear(); sampled.Clear(); armed.Clear();
        }

        internal static bool TryLocalTime(TimelineClip clip, double sequenceTime, out float local)
        {
            local = 0;
            if (clip == null || !StudioPropertyEditing.IsFinite(sequenceTime)) return false;
            double age = sequenceTime - clip.start;
            if (age < -.000001 || age > clip.duration + .000001) return false;
            local = (float)Math.Max(0, Math.Min(clip.duration, age));
            return true;
        }

        internal static bool HasKey(TimelineClip clip, StudioProperty property, float local)
        {
            foreach (var key in StudioKeyframes.GetKeys((StudioClip)clip.asset, property, clip.duration))
                if (key != null && Math.Abs(key.Time - local) < .0001f) return true;
            return false;
        }

        internal static double? AdjacentTime(TimelineClip clip, IReadOnlyList<StudioProperty> properties, double time, bool next)
        {
            double? result = null;
            foreach (var property in properties)
                foreach (var key in StudioKeyframes.GetKeys((StudioClip)clip.asset, property, clip.duration))
                {
                    if (key == null) continue;
                    double at = clip.start + key.Time;
                    if (next ? at <= time + .000001 : at >= time - .000001) continue;
                    if (!result.HasValue || (next ? at < result.Value : at > result.Value)) result = at;
                }
            return result;
        }

        internal static bool Write(TimelineClip clip, float local, IReadOnlyDictionary<StudioProperty, float> values, string label)
        {
            if (clip?.asset is not StudioClip asset || values.Count == 0 || !StudioPropertyEditing.IsFinite(local) || local < 0 || local > clip.duration + .000001) return false;
            foreach (var pair in values)
            {
                bool supported = false;
                foreach (var property in StudioKeyframes.Properties(asset)) supported |= property == pair.Key;
                if (!supported || !StudioPropertyEditing.IsFinite(pair.Value)) return false;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);
            Undo.RegisterCompleteObjectUndo(asset, label);
            foreach (var pair in values)
                StudioKeyframeEditing.Set(clip, pair.Key, -1, local, pair.Value, false);
            Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
            return true;
        }
    }
}
