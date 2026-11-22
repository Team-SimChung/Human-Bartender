using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 트랙 순서 편집과 Undo를 담당하며 입력 좌표나 패널 상태에는 의존하지 않음.
    internal static class StudioTrackOrdering
    {
        internal static bool Move(StudioSequence sequence, int group, StudioTrack source, StudioTrack target, bool after)
        {
            if (sequence == null || sequence.Timeline == null || source == null || target == null || source == target ||
                group < 0 || group >= StudioClipSchema.Groups.Count)
                return false;

            // 검색으로 가려진 트랙도 포함해 순서를 갱신함.
            var tracks = StudioTimelineLayout.Build(sequence, 0)
                .Where(row => row.Group == group && row.Track != null).Select(row => row.Track).ToList();
            if (!tracks.Contains(source) || !tracks.Contains(target))
                return false;
            var before = tracks.ToArray();
            tracks.Remove(source);
            tracks.Insert(tracks.IndexOf(target) + (after ? 1 : 0), source);
            if (tracks.SequenceEqual(before))
                return false;

            const string label = "트랙 순서 변경";
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(label);
            Undo.RegisterCompleteObjectUndo(tracks.Cast<Object>().ToArray(), label);
            var category = StudioClipSchema.Groups[group].Kind;
            for (int i = 0; i < tracks.Count; i++)
            {
                tracks[i].SetDisplayOrder(category, i);
                EditorUtility.SetDirty(tracks[i]);
            }
            StudioEditorAssets.Dirty(sequence);
            return true;
        }
    }
}
