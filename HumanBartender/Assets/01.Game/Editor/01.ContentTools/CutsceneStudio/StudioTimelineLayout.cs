using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal sealed class StudioTimelineRow
    {
        public int Group;
        public float Y, Height;
        public bool Header;
        public StudioTrack Track;
        public TimelineClip[] Clips;
    }

    internal static class StudioTimelineLayout
    {
        public const float RulerHeight = 28, GroupHeight = 28, TrackHeight = 56;
        public static int Group(StudioClip clip)
        {
            return Group(StudioKeyframes.IsCamera(clip) ? StudioKind.Camera : clip.Kind);
        }
        public static int Group(StudioKind kind)
        {
            return StudioClipSchema.Definition(kind).Group;
        }
        // 화면의 분류만 변경하고 원본 트랙 순서와 바인딩은 유지함.
        public static StudioTimelineRow[] Build(StudioSequence sequence, int collapsed, string search = null)
        {
            var groups = new List<StudioTimelineRow>[StudioClipSchema.Groups.Count];
            for (int i = 0; i < groups.Length; i++)
                groups[i] = new List<StudioTimelineRow>();
            string query = search?.Trim();
            bool filtering = !string.IsNullOrEmpty(query);
            foreach (var output in sequence.Timeline.GetOutputTracks())
            {
                if (output is not StudioTrack track)
                    continue;
                bool trackMatch = !filtering || Matches(track.name, query) || Matches(track.StudioDisplayName, query) || track.name == "카메라" && Matches("메인 카메라", query);
                var byGroup = new List<TimelineClip>[groups.Length];
                bool empty = true;
                foreach (var clip in track.GetClips())
                {
                    if (clip.asset is not StudioClip data)
                        continue;
                    empty = false;
                    if (!trackMatch && !MatchesClip(sequence, clip, data, query))
                        continue;
                    int group = Group(data);
                    byGroup[group] ??= new List<TimelineClip>();
                    byGroup[group].Add(clip);
                }

                if (empty && trackMatch)
                    byGroup[Group(track.StudioTrackKind >= 0 ? (StudioKind)track.StudioTrackKind : StudioKind.Camera)] = new List<TimelineClip>();
                for (int group = 0; group < groups.Length; group++)
                    if (byGroup[group] != null)
                        groups[group].Add(new StudioTimelineRow { Group = group, Track = track, Height = Mathf.Clamp(track.StudioRowHeight, TrackHeight, 240), Clips = byGroup[group].ToArray() });
            }

            var rows = new List<StudioTimelineRow>();
            float y = RulerHeight;
            for (int group = 0; group < groups.Length; group++)
            {
                var lanes = groups[group];
                if (filtering && lanes.Count == 0)
                    continue;
                var clips = new List<TimelineClip>();
                foreach (var lane in lanes)
                    clips.AddRange(lane.Clips);
                rows.Add(new StudioTimelineRow { Group = group, Y = y, Height = GroupHeight, Header = true, Clips = clips.ToArray() });
                y += GroupHeight;
                if (!filtering && (collapsed & (1 << group)) != 0)
                    continue;
                if (lanes.Count == 0)
                {
                    rows.Add(new StudioTimelineRow { Group = group, Y = y, Height = 30, Clips = Array.Empty<TimelineClip>() });
                    y += 30;
                }

                foreach (var lane in lanes)
                {
                    lane.Y = y;
                    rows.Add(lane);
                    y += lane.Height;
                }
            }

            return rows.ToArray();
        }

        private static bool Matches(string value, string query)
        {
            return value != null && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesClip(StudioSequence sequence, TimelineClip clip, StudioClip data, string query)
        {
            if (Matches(clip.displayName, query) || data.Sound != null && Matches(data.Sound.name, query))
                return true;
            foreach (var actor in sequence.Actors)
                if (actor != null && actor.Id == data.ActorId && Matches(actor.Name, query))
                    return true;
            return false;
        }

        public static StudioTrack TrackAt(StudioTimelineRow[] rows, float y)
        {
            foreach (var row in rows)
                if (y >= row.Y && y < row.Y + row.Height)
                    return row.Track;
            return null;
        }
    }
}
