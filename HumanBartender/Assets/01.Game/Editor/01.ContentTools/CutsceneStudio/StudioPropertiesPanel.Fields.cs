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
            if (kind == StudioPropertiesSection.CurrentKeys) { BuildCurrentFields(clip); return; }
            if (kind == StudioPropertiesSection.RecordedKeys) { BuildRecordedKeyFields(clip); return; }
            if (kind == StudioPropertiesSection.SharedObject &&
                (host.ActorIndex < 0 || host.ActorIndex >= host.Sequence.Actors.Count))
                return;
            var section = Section(StudioPropertiesLayout.Title(kind, host.Selected.Kind), kind.ToString(), kind != StudioPropertiesSection.SharedObject || host.Selected.Kind == StudioKind.Visual);
            switch (kind)
            {
                case StudioPropertiesSection.Appearance:
                    Note(section, "좌우 반전은 클립 전체에 적용됩니다. 불투명도는 현재 시점 / 키프레임에서 편집하세요.");
                    ClipField(section, data, "FlipX");
                    break;
                case StudioPropertiesSection.Animation:
                    BuildAnimation(section, data);
                    break;
                case StudioPropertiesSection.Playback:
                    if (StudioPropertiesLayout.HasCurrentEditor(host.Selected.Kind))
                    {
                        BuildMotionPlayback(section, data);
                        break;
                    }
                    ClipField(section, data, "Curve");
                    ClipField(section, data, "HoldEnd");
                    break;
                case StudioPropertiesSection.SharedObject:
                    BuildSharedObject(section);
                    break;
                case StudioPropertiesSection.Dialogue:
                    Note(section, "화자와 본문은 대사 클립 전체에 적용됩니다.");
                    BuildSpeakerPicker(section);
                    if (string.IsNullOrEmpty(host.Selected.ActorId))
                        BoundField(section, data, "SpeakerName", "화자 표시 이름");
                    BuildDialogueText(section);
                    break;
                case StudioPropertiesSection.Typography:
                    BuildDialogueTypography(section, data);
                    break;
                case StudioPropertiesSection.Bubble:
                    BuildDialogueLayout(section, data);
                    BuildDialogueHeight(section, data);
                    break;
                case StudioPropertiesSection.DialoguePrefab:
                    BuildDialoguePrefab(section, data);
                    break;
                case StudioPropertiesSection.DialoguePlayback:
                    BuildTypingAudio(section, data, false);
                    ClipField(section, data, "WaitForInput");
                    if (host.Selected.WaitForInput)
                        Note(section, "타이핑 중 첫 입력은 전체 표시, 다음 입력은 클립 끝으로 이동합니다. 자동 진행 대사에서는 입력해도 전체 표시하지 않습니다.");
                    break;
                case StudioPropertiesSection.Effect:
                    ClipField(section, data, "Effect");
                    Note(section, "효과 유형은 클립 전체에 적용됩니다. 유형을 바꿔도 저장된 강도 / 불투명도 키는 유지됩니다.");
                    break;
                case StudioPropertiesSection.EffectSettings:
                    BuildEffect(section, data);
                    break;
                case StudioPropertiesSection.Curve:
                    if (host.Selected.Kind == StudioKind.Audio)
                    {
                        Note(section, "볼륨 키 사이의 변화에 적용됩니다. 페이드 인 / 아웃은 이 볼륨에 추가 적용됩니다.");
                        BuildMotionPlayback(section, data, false);
                    }
                    else if (host.Selected.Kind is StudioKind.Dialogue or StudioKind.Effect)
                    {
                        Note(section, host.Selected.Kind == StudioKind.Dialogue ? "말풍선 속성 키 사이의 변화에 적용됩니다. 타이핑 속도는 글꼴 / 타이핑에서 설정하세요." :
                            "강도 / " + EffectOpacityLabel + "의 키 사이 변화에 적용됩니다. 효과는 클립 구간에서만 표시됩니다.");
                        BuildMotionPlayback(section, data, false);
                    }
                    else ClipField(section, data, "Curve");
                    break;
                case StudioPropertiesSection.Wait:
                    BuildWaitSettings(section);
                    break;
                case StudioPropertiesSection.WaitPosition:
                    BuildWaitPosition(section, clip);
                    break;
                case StudioPropertiesSection.VisualSource:
                    Note(section, "클립 이미지 / 애니메이션 프레임은 이 클립에 적용됩니다. 프레임을 비우면 오브젝트 공통 설정의 기본 이미지를 사용합니다.");
                    BuildClipSprite(section);
                    break;
                case StudioPropertiesSection.Audio:
                    BuildAudioSource(section, data);
                    break;
                case StudioPropertiesSection.AudioPlayback:
                    BuildAudioPlayback(section, data);
                    break;
                case StudioPropertiesSection.Fade:
                    BuildAudioFade(section, data);
                    break;
                case StudioPropertiesSection.AudioPosition:
                    BuildAudioPosition(section, clip);
                    break;
                case StudioPropertiesSection.Background:
                    Note(section, "이미지는 클립 전체에 적용됩니다. 비워 두면 배경 이미지는 표시되지 않습니다. 위치 / 회전 / 크기 / 불투명도는 아래에서 편집하세요.");
                    ClipField(section, data, "BackgroundSprite");
                    BuildSpritePreview(section, true);
                    break;
                case StudioPropertiesSection.CameraFollow:
                    BuildCameraFollow(section, data);
                    break;
                case StudioPropertiesSection.BackgroundTransition:
                    BoundField(section, data, "BackgroundDissolve", "디졸브 시간 (초)");
                    Note(section, "0초는 즉시 전환 / 클립 시작부터 이전에 표시되던 배경 위로 서서히 나타납니다. 클립 길이를 넘는 값은 클립 길이로 제한됩니다.");
                    break;
            }
        }

        private void BuildSpeakerPicker(VisualElement parent)
        {
            var asset = host.Selected;
            var ids = new List<string> { "" };
            var labels = new List<string> { "화자 없음" };
            var counts = new Dictionary<string, int>();
            foreach (var actor in host.Sequence.Actors)
            {
                if (actor == null || string.IsNullOrEmpty(actor.Id))
                    continue;
                string label = string.IsNullOrEmpty(actor.Name) ? actor.Id : actor.Name;
                counts[label] = counts.TryGetValue(label, out int count) ? count + 1 : 1;
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
            var picker = new PopupField<string>("화자", labels, index) { name = "actor-picker" };
            parent.Add(picker);
            picker.RegisterValueChangedCallback(ChangeTarget);

            void ChangeTarget(ChangeEvent<string> _)
            {
                Undo.RecordObject(asset, "클립 대상 변경");
                asset.ActorId = ids[picker.index];
                host.Session.ClearKey();
                host.Changed(true);
            }
        }

        private void BuildAnimation(VisualElement parent, SerializedObject data)
        {
            var asset = host.Selected;
            const string framesOnly = " 애니메이션 프레임에만 적용되며 이동 키와 클립 길이는 유지됩니다.";
            BuildEngineClip(parent);
            ClipField(parent, data, "Frames", changed: FramesChanged).tooltip =
                "클립에서 넘길 이미지 목록입니다. 원본 프레임과 재생 설정은 위치나 회전 등의 속성 키와 별개입니다.";
            // 엔진 클립의 키 시간으로 재생하는 동안에는 쓰지 않는 값이라 숨김. 연결을 해제하면 다시 나타남.
            if (!StudioPropertyEditing.UsesSourceTiming(asset))
                ClipField(parent, data, "FramesPerSecond").tooltip = "엔진 클립의 키 시간을 쓰지 않을 때 1초에 넘기는 프레임 수입니다.";
            ClipField(parent, data, "Loop");
            BuildWaitPlayback(parent, data);
            BoundField(parent, data, "AnimationSpeed", "재생 배속").tooltip =
                "1이면 엔진(게임)과 같은 속도입니다. 실제 재생 속도는 재생 배속 × 엔진 속도 보정입니다." + framesOnly;
            BoundField(parent, data, "AnimationOffset", "원본 시작 (초)").tooltip = "애니메이션을 원본의 몇 초 지점부터 시작할지 정합니다." + framesOnly;
            void FramesChanged() { StudioPropertyEditing.ValidateFrameTiming(asset); }
        }

        private void BuildSharedObject(VisualElement parent)
        {
            var data = PanelBinding(host.Sequence);
            string path = $"Actors.Array.data[{host.ActorIndex}].";
            Note(parent, "이 오브젝트를 사용하는 모든 클립에 적용됩니다.");
            if (host.Selected.Kind == StudioKind.Visual)
                BuildSharedVisualIdentity(parent);
            else
            {
                BoundField(parent, data, path + "Name", "이름");
                BoundField(parent, data, path + "Sprite", "기본 이미지").tooltip = "클립에 프레임이 없을 때 사용하는 이미지";
            }
            var actor = host.Sequence.Actors[host.ActorIndex];
            if (!string.IsNullOrEmpty(actor.Caption))
            {
                BoundField(parent, data, path + "Caption", "크레딧 문구");
                BoundField(parent, data, path + "CaptionSize", "글자 크기");
                BoundField(parent, data, path + "CenterCaption", "가운데 정렬");
            }
            if (actor.HasRegion)
            {
                BoundField(parent, data, path + "RegionTexture", "원본 그림");
                BoundField(parent, data, path + "Region", "영역 꼭짓점");
                BoundField(parent, data, path + "RegionMaterial", "원화 표시 효과");
            }
            BoundField(parent, data, path + "Size", "기본 크기");
            BoundField(parent, data, path + "Color", "색상");
            BoundField(parent, data, path + "Layer", "그리기 순서");
            if (host.Selected.Kind == StudioKind.Visual)
                Note(parent, "이름 / 기본 이미지를 바꾸면 클립 정보에도 자동 반영됩니다. 애니메이션 프레임은 별도로 유지됩니다.");
        }

        private void BuildSharedVisualIdentity(VisualElement parent)
        {
            string id = host.Selected.ActorId;
            var name = new TextField("이름") { name = "shared-object-name", isDelayed = true };
            var sprite = new ObjectField("기본 이미지") { name = "shared-object-sprite", objectType = typeof(Sprite), allowSceneObjects = false };
            parent.Add(name); parent.Add(sprite);
            name.RegisterValueChangedCallback(e =>
            {
                if (StudioSharedObjectEditing.Rename(host.Sequence, id, e.newValue)) host.Changed(true);
                Refresh();
            });
            sprite.RegisterValueChangedCallback(e =>
            {
                if (StudioSharedObjectEditing.SetSprite(host.Sequence, id, e.newValue as Sprite)) host.Changed(true);
                Refresh();
            });
            refreshFields.Add(Refresh);
            Refresh();
            void Refresh()
            {
                var actor = host.Sequence.FindActor(id);
                name.SetValueWithoutNotify(actor?.Name ?? "");
                sprite.SetValueWithoutNotify(actor?.Sprite);
            }
        }

        private void BuildLinkedVisualSource(VisualElement parent)
        {
            var image = new ObjectField("클립 이미지") { name = "clip-sprite", objectType = typeof(Sprite), allowSceneObjects = false };
            image.SetEnabled(false); parent.Add(image);
            refreshFields.Add(() => image.SetValueWithoutNotify(host.Sequence.FindActor(host.Selected.ActorId)?.Sprite));
            BuildSpritePreview(parent, false);
            Note(parent, "클립 이름 / 이미지는 오브젝트 공통 설정을 따릅니다.");
            if (StudioSharedObjectEditing.HasStaticOverride(host.Selected))
                Note(parent, "기존 개별 이미지는 유지 중 / 기본 이미지를 변경하면 공통 이미지에 연결됩니다.");
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
                var sprite = background ? asset.BackgroundSprite : asset.Kind == StudioKind.Visual ? host.Sequence.FindActor(asset.ActorId)?.Sprite : asset.Frames != null && asset.Frames.Length > 0 ? asset.Frames[0] : null;
                preview.sprite = sprite;
                preview.style.display = sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
                size.SetValueWithoutNotify(sprite != null ? $"{sprite.rect.width:0} × {sprite.rect.height:0} px" : "이미지 없음");
            }
        }

        private void BuildEffect(VisualElement parent, SerializedObject data)
        {
            if (!StudioKeyframes.HasKeys(host.Selected, StudioProperty.Strength))
            {
                var strength = ClipField(parent, data, "Strength");
                strength.label = "기본 강도";
                strength.tooltip = "강도 키가 없을 때 클립 전체에 적용되는 값입니다.";
                Note(parent, "기본 강도는 강도 키가 없을 때 적용됩니다. 시간에 따라 바꾸려면 현재 시점 / 키프레임을 편집하세요.");
            }
            else Note(parent, "강도 키 사용 중 / 현재 시점 / 키프레임에서 강도를 편집하세요.");
            bool shake = host.Selected.Effect == StudioEffect.Shake;
            if (!shake)
            {
                BoundField(parent, data, "ScreenMaterial", "화면 머티리얼");
                ClipField(parent, data, "EffectColor");
                Note(parent, "효과 색상은 클립 전체에 적용됩니다. 표시 불투명도는 색상의 알파 × 강도 × 불투명도 값입니다.");
            }
            else
            {
                BoundField(parent, data, "ShakeSpeed", "흔들림 속도 (배)");
                BoundField(parent, data, "ShakeAmplitude", "가로 / 세로 크기");
                Note(parent, "기본값은 속도 1배 / 크기 12, 12입니다. 크기 × 강도 × 감쇠를 적용하며, 한 축을 0으로 두면 해당 방향은 흔들리지 않습니다.");
            }
            // 흔들림이거나 화면 머티리얼이 있을 때만 보임. 머티리얼을 넣고 빼면 다시 그리지 않아도 따라감.
            BuildWaitPlayback(parent, data);
        }

    }
}
