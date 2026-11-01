using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 속성 입력을 기존 직렬화 데이터와 키 편집 명령에 반영함. 별도 상태를 저장하지 않음.
    internal static class StudioPropertyEditing
    {
        internal static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        internal static bool SetTiming(StudioSequence sequence, TimelineClip clip, double start, double duration)
        {
            if (!IsFinite(start) || !IsFinite(duration))
                return false;
            start = Math.Max(0, start);
            duration = Math.Max(StudioTiming.FrameDuration(sequence), duration);
            if (!IsFinite(start + duration))
                return false;
            Undo.RecordObject(clip.GetParentTrack(), "클립 시간 변경");
            Undo.RecordObject(sequence.Timeline, "컷씬 길이 변경");
            clip.start = start;
            clip.duration = duration;
            sequence.Timeline.fixedDuration = Math.Max(sequence.Duration, clip.end);
            return true;
        }

        internal static bool UsesSourceTiming(StudioClip clip) => clip.Frames != null
            && clip.FrameTimes != null && clip.Frames.Length == clip.FrameTimes.Length && clip.AnimationLength > 0;

        internal static void ValidateFrameTiming(StudioClip clip)
        {
            if (clip.FrameTimes == null || UsesSourceTiming(clip))
                return;
            Undo.RecordObject(clip, "애니메이션 프레임 변경");
            clip.FrameTimes = null;
            clip.AnimationLength = 0;
            EditorUtility.SetDirty(clip);
        }

        internal static void SetSprite(StudioClip clip, Sprite sprite)
        {
            Undo.RecordObject(clip, "클립 이미지 변경");
            clip.Frames = sprite == null ? Array.Empty<Sprite>() : new[] { sprite };
            clip.FrameTimes = null;
            clip.AnimationLength = 0;
            EditorUtility.SetDirty(clip);
        }
    }
}
