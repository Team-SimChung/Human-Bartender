using System;
using System.Collections.Generic;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal enum StudioPropertiesSection
    {
        Target, Camera, Transform, Appearance, Animation, Playback, SharedObject,
        Dialogue, Typography, Bubble, DialoguePlayback, Effect, EffectSettings,
        Curve, Wait, VisualSource, Audio, AudioPlayback, Fade, StateTarget,
        StatePlayback, Background, CurrentKeys, RecordedKeys, EffectEndpoints, WaitPosition, AudioPosition
    }

    // 속성 패널의 배치만 정의하며 공통 클립 정의와 다른 패널의 순서는 변경하지 않음.
    internal static class StudioPropertiesLayout
    {
        private static readonly IReadOnlyList<StudioPropertiesSection> Camera = Layout(
            StudioPropertiesSection.CurrentKeys, StudioPropertiesSection.Camera,
            StudioPropertiesSection.Playback, StudioPropertiesSection.RecordedKeys);
        private static readonly IReadOnlyList<StudioPropertiesSection> Actor = Layout(
            StudioPropertiesSection.Target, StudioPropertiesSection.CurrentKeys,
            StudioPropertiesSection.Transform, StudioPropertiesSection.Animation,
            StudioPropertiesSection.Appearance, StudioPropertiesSection.Playback,
            StudioPropertiesSection.SharedObject, StudioPropertiesSection.RecordedKeys);
        private static readonly IReadOnlyList<StudioPropertiesSection> Dialogue = Layout(
            StudioPropertiesSection.Dialogue, StudioPropertiesSection.CurrentKeys,
            StudioPropertiesSection.Transform, StudioPropertiesSection.Typography,
            StudioPropertiesSection.Bubble, StudioPropertiesSection.DialoguePlayback,
            StudioPropertiesSection.Curve, StudioPropertiesSection.RecordedKeys);
        private static readonly IReadOnlyList<StudioPropertiesSection> Effect = Layout(
            StudioPropertiesSection.Effect, StudioPropertiesSection.CurrentKeys,
            StudioPropertiesSection.EffectEndpoints, StudioPropertiesSection.EffectSettings,
            StudioPropertiesSection.Curve, StudioPropertiesSection.RecordedKeys);
        private static readonly IReadOnlyList<StudioPropertiesSection> Wait = Layout(
            StudioPropertiesSection.Wait, StudioPropertiesSection.WaitPosition);
        private static readonly IReadOnlyList<StudioPropertiesSection> Visual = Layout(
            StudioPropertiesSection.Target, StudioPropertiesSection.CurrentKeys,
            StudioPropertiesSection.Transform, StudioPropertiesSection.VisualSource,
            StudioPropertiesSection.Animation, StudioPropertiesSection.Appearance,
            StudioPropertiesSection.Playback, StudioPropertiesSection.SharedObject,
            StudioPropertiesSection.RecordedKeys);
        private static readonly IReadOnlyList<StudioPropertiesSection> Audio = Layout(
            StudioPropertiesSection.Audio, StudioPropertiesSection.AudioPlayback,
            StudioPropertiesSection.Fade, StudioPropertiesSection.AudioPosition);
        private static readonly IReadOnlyList<StudioPropertiesSection> State = Layout(
            StudioPropertiesSection.StateTarget, StudioPropertiesSection.CurrentKeys,
            StudioPropertiesSection.RecordedKeys, StudioPropertiesSection.Appearance, StudioPropertiesSection.StatePlayback);
        private static readonly IReadOnlyList<StudioPropertiesSection> Background = Layout(
            StudioPropertiesSection.Background, StudioPropertiesSection.CurrentKeys,
            StudioPropertiesSection.Transform, StudioPropertiesSection.Appearance,
            StudioPropertiesSection.Playback, StudioPropertiesSection.RecordedKeys);

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

        internal static bool HasCurrentEditor(StudioKind kind) => kind is StudioKind.Camera or StudioKind.Actor or StudioKind.Dialogue or StudioKind.Effect or StudioKind.Visual or StudioKind.State or StudioKind.Background;

        internal static string Title(StudioPropertiesSection section, StudioKind kind) => (kind, section) switch
        {
            (StudioKind.Background, StudioPropertiesSection.Background) => "배경 이미지 / 클립 전체",
            (StudioKind.Background, StudioPropertiesSection.Transform) => "시작점 / 도착점",
            (StudioKind.Background, StudioPropertiesSection.Appearance) => "외형 / 클립 전체",
            (StudioKind.State, StudioPropertiesSection.Appearance) => "외형 / 클립 전체",
            (StudioKind.Actor, StudioPropertiesSection.Transform) => "시작점 / 도착점",
            (StudioKind.Actor, StudioPropertiesSection.Animation) => "애니메이션 / 클립 전체",
            (StudioKind.Actor, StudioPropertiesSection.Appearance) => "외형 / 클립 전체",
            (StudioKind.Visual, StudioPropertiesSection.Transform) => "시작점 / 도착점",
            (StudioKind.Visual, StudioPropertiesSection.VisualSource) => "표시 에셋 / 클립 전체",
            (StudioKind.Visual, StudioPropertiesSection.Animation) => "애니메이션 / 클립 전체",
            (StudioKind.Visual, StudioPropertiesSection.Appearance) => "외형 / 클립 전체",
            (StudioKind.Dialogue, StudioPropertiesSection.Transform) => "시작점 / 도착점",
            (StudioKind.Dialogue, StudioPropertiesSection.Dialogue) => "대사 / 클립 전체",
            (StudioKind.Dialogue, StudioPropertiesSection.Typography) => "글꼴 / 타이핑",
            (StudioKind.Dialogue, StudioPropertiesSection.Bubble) => "말풍선 기준 / 클립 전체",
            (StudioKind.Dialogue, StudioPropertiesSection.DialoguePlayback) => "입력 / 클립 전체",
            _ => Title(section)
        };

        private static string Title(StudioPropertiesSection section) => section switch
        {
            StudioPropertiesSection.Target => "대상",
            StudioPropertiesSection.Camera => "시작점 / 도착점",
            StudioPropertiesSection.Transform => "변형",
            StudioPropertiesSection.Appearance => "외형",
            StudioPropertiesSection.Animation => "애니메이션",
            StudioPropertiesSection.Playback or StudioPropertiesSection.StatePlayback => "보간 / 종료",
            StudioPropertiesSection.SharedObject => "오브젝트 공통 설정",
            StudioPropertiesSection.Dialogue => "대사",
            StudioPropertiesSection.Typography => "글꼴 / 텍스트",
            StudioPropertiesSection.Bubble => "말풍선",
            StudioPropertiesSection.DialoguePlayback => "재생 / 입력 대기",
            StudioPropertiesSection.Effect => "효과 유형 / 클립 전체",
            StudioPropertiesSection.EffectEndpoints => "시작값 / 종료값",
            StudioPropertiesSection.EffectSettings => "효과 설정 / 클립 전체",
            StudioPropertiesSection.Curve => "보간",
            StudioPropertiesSection.Wait => "입력 대기 / 클립 전체",
            StudioPropertiesSection.WaitPosition => "대기 시점 / 확인",
            StudioPropertiesSection.VisualSource => "표시 에셋",
            StudioPropertiesSection.Audio => "오디오 / 클립 전체",
            StudioPropertiesSection.AudioPlayback => "재생 / 클립 전체",
            StudioPropertiesSection.Fade => "페이드 / 클립 전체",
            StudioPropertiesSection.AudioPosition => "현재 시점 / 확인",
            StudioPropertiesSection.StateTarget => "대상",
            StudioPropertiesSection.Background => "배경 이미지",
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
    }
}
