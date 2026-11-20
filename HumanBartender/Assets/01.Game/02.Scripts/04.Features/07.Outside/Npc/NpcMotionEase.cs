using System;
using UnityEngine;

public enum NpcMotionEase
{
    [InspectorName("일정하게")] Linear,
    [InspectorName("부드럽게 시작·끝")] SmoothStep
}

internal static class NpcMotionCurves
{
    internal static float Evaluate(NpcMotionEase ease, float progress)
    {
        float p = Mathf.Clamp01(progress);
        return ease switch
        {
            NpcMotionEase.Linear => p,
            NpcMotionEase.SmoothStep => p * p * (3 - 2 * p),
            _ => throw new ArgumentOutOfRangeException(nameof(ease))
        };
    }
}
