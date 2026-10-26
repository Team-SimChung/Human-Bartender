namespace HumanBartender.CutsceneStudio.Editor
{
    // 기존 숫자 속성의 표시 계약을 재사용하고 속성 패널 전용 이산 값만 확장함.
    internal static class StudioPropertyPresentation
    {
        internal static string Label(StudioClip clip, StudioProperty property) =>
            StudioTimelinePanel.PropertyLabel(clip, property);

        internal static string Value(StudioProperty property, float value) =>
            StudioKeyframes.IsDiscrete(property) ? (value >= .5f ? "반전" : "기본 방향") : value.ToString("0.###");
    }
}
