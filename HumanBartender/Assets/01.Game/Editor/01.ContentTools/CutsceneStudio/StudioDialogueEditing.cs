using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 편집 → 전체 이동 계획 → 잠금 검사 → 일괄 반영을 하나의 Undo로 묶는다.
    internal static class StudioDialogueEditing
    {
        private const double Epsilon = 1e-8;
        private sealed class Placement
        {
            internal TimelineClip Clip;
            internal double Start, Duration;
            internal bool Changed => Math.Abs(Start - Clip.start) > Epsilon || Math.Abs(Duration - Clip.duration) > Epsilon;
        }

        internal static bool Edit(StudioSequence sequence, StudioClip edited, Action mutation, string label,
            out string message, bool fitOnly = false)
        {
            message = "";
            if (sequence == null || sequence.Timeline == null) return false;
            var placements = sequence.Timeline.GetOutputTracks().SelectMany(t => t.GetClips())
                .Select(c => new Placement { Clip = c, Start = c.start, Duration = c.duration }).OrderBy(p => p.Start).ToList();
            var target = placements.FirstOrDefault(p => p.Clip.asset == edited);
            if (edited != null && (target == null || target.Clip.GetParentTrack().lockedInHierarchy))
            {
                message = "[CutsceneStudio] 대사 트랙이 잠겨 있거나 현재 제작 파일에 속하지 않아 변경하지 않았습니다.";
                return false;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);
            var data = edited != null ? (UnityEngine.Object)edited : sequence;
            Undo.RegisterCompleteObjectUndo(data, label);
            try
            {
                mutation?.Invoke();
                var notes = new HashSet<string>();
                double length = sequence.Duration;
                foreach (var item in placements)
                {
                    if (item.Clip.asset is not StudioClip clip || clip.Kind != StudioKind.Dialogue ||
                        edited != null && clip != edited || !fitOnly && clip.DialogueLengthMode == StudioDialogueLengthMode.Manual) continue;
                    double duration = Math.Max(StudioTiming.FrameDuration(sequence), StudioDialogueTiming.For(clip, sequence).Duration);
                    if (!double.IsFinite(duration)) throw new InvalidOperationException("[CutsceneStudio] 대사 길이를 계산할 수 없습니다.");
                    double delta = duration - item.Duration;
                    if (Math.Abs(delta) <= Epsilon) continue;
                    double boundary = item.Start + item.Duration;
                    item.Duration = duration;
                    if (clip.PropertyKeys != null && clip.PropertyKeys.Any(c => c?.Keys?.Any(k => k != null && k.Time > duration + Epsilon) == true))
                        notes.Add($"'{item.Clip.displayName}'의 길이 밖 키프레임은 삭제하지 않았습니다. 위치를 확인하세요.");
                    if (fitOnly || clip.DialogueLengthMode != StudioDialogueLengthMode.Ripple) continue;
                    foreach (var other in placements)
                    {
                        if (other == item) continue;
                        if (other.Start >= boundary - Epsilon) other.Start += delta;
                        else if (other.Start + other.Duration > boundary + Epsilon)
                            notes.Add($"'{other.Clip.displayName}'은 변경 지점을 가로지르므로 길이·시작을 유지했습니다.");
                    }
                    length += delta;
                }

                var changed = placements.Where(p => p.Changed).ToList();
                var locked = changed.FirstOrDefault(p => p.Clip.GetParentTrack().lockedInHierarchy);
                if (locked != null)
                {
                    Undo.RevertAllDownToGroup(group);
                    message = $"[CutsceneStudio] '{locked.Clip.GetParentTrack().name}' 트랙이 잠겨 있어 본문·설정·시간 변경을 모두 취소했습니다.";
                    return false;
                }
                if (changed.Count > 0)
                {
                    var tracks = changed.Select(p => p.Clip.GetParentTrack()).Distinct().ToArray();
                    Undo.RegisterCompleteObjectUndo(tracks, label);
                    Undo.RegisterCompleteObjectUndo(sequence.Timeline, label);
                    foreach (var p in changed) { p.Clip.start = p.Start; p.Clip.duration = p.Duration; }
                    sequence.Timeline.fixedDuration = Math.Max(length, placements.Max(p => p.Start + p.Duration));
                    foreach (var track in tracks) EditorUtility.SetDirty(track);
                    EditorUtility.SetDirty(sequence.Timeline);
                }
                EditorUtility.SetDirty(data);
                Undo.CollapseUndoOperations(group);
                message = string.Join("\n", notes);
                return true;
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }
    }
}
