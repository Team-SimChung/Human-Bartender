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
        Strength
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
        public static IReadOnlyList<StudioProperty> Properties(StudioClip clip)
        {
            if (clip == null)
                return Array.Empty<StudioProperty>();
            return clip.Kind switch
            {
                StudioKind.Effect => EffectProperties,
                StudioKind.Camera => CameraProperties,
                StudioKind.State when clip.StateCamera => CameraProperties,
                StudioKind.Actor or StudioKind.Visual or StudioKind.State or StudioKind.Dialogue or StudioKind.Background => TransformProperties,
                _ => Array.Empty<StudioProperty>()
            };
        }

        public static bool IsCamera(StudioClip clip) => clip.Kind == StudioKind.Camera || clip.Kind == StudioKind.State && clip.StateCamera;
        public static StudioPropertyKeys Channel(StudioClip clip, StudioProperty property)
        {
            if (clip.PropertyKeys == null)
                return null;
            foreach (var channel in clip.PropertyKeys)
                if (channel != null && channel.Property == property)
                    return channel;
            return null;
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

        public static float Value(StudioPoseKey pose, StudioProperty property) => property switch
        {
            StudioProperty.PositionX => pose.Position.x,
            StudioProperty.PositionY => pose.Position.y,
            StudioProperty.Scale => pose.Scale,
            StudioProperty.Rotation => pose.Rotation,
            _ => pose.Opacity
        };
        public static float Limit(StudioProperty property, float value) => property == StudioProperty.Scale ? Mathf.Max(.01f, value) : property == StudioProperty.Opacity || property == StudioProperty.Strength ? Mathf.Clamp01(value) : value;
        // 기본 키를 가상으로 만들어 창 열기만으로 에셋이 변경되지 않게 하고 기존 보간을 유지함.
        public static StudioFloatKey[] GetKeys(StudioClip clip, StudioProperty property, double duration)
        {
            var channel = Channel(clip, property);
            if (channel != null)
                return channel.Keys ?? Array.Empty<StudioFloatKey>();
            if (clip.Kind == StudioKind.State)
            {
                var result = new List<StudioFloatKey>();
                if (clip.Keys != null)
                    foreach (var key in clip.Keys)
                    {
                        if (key == null)
                            continue;
                        int index = result.Count;
                        while (index > 0 && result[index - 1].Time > key.Time)
                            index--;
                        result.Insert(index, new StudioFloatKey(key.Time, Value(key, property)));
                    }

                return result.ToArray();
            }
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
            StudioProperty.Opacity when c.Kind == StudioKind.Effect => c.Effect == StudioEffect.FadeOut ? (end ? 1 : 0) : c.Effect == StudioEffect.Tint ? 1 : (end ? 0 : 1),
            _ => end ? c.OpacityTo : c.OpacityFrom
        };
        public static float Evaluate(StudioClip clip, StudioProperty property, float age, double duration)
        {
            age = Mathf.Clamp(age, 0, (float)duration);
            var channel = Channel(clip, property);
            if (channel == null && clip.Kind != StudioKind.State)
            {
                float progress = Mathf.Clamp01(age / Mathf.Max(.001f, (float)duration));
                if (clip.Kind != StudioKind.Effect && clip.Curve != null)
                    progress = clip.Curve.Evaluate(progress);
                return Limit(property, Mathf.LerpUnclamped(Endpoint(clip, property, false), Endpoint(clip, property, true), progress));
            }

            var samples = new KeySamples();
            if (channel == null)
            {
                if (clip.Keys != null)
                    foreach (var key in clip.Keys)
                        if (key != null)
                            samples.Add(key.Time, Value(key, property), age);
            }
            else if (channel.Keys != null)
            {
                foreach (var key in channel.Keys)
                    if (key != null)
                        samples.Add(key.Time, key.Value, age);
            }

            return Limit(property, samples.Evaluate(age, clip.Curve, Endpoint(clip, property, false)));
        }

        // 기존 오브젝트 키와 속성별 키에 같은 경계 선택·보간 규칙을 적용함.
        private struct KeySamples
        {
            private bool hasBefore, hasAfter;
            private float beforeTime, afterTime, beforeValue, afterValue;

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

        public static StudioPoseKey Sample(StudioClip clip, float age, double duration) => new StudioPoseKey
        {
            Position = new Vector2(Evaluate(clip, StudioProperty.PositionX, age, duration), Evaluate(clip, StudioProperty.PositionY, age, duration)),
            Scale = Evaluate(clip, StudioProperty.Scale, age, duration),
            Rotation = Evaluate(clip, StudioProperty.Rotation, age, duration),
            Opacity = Evaluate(clip, StudioProperty.Opacity, age, duration)
        };
    }
}
