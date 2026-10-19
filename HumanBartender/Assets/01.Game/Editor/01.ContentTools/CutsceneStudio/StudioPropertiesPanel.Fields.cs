using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 유형별 속성의 표시를 담당하며 값의 범위와 키 보간은 기존 모델에 위임함.
    internal sealed partial class StudioPropertiesPanel
    {
        private void BuildSection(StudioPropertiesSection kind, TimelineClip clip, SerializedObject data)
        {
            if (kind == StudioPropertiesSection.SharedObject &&
                (host.ActorIndex < 0 || host.ActorIndex >= host.Sequence.Actors.Count))
                return;
            var section = Section(StudioPropertiesLayout.Title(kind), kind.ToString(), kind != StudioPropertiesSection.SharedObject);
            switch (kind)
            {
                case StudioPropertiesSection.Target:
                    BuildActorPicker(section, false);
                    break;
                case StudioPropertiesSection.Camera:
                case StudioPropertiesSection.Transform:
                    BuildTransform(section, clip, kind == StudioPropertiesSection.Camera);
                    break;
                case StudioPropertiesSection.Appearance:
                    EndpointField(section, clip, StudioProperty.Opacity, false, "시작 불투명도");
                    EndpointField(section, clip, StudioProperty.Opacity, true, "종료 불투명도");
                    ClipField(section, data, "FlipX");
                    break;
                case StudioPropertiesSection.Animation:
                    BuildAnimation(section, data);
                    break;
                case StudioPropertiesSection.Playback:
                    ClipField(section, data, "Curve");
                    ClipField(section, data, "HoldEnd");
                    break;
                case StudioPropertiesSection.SharedObject:
                    BuildSharedObject(section);
                    break;
                case StudioPropertiesSection.Dialogue:
                    BuildActorPicker(section, true);
                    ClipField(section, data, "Text").AddToClassList("studio-properties-dialogue");
                    break;
                case StudioPropertiesSection.Typography:
                    ClipField(section, data, "Font");
                    ClipField(section, data, "CharactersPerSecond");
                    Note(section, "글꼴을 비워 두면 기본 글꼴을 사용합니다. TMP 리치 텍스트 태그를 사용할 수 있습니다.");
                    break;
                case StudioPropertiesSection.Bubble:
                    bool hasSpeaker = host.ActorIndex >= 0;
                    ClipField(section, data, "BubbleOffset", hasSpeaker).tooltip = "화자의 위치를 기준으로 말풍선 높이를 조절합니다.";
                    if (!hasSpeaker)
                        Note(section, "말풍선 높이는 연결된 화자가 있을 때 적용됩니다. 화자가 없으면 아래 위치의 Y 값으로 조절하세요.");
                    Note(section, "위치는 기본 말풍선 위치에 더해지는 이동 값입니다.");
                    BuildTransform(section, clip, false, true);
                    break;
                case StudioPropertiesSection.DialoguePlayback:
                    ClipField(section, data, "WaitForInput");
                    ClipField(section, data, "Curve");
                    if (host.Selected.WaitForInput)
                        Note(section, "대사가 끝나면 E / Enter 입력으로 진행합니다.");
                    break;
                case StudioPropertiesSection.Effect:
                    ClipField(section, data, "Effect");
                    break;
                case StudioPropertiesSection.EffectSettings:
                    BuildEffect(section, clip, data);
                    break;
                case StudioPropertiesSection.Curve:
                    ClipField(section, data, "Curve");
                    break;
                case StudioPropertiesSection.Wait:
                    ReadOnly(section, "대기 시점", "wait-trigger").value = "클립 시작";
                    ReadOnly(section, "진행 입력", "wait-input").value = "E / Enter";
                    Note(section, "클립 시작 시점에서 입력을 기다립니다. 클립 길이는 입력 대기 제한 시간이 아닙니다.");
                    break;
                case StudioPropertiesSection.VisualSource:
                    BuildActorPicker(section, false);
                    BuildClipSprite(section);
                    break;
                case StudioPropertiesSection.Audio:
                    ClipField(section, data, "Sound");
                    ClipField(section, data, "Bus");
                    BuildAudioInfo(section);
                    break;
                case StudioPropertiesSection.AudioPlayback:
                    ClipField(section, data, "Volume");
                    ClipField(section, data, "AudioOffset");
                    ClipField(section, data, "Loop");
                    Note(section, "미리보기 재생에도 게임과 같은 음량·믹서 설정을 적용합니다. 소리가 없으면 Unity의 Mute Audio 설정을 확인하세요.");
                    break;
                case StudioPropertiesSection.Fade:
                    ClipField(section, data, "FadeIn");
                    ClipField(section, data, "FadeOut");
                    break;
                case StudioPropertiesSection.StateTarget:
                    BuildStateTarget(section);
                    if (!host.Selected.StateCamera)
                        BuildActorPicker(section, false);
                    break;
                case StudioPropertiesSection.StateKeys:
                    BuildKeySummary(section, clip);
                    break;
                case StudioPropertiesSection.StatePlayback:
                    ClipField(section, data, "Curve");
                    ClipField(section, data, "HoldEnd");
                    if (!host.Selected.StateCamera)
                        ClipField(section, data, "FlipX");
                    break;
                case StudioPropertiesSection.Background:
                    ClipField(section, data, "BackgroundSprite");
                    BuildSpritePreview(section, true);
                    break;
            }
        }

        private void BuildActorPicker(VisualElement parent, bool allowNone)
        {
            var asset = host.Selected;
            var ids = new List<string>();
            var labels = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var actor in host.Sequence.Actors)
            {
                if (actor == null || string.IsNullOrEmpty(actor.Id))
                    continue;
                string label = string.IsNullOrEmpty(actor.Name) ? actor.Id : actor.Name;
                counts[label] = counts.TryGetValue(label, out int count) ? count + 1 : 1;
            }
            if (allowNone)
            {
                ids.Add("");
                labels.Add("화자 없음");
            }
            foreach (var actor in host.Sequence.Actors)
            {
                if (actor == null || string.IsNullOrEmpty(actor.Id))
                    continue;
                string label = string.IsNullOrEmpty(actor.Name) ? actor.Id : actor.Name;
                ids.Add(actor.Id);
                labels.Add(counts[label] > 1 ? $"{label} ({actor.Id})" : label);
            }
            int index = ids.IndexOf(asset.ActorId ?? "");
            if (index < 0)
            {
                index = ids.Count;
                ids.Add(asset.ActorId ?? "");
                labels.Add(string.IsNullOrEmpty(asset.ActorId) ? "대상을 선택하세요" : $"연결 없음 ({asset.ActorId})");
            }
            var picker = new PopupField<string>(allowNone ? "화자" : "대상 오브젝트", labels, index) { name = "actor-picker" };
            parent.Add(picker);
            picker.RegisterValueChangedCallback(ChangeTarget);
            if (!allowNone && string.IsNullOrEmpty(asset.ActorId))
                Note(parent, "시퀀스에 등록된 오브젝트를 선택하세요.");

            void ChangeTarget(ChangeEvent<string> _)
            {
                Undo.RecordObject(asset, "클립 대상 변경");
                asset.ActorId = ids[picker.index];
                host.Session.ClearKey();
                host.Changed(true);
            }
        }

        private void BuildStateTarget(VisualElement parent)
        {
            var asset = host.Selected;
            var picker = new PopupField<string>("대상 유형", new List<string> { "오브젝트", "카메라" }, asset.StateCamera ? 1 : 0)
            { name = "state-target" };
            parent.Add(picker);
            picker.RegisterValueChangedCallback(ChangeTarget);
            void ChangeTarget(ChangeEvent<string> _)
            {
                Undo.RecordObject(asset, "속성 키 대상 변경");
                asset.StateCamera = picker.index == 1;
                host.Session.ClearKey();
                host.Changed();
            }
        }

        private void BuildTransform(VisualElement parent, TimelineClip clip, bool camera, bool opacity = false)
        {
            if (StudioKeyframes.HasOverrides(host.Selected))
                Note(parent, "시작·종료 값을 바꾸면 클립 경계의 키를 편집합니다. 중간 키는 유지됩니다.");
            for (int i = 0; i < 2; i++)
            {
                bool end = i == 1;
                var pose = new VisualElement();
                pose.AddToClassList("studio-properties-pose");
                var label = new Label(end ? "종료 상태" : "시작 상태");
                label.AddToClassList("studio-properties-subtitle");
                pose.Add(label);
                parent.Add(pose);
                PositionField(pose, clip, end);
                if (!camera)
                    EndpointField(pose, clip, StudioProperty.Rotation, end, "회전 (°)");
                EndpointField(pose, clip, StudioProperty.Scale, end, camera ? "줌 (배)" : "크기 (배)");
                if (opacity)
                    EndpointField(pose, clip, StudioProperty.Opacity, end, "불투명도");
            }
        }

        private void PositionField(VisualElement parent, TimelineClip clip, bool end)
        {
            var field = new Vector2Field("위치")
            {
                name = "endpoint-" + (end ? "end" : "start") + "-position",
                tooltip = $"{StudioSequence.Width:0} × {StudioSequence.Height:0} 기준 화면 좌표"
            };
            parent.Add(field);
            field.RegisterValueChangedCallback(ChangePosition);
            refreshFields.Add(Refresh);
            Refresh();
            void ChangePosition(ChangeEvent<Vector2> e)
            {
                if (StudioPropertyEditing.IsFinite(e.newValue.x) && StudioPropertyEditing.IsFinite(e.newValue.y))
                {
                    int group = Undo.GetCurrentGroup();
                    if (e.previousValue.x != e.newValue.x)
                        StudioPropertyEditing.SetEndpoint(clip, StudioProperty.PositionX, end, e.newValue.x);
                    if (e.previousValue.y != e.newValue.y)
                        StudioPropertyEditing.SetEndpoint(clip, StudioProperty.PositionY, end, e.newValue.y);
                    Undo.CollapseUndoOperations(group);
                    host.Session.ClearKey();
                    host.Changed();
                }
                Refresh();
            }
            void Refresh()
            {
                field.SetValueWithoutNotify(new Vector2(StudioPropertyEditing.Endpoint(clip, StudioProperty.PositionX, end),
                    StudioPropertyEditing.Endpoint(clip, StudioProperty.PositionY, end)));
            }
        }

        private void EndpointField(VisualElement parent, TimelineClip clip, StudioProperty property, bool end, string label)
        {
            var field = new FloatField(label) { name = "endpoint-" + (end ? "end" : "start") + "-" + property, isDelayed = true };
            parent.Add(field);
            field.RegisterValueChangedCallback(ChangeValue);
            refreshFields.Add(Refresh);
            Refresh();
            void ChangeValue(ChangeEvent<float> e)
            {
                if (StudioPropertyEditing.SetEndpoint(clip, property, end, e.newValue))
                {
                    host.Session.ClearKey();
                    host.Changed();
                }
                Refresh();
            }
            void Refresh() { field.SetValueWithoutNotify(StudioPropertyEditing.Endpoint(clip, property, end)); }
        }

        private void BuildAnimation(VisualElement parent, SerializedObject data)
        {
            var asset = host.Selected;
            ClipField(parent, data, "Frames", changed: FramesChanged);
            bool sourceTiming = StudioPropertyEditing.UsesSourceTiming(asset);
            ReadOnly(parent, "프레임 타이밍", "animation-timing").value = sourceTiming ? "원본 애니메이션" : "초당 프레임";
            ClipField(parent, data, "FramesPerSecond", !sourceTiming);
            ClipField(parent, data, "Loop");
            if (sourceTiming)
                Note(parent, $"원본 길이 {asset.AnimationLength:0.###}초의 프레임 시각을 사용합니다.");
            void FramesChanged() { StudioPropertyEditing.ValidateFrameTiming(asset); }
        }

        private void BuildSharedObject(VisualElement parent)
        {
            var data = PanelBinding(host.Sequence);
            string path = $"Actors.Array.data[{host.ActorIndex}].";
            Note(parent, "이 오브젝트를 사용하는 모든 클립에 적용됩니다.");
            BoundField(parent, data, path + "Name", "이름");
            BoundField(parent, data, path + "Sprite", "기본 이미지").tooltip = "클립에 프레임이 없을 때 사용하는 이미지";
            BoundField(parent, data, path + "Size", "기본 크기");
            BoundField(parent, data, path + "Color", "색상");
            BoundField(parent, data, path + "Layer", "그리기 순서");
        }

        private void BuildClipSprite(VisualElement parent)
        {
            var asset = host.Selected;
            bool animated = asset.Frames != null && asset.Frames.Length > 1;
            var field = new ObjectField(animated ? "첫 프레임" : "클립 이미지")
            { name = "clip-sprite", objectType = typeof(Sprite), allowSceneObjects = false };
            field.SetEnabled(!animated);
            parent.Add(field);
            field.RegisterValueChangedCallback(ChangeSprite);
            refreshFields.Add(Refresh);
            Refresh();
            if (animated)
                Note(parent, "이미지 배열은 애니메이션의 프레임 목록에서 편집합니다.");
            BuildSpritePreview(parent, false);
            void ChangeSprite(ChangeEvent<UnityEngine.Object> e)
            {
                StudioPropertyEditing.SetSprite(asset, e.newValue as Sprite);
                host.Changed();
            }
            void Refresh() { field.SetValueWithoutNotify(asset.Frames != null && asset.Frames.Length > 0 ? asset.Frames[0] : null); }
        }

        private void BuildSpritePreview(VisualElement parent, bool background)
        {
            var asset = host.Selected;
            var preview = new Image { name = "sprite-preview", scaleMode = ScaleMode.ScaleToFit };
            preview.AddToClassList("studio-properties-preview");
            parent.Add(preview);
            var size = ReadOnly(parent, "이미지 크기", "sprite-size");
            refreshFields.Add(Refresh);
            Refresh();
            void Refresh()
            {
                var sprite = background ? asset.BackgroundSprite : asset.Frames != null && asset.Frames.Length > 0 ? asset.Frames[0] : null;
                preview.sprite = sprite;
                preview.style.display = sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
                size.SetValueWithoutNotify(sprite != null ? $"{sprite.rect.width:0} × {sprite.rect.height:0} px" : "이미지 없음");
            }
        }

        private void BuildEffect(VisualElement parent, TimelineClip clip, SerializedObject data)
        {
            if (StudioKeyframes.Channel(host.Selected, StudioProperty.Strength) == null)
                ClipField(parent, data, "Strength");
            else
            {
                EndpointField(parent, clip, StudioProperty.Strength, false, "시작 강도");
                EndpointField(parent, clip, StudioProperty.Strength, true, "종료 강도");
            }
            bool shake = host.Selected.Effect == StudioEffect.Shake;
            if (!shake)
                ClipField(parent, data, "EffectColor");
            EndpointField(parent, clip, StudioProperty.Opacity, false, shake ? "시작 감쇠" : "시작 불투명도");
            EndpointField(parent, clip, StudioProperty.Opacity, true, shake ? "종료 감쇠" : "종료 불투명도");
        }

        private void BuildAudioInfo(VisualElement parent)
        {
            var asset = host.Selected;
            var length = ReadOnly(parent, "원본 길이 (초)", "audio-length");
            refreshFields.Add(Refresh);
            Refresh();
            void Refresh() { length.SetValueWithoutNotify(asset.Sound != null ? asset.Sound.length.ToString("0.###") : "오디오 없음"); }
        }

        private void BuildKeySummary(VisualElement parent, TimelineClip clip)
        {
            foreach (var property in StudioKeyframes.Properties(host.Selected))
            {
                var field = ReadOnly(parent, StudioTimelinePanel.PropertyLabel(host.Selected, property), "key-count-" + property);
                refreshFields.Add(Refresh);
                Refresh();
                void Refresh()
                {
                    int count = StudioKeyframes.GetKeys(host.Selected, property, clip.duration).Length;
                    field.SetValueWithoutNotify($"{count}개 키");
                }
            }
            Note(parent, "시퀀스 타임라인에서 키를 선택하면 위에 시간과 값이 표시됩니다. 속성 행을 더블클릭하면 키가 추가됩니다.");
        }
    }
}
