using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal readonly struct StudioClipField
    {
        internal readonly string Path, Label;
        internal readonly bool Enabled, Animation;
        internal StudioClipField(string path, string label, bool enabled, bool animation)
        {
            Path = path;
            Label = label;
            Enabled = enabled;
            Animation = animation;
        }
    }

    internal readonly struct StudioClipDefinition
    {
        internal readonly StudioKind Kind;
        internal readonly string Label, Icon, GroupLabel;
        internal readonly Color Color;
        internal readonly int Group;

        internal StudioClipDefinition(StudioKind kind, string label, string icon, Color color, int group, string groupLabel = null)
        {
            Kind = kind;
            Label = label;
            Icon = icon;
            Color = color;
            Group = group;
            GroupLabel = groupLabel;
        }
    }

    // 클립의 표시 정보와 속성 필드를 공유하며 재생 동작은 런타임에 유지함.
    internal static class StudioClipSchema
    {
        // 배열 순서는 클립 추가와 새 트랙 메뉴의 표시 순서에 쓰임.
        internal static IReadOnlyList<StudioClipDefinition> Definitions { get; } = Array.AsReadOnly(new[]
        {
            new StudioClipDefinition(StudioKind.Camera, "카메라", "▣", new Color(.53f, .46f, .67f), 0, "카메라"),
            new StudioClipDefinition(StudioKind.Actor, "액터 이동", "↗", new Color(.38f, .55f, .63f), 1, "액터 / 오브젝트"),
            new StudioClipDefinition(StudioKind.Dialogue, "대사", "▤", new Color(.68f, .59f, .40f), 3, "대사 / 입력"),
            new StudioClipDefinition(StudioKind.Effect, "화면 효과", "✦", new Color(.65f, .45f, .48f), 4, "화면 효과"),
            new StudioClipDefinition(StudioKind.Wait, "입력 대기", "◇", new Color(.58f, .62f, .45f), 3),
            new StudioClipDefinition(StudioKind.Visual, "에셋 / 오브젝트", "▧", new Color(.43f, .55f, .67f), 1),
            new StudioClipDefinition(StudioKind.Audio, "사운드", "♫", new Color(.49f, .60f, .44f), 2, "사운드"),
            new StudioClipDefinition(StudioKind.State, "오브젝트 키", "◆", new Color(.64f, .48f, .57f), 1),
            new StudioClipDefinition(StudioKind.Background, "배경", "▧", new Color(.46f, .61f, .56f), 5, "배경")
        });
        internal static IReadOnlyList<StudioClipDefinition> Groups { get; } = BuildGroups();

        internal static StudioClipDefinition Definition(StudioKind kind)
        {
            foreach (var definition in Definitions)
                if (definition.Kind == kind)
                    return definition;
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "클립 유형의 표시 정의가 없습니다.");
        }

        private static IReadOnlyList<StudioClipDefinition> BuildGroups()
        {
            var groups = new SortedDictionary<int, StudioClipDefinition>();
            foreach (var definition in Definitions)
                if (definition.GroupLabel != null)
                    groups.Add(definition.Group, definition);
            return new List<StudioClipDefinition>(groups.Values).AsReadOnly();
        }

        internal static List<StudioClipField> Fields(StudioClip clip)
        {
            var fields = new List<StudioClipField>();
            bool editable = !StudioKeyframes.HasOverrides(clip);
            void Add(string path, string label, bool enabled = true, bool animation = false)
            {
                fields.Add(new StudioClipField(path, label, enabled, animation));
            }

            if (StudioEvaluation.OwnsActor(clip) || clip.Kind == StudioKind.Dialogue || clip.Kind == StudioKind.State && !clip.StateCamera)
                Add("ActorId", "캐릭터 ID");
            switch (clip.Kind)
            {
                case StudioKind.Actor:
                case StudioKind.Visual:
                case StudioKind.Camera:
                case StudioKind.Background:
                    if (clip.Kind == StudioKind.Background)
                        Add("BackgroundSprite", "배경 이미지");
                    Add("From", "시작 위치", editable);
                    Add("To", "도착 위치", editable);
                    Add("ScaleFrom", StudioKeyframes.IsCamera(clip) ? "시작 줌" : "시작 크기", editable);
                    Add("ScaleTo", StudioKeyframes.IsCamera(clip) ? "도착 줌" : "도착 크기", editable);
                    Add("Curve", "진행 곡선");
                    Add("HoldEnd", "종료 상태 유지");
                    if (clip.Kind == StudioKind.Camera)
                        break;
                    Add("RotationFrom", "시작 회전", editable);
                    Add("RotationTo", "도착 회전", editable);
                    Add("OpacityFrom", "시작 불투명도", editable);
                    Add("OpacityTo", "도착 불투명도", editable);
                    Add("FlipX", "좌우 반전");
                    if (clip.Kind == StudioKind.Background)
                        break;
                    Add("Frames", "애니메이션 프레임", animation: true);
                    Add("FramesPerSecond", "초당 프레임", animation: true);
                    Add("Loop", "반복", animation: true);
                    break;
                case StudioKind.State:
                    Add("StateCamera", "카메라 대상");
                    Add("Keys", "속성 키 (클립 시작 기준 초)", editable);
                    Add("Curve", "키 사이 보간 곡선");
                    Add("HoldEnd", "종료 상태 유지");
                    Add("FlipX", "좌우 반전");
                    break;
                case StudioKind.Dialogue:
                    Add("Text", "대사");
                    Add("Font", "대사 글꼴");
                    Add("CharactersPerSecond", "초당 글자 수");
                    Add("BubbleOffset", "말풍선 높이");
                    Add("WaitForInput", "끝에서 입력 대기");
                    Add("Curve", "키 사이 보간 곡선");
                    break;
                case StudioKind.Effect:
                    Add("Effect", "효과");
                    Add("Strength", "강도", StudioKeyframes.Channel(clip, StudioProperty.Strength) == null);
                    Add("EffectColor", "색상");
                    Add("Curve", "키 사이 보간 곡선");
                    break;
                case StudioKind.Audio:
                    Add("Sound", "오디오");
                    Add("Volume", "볼륨");
                    Add("AudioOffset", "원본 시작 시간");
                    Add("FadeIn", "페이드 인");
                    Add("FadeOut", "페이드 아웃");
                    Add("Loop", "반복");
                    break;
            }

            return fields;
        }

        internal static IEnumerable<string> Help(StudioClip clip)
        {
            if (StudioKeyframes.HasOverrides(clip))
                yield return "위치 / 크기 등의 값은 키프레임 탭에서 편집하세요. 속성 행을 더블클릭하면 키가 추가됩니다.";
            if (clip.Kind == StudioKind.Background && clip.BackgroundSprite == null)
                yield return "배경 이미지를 지정하세요.";
            switch (clip.Kind)
            {
                case StudioKind.State:
                    yield return "Time 순서로 위치 / 크기 / 회전 / 불투명도를 보간합니다. 카메라는 Position과 Scale(줌)을 사용합니다.";
                    break;
                case StudioKind.Dialogue:
                    yield return "키프레임 탭에서 말풍선의 이동 / 크기 / 회전 / 불투명도를 조절합니다. 이동 값은 기본 말풍선 위치에 더해집니다.";
                    yield return "E / Enter로 입력 대기를 진행합니다. Unity TMP 리치 텍스트 태그를 사용할 수 있습니다.";
                    break;
                case StudioKind.Effect:
                    yield return "키프레임 탭에서 강도와 불투명도(흔들기는 감쇠)를 조절합니다.";
                    break;
                case StudioKind.Audio:
                    yield return "현재 프리뷰는 무음입니다. 오디오는 저장한 씬을 Play하거나 게임에서 컷씬을 호출해 확인하세요.";
                    break;
                case StudioKind.Wait:
                    yield return "이 클립의 시작 시점에서 E / Enter 입력을 기다립니다.";
                    break;
            }
        }
    }
}
