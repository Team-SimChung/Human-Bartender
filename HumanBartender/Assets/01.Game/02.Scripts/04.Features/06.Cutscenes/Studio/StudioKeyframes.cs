using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanBartender.CutsceneStudio
{
    public enum StudioProperty
    {
        PositionX,
        PositionY,
        Scale,
        Rotation,
        Opacity,
        Strength,
        FlipX,
        Volume
    }

    [Serializable]
    public sealed class StudioFloatKey
    {
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Time")]
        private float time;
        public float Time
        {
            get { return time; }

            internal set { time = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Value")]
        private float value;
        public float Value
        {
            get { return value; }

            internal set { this.value = value; }
        }

        public StudioFloatKey(float time, float value)
        {
            Time = time;
            Value = value;
        }
    }

    [Serializable]
    public sealed class StudioPropertyKeys
    {
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Property")]
        private StudioProperty property;
        public StudioProperty Property
        {
            get { return property; }

            internal set { property = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Keys")]
        private StudioFloatKey[] keys = Array.Empty<StudioFloatKey>();
        internal StudioFloatKey[] Keys
        {
            get { return keys; }

            set { keys = value; }
        }
    }

    // 키 시간은 클립 기준 초 단위이며 미리보기와 게임이 같은 키를 평가함.
    public static class StudioKeyframes
    {
        public static bool Supports(StudioClip clip) => Properties(clip).Count > 0;
        private static readonly IReadOnlyList<StudioProperty> TransformProperties = Array.AsReadOnly(new[]
        {
            StudioProperty.PositionX,
            StudioProperty.PositionY,
            StudioProperty.Scale,
            StudioProperty.Rotation,
            StudioProperty.Opacity
        });
        private static readonly IReadOnlyList<StudioProperty> CameraProperties = Array.AsReadOnly(new[]
        {
            StudioProperty.PositionX,
            StudioProperty.PositionY,
            StudioProperty.Scale
        });
        private static readonly IReadOnlyList<StudioProperty> EffectProperties = Array.AsReadOnly(new[]
        {
            StudioProperty.Strength,
            StudioProperty.Opacity
        });
        private static readonly IReadOnlyList<StudioProperty> VisualProperties = Array.AsReadOnly(new[]
        {
            StudioProperty.PositionX, StudioProperty.PositionY, StudioProperty.Rotation,
            StudioProperty.FlipX, StudioProperty.Scale, StudioProperty.Opacity
        });

        private static readonly IReadOnlyList<StudioProperty> AudioProperties = Array.AsReadOnly(new[] { StudioProperty.Volume });
        // 속성 패널과 타임라인이 같은 편집 채널 목록을 사용함.
        public static IReadOnlyList<StudioProperty> EditingProperties(StudioClip clip) => Properties(clip);
        public static bool IsDiscrete(StudioProperty property) => property == StudioProperty.FlipX;

        public static IReadOnlyList<StudioProperty> Properties(StudioClip clip)
        {
            if (clip == null)
                return Array.Empty<StudioProperty>();
            return clip.Kind switch
            {
                StudioKind.Effect => EffectProperties,
                StudioKind.Camera => CameraProperties,
                StudioKind.Audio => AudioProperties,
                StudioKind.Visual => VisualProperties,
                StudioKind.Dialogue or StudioKind.Background => TransformProperties,
                _ => Array.Empty<StudioProperty>()
            };
        }

        public static bool IsCamera(StudioClip clip) => clip.Kind == StudioKind.Camera;
        public static StudioPropertyKeys Channel(StudioClip clip, StudioProperty property)
        {
            if (clip.PropertyKeys == null)
                return null;
            foreach (var channel in clip.PropertyKeys)
                if (channel != null && channel.Property == property)
                    return channel;
            return null;
        }

        // 빈 채널은 저장 의미를 유지하되 기본값 편집을 막지 않음.
        public static bool HasKeys(StudioClip clip, StudioProperty property)
        {
            var keys = Channel(clip, property)?.Keys;
            if (keys != null)
                foreach (var key in keys)
                    if (key != null) return true;
            return false;
        }

        public static bool HasOverrides(StudioClip clip)
        {
            if (clip.PropertyKeys == null)
                return false;
            foreach (var channel in clip.PropertyKeys)
                if (channel != null)
                    return true;
            return false;
        }

        public static float Limit(StudioProperty property, float value) => IsDiscrete(property) ? (value >= .5f ? 1 : 0) : property == StudioProperty.Scale ? Mathf.Max(.01f, value) : property is StudioProperty.Opacity or StudioProperty.Strength or StudioProperty.Volume ? Mathf.Clamp01(value) : value;
        // 기본 키를 가상으로 만들어 창 열기만으로 에셋이 변경되지 않게 하고 기존 보간을 유지함.
        public static StudioFloatKey[] GetKeys(StudioClip clip, StudioProperty property, double duration)
        {
            var channel = Channel(clip, property);
            if (channel != null)
                return channel.Keys ?? Array.Empty<StudioFloatKey>();
            return new[]
            {
                new StudioFloatKey(0, Endpoint(clip, property, false)),
                new StudioFloatKey((float)duration, Endpoint(clip, property, true))
            };
        }

        private static float Endpoint(StudioClip c, StudioProperty p, bool end) => p switch
        {
            StudioProperty.PositionX => end ? c.To.x : c.From.x,
            StudioProperty.PositionY => end ? c.To.y : c.From.y,
            StudioProperty.Scale => end ? c.ScaleTo : c.ScaleFrom,
            StudioProperty.Rotation => end ? c.RotationTo : c.RotationFrom,
            StudioProperty.Strength => c.Strength,
            StudioProperty.Volume => c.Volume,
            StudioProperty.FlipX => c.FlipX ? 1 : 0,
            StudioProperty.Opacity when c.Kind == StudioKind.Effect => c.Effect == StudioEffect.FadeOut ? (end ? 1 : 0) : c.Effect == StudioEffect.Tint ? 1 : (end ? 0 : 1),
            _ => end ? c.OpacityTo : c.OpacityFrom
        };
        public static float Evaluate(StudioClip clip, StudioProperty property, float age, double duration)
        {
            age = Mathf.Clamp(age, 0, (float)duration);
            var channel = Channel(clip, property);
            if (IsDiscrete(property))
                return Limit(property, ReadSamples(channel, age).HeldValue(Endpoint(clip, property, false)));
            if (channel == null)
            {
                float progress = Mathf.Clamp01(age / Mathf.Max(.001f, (float)duration));
                if (clip.Curve != null)
                    progress = clip.Curve.Evaluate(progress);
                return Limit(property, Mathf.LerpUnclamped(Endpoint(clip, property, false), Endpoint(clip, property, true), progress));
            }

            return Limit(property, ReadSamples(channel, age).Evaluate(age, clip.Curve, Endpoint(clip, property, false)));
        }

        private static KeySamples ReadSamples(StudioPropertyKeys channel, float age)
        {
            var samples = new KeySamples();
            if (channel?.Keys != null)
            {
                foreach (var key in channel.Keys)
                    if (key != null)
                        samples.Add(key.Time, key.Value, age);
            }

            return samples;
        }

        // 모든 속성 채널이 같은 경계 선택 / 보간 규칙을 사용함.
        private struct KeySamples
        {
            private bool hasBefore, hasAfter;
            private float beforeTime, afterTime, beforeValue, afterValue;

            internal float HeldValue(float fallback) => hasBefore ? beforeValue : fallback;

            internal void Add(float time, float value, float age)
            {
                if (time <= age && (!hasBefore || time >= beforeTime))
                {
                    hasBefore = true;
                    beforeTime = time;
                    beforeValue = value;
                }
                else if (time > age && (!hasAfter || time < afterTime))
                {
                    hasAfter = true;
                    afterTime = time;
                    afterValue = value;
                }
            }

            internal float Evaluate(float age, AnimationCurve curve, float fallback)
            {
                if (!hasBefore)
                    return hasAfter ? afterValue : fallback;
                if (!hasAfter)
                    return beforeValue;
                float progress = Mathf.InverseLerp(beforeTime, afterTime, age);
                if (curve != null)
                    progress = curve.Evaluate(progress);
                return Mathf.LerpUnclamped(beforeValue, afterValue, progress);
            }
        }

        internal static StudioPose Sample(StudioClip clip, float age, double duration) => new StudioPose
        {
            Position = new Vector2(Evaluate(clip, StudioProperty.PositionX, age, duration), Evaluate(clip, StudioProperty.PositionY, age, duration)),
            Scale = Evaluate(clip, StudioProperty.Scale, age, duration),
            Rotation = Evaluate(clip, StudioProperty.Rotation, age, duration),
            Opacity = Evaluate(clip, StudioProperty.Opacity, age, duration)
        };
    }

    // 현재 시점 평가 결과. 저장되는 키 데이터는 StudioPropertyKeys에서만 관리함.
    internal struct StudioPose
    {
        internal Vector2 Position;
        internal float Scale, Rotation, Opacity;
    }
}
