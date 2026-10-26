using System;
using System.Collections.Generic;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal enum StudioPropertiesSection
    {
        Appearance, Animation, Playback, SharedObject,
        Dialogue, Typography, Bubble, DialoguePlayback, Effect, EffectSettings,
        Curve, Wait, VisualSource, Audio, AudioPlayback, Fade,
        Background, CurrentKeys, RecordedKeys, WaitPosition, AudioPosition, DialoguePrefab, CameraFollow, BackgroundTransition
    }

    // 속성 패널의 배치만 정의하며 공통 클립 정의와 다른 패널의 순서는 변경하지 않음.
    internal static class StudioPropertiesLayout
    {
        private static readonly IReadOnlyList<StudioPropertiesSection> Camera = Layout(
            StudioPropertiesSection.CameraFollow,
            StudioPropertiesSection.CurrentKeys,
            StudioPropertiesSection.Playback, StudioPropertiesSection.RecordedKeys);
        private static readonly IReadOnlyList<StudioPropertiesSection> Dialogue = Layout(
            StudioPropertiesSection.Dialogue, StudioPropertiesSection.DialoguePrefab,
            StudioPropertiesSection.Typography,
            StudioPropertiesSection.Bubble, StudioPropertiesSection.DialoguePlayback);
        private static readonly IReadOnlyList<StudioPropertiesSection> Effect = Layout(
            StudioPropertiesSection.Effect, StudioPropertiesSection.CurrentKeys,
            StudioPropertiesSection.EffectSettings,
            StudioPropertiesSection.Curve, StudioPropertiesSection.RecordedKeys);
        private static readonly IReadOnlyList<StudioPropertiesSection> Wait = Layout(
            StudioPropertiesSection.Wait, StudioPropertiesSection.WaitPosition);
        private static readonly IReadOnlyList<StudioPropertiesSection> Visual = Layout(
            StudioPropertiesSection.SharedObject, StudioPropertiesSection.CurrentKeys,
            StudioPropertiesSection.Animation, StudioPropertiesSection.Playback,
            StudioPropertiesSection.RecordedKeys);
        private static readonly IReadOnlyList<StudioPropertiesSection> Audio = Layout(
            StudioPropertiesSection.Audio, StudioPropertiesSection.CurrentKeys, StudioPropertiesSection.AudioPlayback,
            StudioPropertiesSection.Fade, StudioPropertiesSection.Curve, StudioPropertiesSection.AudioPosition, StudioPropertiesSection.RecordedKeys);
        private static readonly IReadOnlyList<StudioPropertiesSection> Background = Layout(
            StudioPropertiesSection.Background, StudioPropertiesSection.CurrentKeys,
            StudioPropertiesSection.Appearance, StudioPropertiesSection.BackgroundTransition,
            StudioPropertiesSection.Playback, StudioPropertiesSection.RecordedKeys);

        private static IReadOnlyList<StudioPropertiesSection> Layout(params StudioPropertiesSection[] sections)
        {
            return Array.AsReadOnly(sections);
        }

        internal static IReadOnlyList<StudioPropertiesSection> Sections(StudioKind kind) => kind switch
        {
            StudioKind.Camera => Camera,
            StudioKind.Dialogue => Dialogue,
            StudioKind.Effect => Effect,
            StudioKind.Wait => Wait,
            StudioKind.Visual => Visual,
            StudioKind.Audio => Audio,
            StudioKind.Background => Background,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        internal static bool HasCurrentEditor(StudioKind kind) => kind is StudioKind.Camera or StudioKind.Effect or StudioKind.Visual or StudioKind.Background or StudioKind.Audio;

        internal static string Title(StudioPropertiesSection section, StudioKind kind) => (kind, section) switch
        {
            (StudioKind.Background, StudioPropertiesSection.Background) => "배경 이미지 / 클립 전체",
            (StudioKind.Background, StudioPropertiesSection.Appearance) => "외형 / 클립 전체",
            (StudioKind.Visual, StudioPropertiesSection.VisualSource) => "표시 에셋 / 클립 전체",
            (StudioKind.Visual, StudioPropertiesSection.Animation) => "애니메이션 / 클립 전체",
            (StudioKind.Visual, StudioPropertiesSection.Appearance) => "외형 / 클립 전체",
            (StudioKind.Dialogue, StudioPropertiesSection.Dialogue) => "대사 / 클립 전체",
            (StudioKind.Dialogue, StudioPropertiesSection.Typography) => "글꼴 / 타이핑",
            (StudioKind.Dialogue, StudioPropertiesSection.Bubble) => "말풍선 기준 / 클립 전체",
            (StudioKind.Dialogue, StudioPropertiesSection.DialoguePlayback) => "입력 / 클립 전체",
            _ => Title(section)
        };

        private static string Title(StudioPropertiesSection section) => section switch
        {
            StudioPropertiesSection.Appearance => "외형",
            StudioPropertiesSection.Animation => "애니메이션",
            StudioPropertiesSection.Playback => "보간 / 종료",
            StudioPropertiesSection.SharedObject => "오브젝트 공통 설정",
            StudioPropertiesSection.Dialogue => "대사",
            StudioPropertiesSection.DialoguePrefab => "말풍선 프리팹",
            StudioPropertiesSection.Typography => "글꼴 / 텍스트",
            StudioPropertiesSection.Bubble => "말풍선",
            StudioPropertiesSection.DialoguePlayback => "재생 / 입력 대기",
            StudioPropertiesSection.Effect => "효과 유형 / 클립 전체",
            StudioPropertiesSection.EffectSettings => "효과 설정 / 클립 전체",
            StudioPropertiesSection.Curve => "보간",
            StudioPropertiesSection.Wait => "입력 대기 / 클립 전체",
            StudioPropertiesSection.WaitPosition => "대기 시점 / 확인",
            StudioPropertiesSection.VisualSource => "표시 에셋",
            StudioPropertiesSection.Audio => "오디오 / 클립 전체",
            StudioPropertiesSection.AudioPlayback => "재생 / 클립 전체",
            StudioPropertiesSection.Fade => "페이드 / 클립 전체",
            StudioPropertiesSection.AudioPosition => "현재 시점 / 확인",
            StudioPropertiesSection.Background => "배경 이미지",
            StudioPropertiesSection.CameraFollow => "대상 따라가기 / 클립 전체",
            StudioPropertiesSection.BackgroundTransition => "이미지 전환 / 클립 전체",
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
    }
}
