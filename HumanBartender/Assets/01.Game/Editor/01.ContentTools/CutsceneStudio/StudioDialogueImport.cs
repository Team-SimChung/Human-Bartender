using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.Timeline;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 대사 입력 해석과 타임라인 추가를 창의 표시 상태와 분리함.
    internal static class StudioDialogueImport
    {
        internal static bool TryParse(StudioSequence sequence, string input, out string[] texts, out string[] speakerIds, out string error)
        {
            var parsedTexts = new List<string>();
            var parsedIds = new List<string>();
            texts = speakerIds = Array.Empty<string>();
            error = "";
            if (sequence == null)
                return false;
            string[] lines = (input ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                    continue;
                int separator = line.IndexOf(':');
                if (separator < 1 || string.IsNullOrWhiteSpace(line.Substring(separator + 1)))
                    return false;
                string speaker = line.Substring(0, separator).Trim();
                StudioActor actor = sequence.FindActor(speaker);
                if (actor == null && !TryFindSpeaker(sequence, speaker, out actor))
                {
                    error = (i + 1) + "번째 줄: 같은 이름의 화자가 여러 명입니다. 화자 ID를 입력하세요.";
                    return false;
                }

                if (actor == null && speaker != "내레이션")
                {
                    error = (i + 1) + "번째 줄: ‘" + speaker + "’ 화자를 찾을 수 없습니다. 등록된 이름 또는 ID를 입력하세요.";
                    return false;
                }

                parsedIds.Add(actor?.Id ?? "");
                parsedTexts.Add(line.Substring(separator + 1).Trim());
            }

            texts = parsedTexts.ToArray();
            speakerIds = parsedIds.ToArray();
            return texts.Length > 0;
        }

        private static bool TryFindSpeaker(StudioSequence sequence, string name, out StudioActor match)
        {
            match = null;
            foreach (var actor in sequence.Actors)
            {
                if (actor == null || actor.Name != name)
                    continue;
                if (match != null)
                    return false;
                match = actor;
            }

            return true;
        }

        internal static TimelineClip Append(StudioSequence sequence, string[] texts, string[] speakerIds, double duration, double currentTime)
        {
            if (sequence == null || sequence.Timeline == null || !double.IsFinite(duration) || duration <= 0)
                return null;
            if (texts == null || speakerIds == null || texts.Length != speakerIds.Length)
                return null;
            var entries = StudioEvaluation.Clips(sequence, true);
            double at = currentTime;
            StudioTrack track = null;
            foreach (var entry in entries)
            {
                if (entry.Asset.Kind != StudioKind.Dialogue)
                    continue;
                at = Math.Max(at, entry.Clip.end);
                track = entry.Clip.GetParentTrack() as StudioTrack;
            }

            double originalDuration = sequence.Duration;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("대사 추가");
            TimelineClip first = null;
            try
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(texts[i]))
                        continue;
                    var clip = StudioEditorAssets.AddClip(sequence, StudioKind.Dialogue, at, speakerIds[i], track);
                    first ??= clip;
                    track = (StudioTrack)clip.GetParentTrack();
                    ((StudioClip)clip.asset).Text = texts[i].Trim();
                    clip.duration = Math.Max(StudioTiming.FrameDuration(sequence), duration);
                    at = clip.end;
                }

                if (first != null)
                    sequence.Timeline.fixedDuration = Math.Max(originalDuration, at);
                return first;
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }
        }
    }
}
