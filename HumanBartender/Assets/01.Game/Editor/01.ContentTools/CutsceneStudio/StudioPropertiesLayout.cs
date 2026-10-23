using System;
using System.Collections.Generic;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal enum StudioPropertiesSection
    {
        Target, Camera, Transform, Appearance, Animation, Playback, SharedObject,
        Dialogue, Typography, Bubble, DialoguePlayback, Effect, EffectSettings,
        Curve, Wait, VisualSource, Audio, AudioPlayback, Fade, StateTarget,
        StateKeys, StatePlayback, Background
    }

    // 속성 패널의 배치만 정의하며 공통 클립 정의와 다른 패널의 순서는 변경하지 않음.
    internal static class StudioPropertiesLayout
    {
        private static readonly IReadOnlyList<StudioPropertiesSection> Camera = Layout(
            StudioPropertiesSection.Camera, StudioPropertiesSection.Playback);
        private static readonly IReadOnlyList<StudioPropertiesSection> Actor = Layout(
            StudioPropertiesSection.Target, StudioPropertiesSection.Transform,
            StudioPropertiesSection.Appearance, StudioPropertiesSection.Animation,
            StudioPropertiesSection.Playback, StudioPropertiesSection.SharedObject);
        private static readonly IReadOnlyList<StudioPropertiesSection> Dialogue = Layout(
            StudioPropertiesSection.Dialogue, StudioPropertiesSection.Typography,
            StudioPropertiesSection.Bubble, StudioPropertiesSection.DialoguePlayback);
        private static readonly IReadOnlyList<StudioPropertiesSection> Effect = Layout(
            StudioPropertiesSection.Effect, StudioPropertiesSection.EffectSettings, StudioPropertiesSection.Curve);
        private static readonly IReadOnlyList<StudioPropertiesSection> Wait = Layout(StudioPropertiesSection.Wait);
        private static readonly IReadOnlyList<StudioPropertiesSection> Visual = Layout(
            StudioPropertiesSection.VisualSource, StudioPropertiesSection.Transform,
            StudioPropertiesSection.Appearance, StudioPropertiesSection.Animation,
            StudioPropertiesSection.Playback, StudioPropertiesSection.SharedObject);
        private static readonly IReadOnlyList<StudioPropertiesSection> Audio = Layout(
            StudioPropertiesSection.Audio, StudioPropertiesSection.AudioPlayback, StudioPropertiesSection.Fade);
        private static readonly IReadOnlyList<StudioPropertiesSection> State = Layout(
            StudioPropertiesSection.StateTarget, StudioPropertiesSection.StateKeys, StudioPropertiesSection.StatePlayback);
        private static readonly IReadOnlyList<StudioPropertiesSection> Background = Layout(
            StudioPropertiesSection.Background, StudioPropertiesSection.Transform,
            StudioPropertiesSection.Appearance, StudioPropertiesSection.Playback);

        private static IReadOnlyList<StudioPropertiesSection> Layout(params StudioPropertiesSection[] sections)
        {
            return Array.AsReadOnly(sections);
        }

        internal static IReadOnlyList<StudioPropertiesSection> Sections(StudioKind kind) => kind switch
        {
            StudioKind.Camera => Camera,
            StudioKind.Actor => Actor,
            StudioKind.Dialogue => Dialogue,
            StudioKind.Effect => Effect,
            StudioKind.Wait => Wait,
            StudioKind.Visual => Visual,
            StudioKind.Audio => Audio,
            StudioKind.State => State,
            StudioKind.Background => Background,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        internal static string Title(StudioPropertiesSection section) => section switch
        {
            StudioPropertiesSection.Target => "대상",
            StudioPropertiesSection.Camera => "카메라 구도",
            StudioPropertiesSection.Transform => "변형",
            StudioPropertiesSection.Appearance => "외형",
            StudioPropertiesSection.Animation => "애니메이션",
            StudioPropertiesSection.Playback or StudioPropertiesSection.StatePlayback => "보간 · 종료",
            StudioPropertiesSection.SharedObject => "오브젝트 공통 설정",
            StudioPropertiesSection.Dialogue => "대사",
            StudioPropertiesSection.Typography => "글꼴 · 텍스트",
            StudioPropertiesSection.Bubble => "말풍선",
            StudioPropertiesSection.DialoguePlayback => "재생 · 입력 대기",
            StudioPropertiesSection.Effect => "효과",
            StudioPropertiesSection.EffectSettings => "효과 설정",
            StudioPropertiesSection.Curve => "보간",
            StudioPropertiesSection.Wait => "입력 대기",
            StudioPropertiesSection.VisualSource => "표시 에셋",
            StudioPropertiesSection.Audio => "오디오",
            StudioPropertiesSection.AudioPlayback => "재생",
            StudioPropertiesSection.Fade => "페이드",
            StudioPropertiesSection.StateTarget => "대상",
            StudioPropertiesSection.StateKeys => "속성 키",
            StudioPropertiesSection.Background => "배경 이미지",
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
    }
}
