using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 시퀀스와 선택 대상의 기준을 한곳에서 관리하며 화면별 상태는 보관하지 않음.
    [Serializable]
    internal sealed class StudioEditSession
    {
        [SerializeField] private StudioSequence sequence;
        [SerializeField] private StudioClip selected;
        [NonSerialized] private StudioClip keyOwner;
        [NonSerialized] private StudioProperty keyProperty;
        [NonSerialized] private int keyIndex = -1;
        [NonSerialized] private readonly Dictionary<int, Hash128> undoState = new();
        [NonSerialized] private bool capturingUndoState;

        internal StudioSequence Sequence => sequence;
        internal StudioClip Selected => selected;
        internal StudioClip KeyOwner => keyOwner;
        internal StudioProperty KeyProperty => keyProperty;
        internal int KeyIndex => keyIndex;
        internal bool CameraSelected => selected != null && StudioKeyframes.IsCamera(selected);
        internal int ActorIndex
        {
            get
            {
                if (sequence == null || selected == null || CameraSelected)
                    return -1;
                for (int i = 0; i < sequence.Actors.Count; i++)
                    if (sequence.Actors[i].Id == selected.ActorId)
                        return i;
                return -1;
            }
        }

        internal void Load(StudioSequence value)
        {
            sequence = value;
            ClearSelection();
            CaptureUndoState();
        }

        internal void Select(StudioClip value)
        {
            selected = value;
            ClearKey();
            ValidateSelection();
        }

        internal void SelectKey(StudioClip owner, StudioProperty property, int index)
        {
            Select(owner);
            if (selected == null)
                return;
            keyOwner = owner;
            keyProperty = property;
            keyIndex = index;
        }

        internal void UpdateKeyIndex(int index) { keyIndex = index; }
        internal void ClearKey() { keyOwner = null; keyIndex = -1; }
        internal void ClearSelection() { selected = null; ClearKey(); }

        internal TimelineClip FindSelected()
        {
            foreach (var entry in StudioEvaluation.Clips(sequence, true))
                if (entry.Asset == selected)
                    return entry.Clip;
            return null;
        }

        internal void ValidateSelection()
        {
            if (selected == null || FindSelected() == null)
                ClearSelection();
        }

        internal bool TracksUndoTarget(int instanceId)
        {
            return undoState.ContainsKey(instanceId);
        }

        // 편집이 반영된 시점의 직렬화 데이터를 비교 기준으로 보관함.
        internal void CaptureUndoState()
        {
            if (capturingUndoState)
                return;
            capturingUndoState = true;
            try
            {
                undoState.Clear();
                foreach (var target in UndoTargets())
                    undoState[target.GetInstanceID()] = ContentHash(target);
            }
            finally { capturingUndoState = false; }
        }

        internal bool HasUndoChanges()
        {
            var targets = new HashSet<int>();
            foreach (var target in UndoTargets())
            {
                int id = target.GetInstanceID();
                if (!targets.Add(id))
                    continue;
                if (!undoState.TryGetValue(id, out var previous) || previous != ContentHash(target))
                    return true;
            }
            return targets.Count != undoState.Count;
        }

        private IEnumerable<UnityEngine.Object> UndoTargets()
        {
            if (sequence == null)
                yield break;
            yield return sequence;
            if (sequence.Timeline == null)
                yield break;
            yield return sequence.Timeline;
            foreach (var track in sequence.Timeline.GetRootTracks())
                foreach (var target in TrackUndoTargets(track))
                    yield return target;
        }

        private static IEnumerable<UnityEngine.Object> TrackUndoTargets(TrackAsset track)
        {
            if (track == null)
                yield break;
            yield return track;
            if (track.curves != null)
                yield return track.curves;
            foreach (var clip in track.GetClips())
            {
                if (clip.asset != null)
                    yield return clip.asset;
                if (clip.curves != null)
                    yield return clip.curves;
            }
            foreach (var child in track.GetChildTracks())
                foreach (var target in TrackUndoTargets(child))
                    yield return target;
        }

        private static Hash128 ContentHash(UnityEngine.Object target)
        {
            using var serialized = new SerializedObject(target);
            using var property = serialized.GetIterator();
            var content = new StringBuilder();
            // 숨겨진 트랙 목록과 클립 시간도 포함하며 참조된 에셋은 별도로 비교함.
            bool enter = true;
            while (property.Next(enter))
            {
                enter = false;
                content.Append(property.propertyPath).Append(':').Append(property.contentHash).Append(';');
            }
            return Hash128.Compute(content.ToString());
        }
    }
}
