using System;
using UnityEngine;

namespace HumanBartender.CutsceneStudio
{
    internal static class StudioSpriteAnimation
    {
        internal static Sprite Sample(StudioClip clip, double localTime, double duration)
        {
            if (clip.Frames == null || clip.Frames.Length == 0)
                return null;
            // 원본 타이밍과 FPS 방식 모두 클립 종료 시점의 프레임을 유지함.
            float age = StudioExtensionValues.NonNegative(clip.AnimationOffset) +
                Mathf.Max(0, (float)Math.Min(localTime, duration)) * Speed(clip);
            int index = Mathf.FloorToInt(age * Mathf.Max(1, clip.FramesPerSecond));
            index = clip.Loop ? index % clip.Frames.Length : Mathf.Min(index, clip.Frames.Length - 1);
            if (UsesSourceTiming(clip))
            {
                age = clip.Loop ? age % clip.AnimationLength : Mathf.Min(age, clip.AnimationLength);
                index = 0;
                for (int i = 0; i < clip.FrameTimes.Length; i++)
                    if (clip.FrameTimes[i] <= age)
                        index = i;
            }

            return clip.Frames[index];
        }

        // 재생 배속 × 엔진 속도 보정. 엔진 속도 보정이 없는 기존 클립은 1이라 결과가 같음.
        internal static float Speed(StudioClip clip) =>
            StudioExtensionValues.Speed(clip.AnimationSpeed) * StudioExtensionValues.Speed(clip.EngineSpeed);

        // 프레임 목록을 한 바퀴 도는 데 걸리는 타임라인 시간(초). 프레임이 없으면 0.
        internal static float CycleLength(StudioClip clip)
        {
            if (clip.Frames == null || clip.Frames.Length == 0)
                return 0;
            float sourceLength = UsesSourceTiming(clip) ? clip.AnimationLength : clip.Frames.Length / Mathf.Max(1, clip.FramesPerSecond);
            return sourceLength / Speed(clip);
        }

        private static bool UsesSourceTiming(StudioClip clip) =>
            clip.FrameTimes != null && clip.FrameTimes.Length == clip.Frames.Length && clip.AnimationLength > 0;
    }
}
