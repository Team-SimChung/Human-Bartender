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

        internal static float Endpoint(TimelineClip clip, StudioProperty property, bool end)
        {
            var asset = (StudioClip)clip.asset;
            if (StudioKeyframes.Channel(asset, property) != null || asset.Kind == StudioKind.State)
            {
                float time = end ? (float)clip.duration : 0;
                foreach (var key in StudioKeyframes.GetKeys(asset, property, clip.duration))
                    if (key != null && Mathf.Abs(key.Time - time) < .0001f)
                        return key.Value;
                return StudioKeyframes.Evaluate(asset, property, time, clip.duration);
            }
            var keys = StudioKeyframes.GetKeys(asset, property, clip.duration);
            return keys[end ? keys.Length - 1 : 0].Value;
        }

        internal static bool SetEndpoint(TimelineClip clip, StudioProperty property, bool end, float value)
        {
            if (!IsFinite(value))
                return false;
            var asset = (StudioClip)clip.asset;
            value = StudioKeyframes.Limit(property, value);
            if (StudioKeyframes.Channel(asset, property) != null || asset.Kind == StudioKind.State || asset.Kind == StudioKind.Effect)
            {
                // 경계의 키만 추가·갱신하고 중간 키와 다른 채널을 유지함.
                StudioKeyframeEditing.Set(clip, property, -1, end ? (float)clip.duration : 0, value);
                return true;
            }

            using var data = new SerializedObject(asset);
            var field = StudioSerializedFields.Find(data, EndpointPath(property, end));
            field.floatValue = value;
            return data.ApplyModifiedProperties();
        }

        private static string EndpointPath(StudioProperty property, bool end) => property switch
        {
            StudioProperty.PositionX => end ? "To.x" : "From.x",
            StudioProperty.PositionY => end ? "To.y" : "From.y",
            StudioProperty.Scale => end ? "ScaleTo" : "ScaleFrom",
            StudioProperty.Rotation => end ? "RotationTo" : "RotationFrom",
            StudioProperty.Opacity => end ? "OpacityTo" : "OpacityFrom",
            StudioProperty.Strength => "Strength",
            _ => throw new ArgumentOutOfRangeException(nameof(property))
        };

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
