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
            float age = Mathf.Max(0, (float)Math.Min(localTime, duration));
            int index = Mathf.FloorToInt(age * Mathf.Max(1, clip.FramesPerSecond));
            index = clip.Loop ? index % clip.Frames.Length : Mathf.Min(index, clip.Frames.Length - 1);
            if (clip.FrameTimes != null && clip.FrameTimes.Length == clip.Frames.Length && clip.AnimationLength > 0)
            {
                age = clip.Loop ? age % clip.AnimationLength : Mathf.Min(age, clip.AnimationLength);
                index = 0;
                for (int i = 0; i < clip.FrameTimes.Length; i++)
                    if (clip.FrameTimes[i] <= age)
                        index = i;
            }

            return clip.Frames[index];
        }
    }
}
