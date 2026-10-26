using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal sealed partial class StudioPropertiesPanel
    {
        private static readonly StudioProperty[] PositionProperties = { StudioProperty.PositionX, StudioProperty.PositionY };

        private bool CanRecord(TimelineClip clip) => !host.Playing && StudioCurrentKeyEditing.TryLocalTime(clip, host.CurrentTime, out _);

        private void BuildCurrentFields(TimelineClip clip)
        {
            bool camera = StudioKeyframes.IsCamera(host.Selected);
            bool dialogue = host.Selected.Kind == StudioKind.Dialogue;
            bool effect = host.Selected.Kind == StudioKind.Effect;
            bool audio = host.Selected.Kind == StudioKind.Audio;
            var section = Section("현재 시점 / 키프레임", "current-keys");
            section.AddToClassList("studio-properties-current");
            Note(section, (audio ? "재생 헤드 위치의 클립 볼륨" : camera ? "재생 헤드 위치의 카메라 값" : dialogue ? "재생 헤드 위치의 말풍선 상태" : effect ? "재생 헤드 위치의 화면 효과 값" :
                host.Selected.Kind == StudioKind.Background ? "재생 헤드 위치의 배경 상태" :
                "재생 헤드 위치의 오브젝트 상태") + " / 현재 키는 값 변경 시 바로 갱신됩니다. 키가 없으면 ◇로 추가하거나 자동 키 기록을 켜세요.");
            if (dialogue) Note(section, "이동 X / Y는 기본 말풍선 위치에 더해지는 값입니다. 화자 오브젝트의 위치는 유지됩니다.");
            var times = new VisualElement();
            times.AddToClassList("studio-properties-current-times");
            var sequenceTime = new Label { name = "current-sequence-time" };
            var localTime = new Label { name = "current-local-time" };
            times.Add(sequenceTime); times.Add(localTime); section.Add(times);
            var availability = new Label { name = "current-key-availability" };
            availability.AddToClassList("studio-properties-note");
            section.Add(availability);

            if (audio) BuildCurrentFloat(section, clip, StudioProperty.Volume, "volume", "볼륨 (0–1)", "페이드 / 채널 음량 적용 전의 클립 볼륨입니다.");
            else if (effect) BuildCurrentEffectFields(section, clip);
            else BuildCurrentTransformFields(section, clip, camera, dialogue);

            var all = StudioGUI.ActionButton("현재 시점에 전체 키 추가", () => RecordCurrent(clip, StudioKeyframes.EditingProperties(host.Selected)), "current-all-keys");
            all.AddToClassList("studio-properties-key-all"); section.Add(all);
            refreshFields.Add(Refresh);
            Refresh();
            void Refresh()
            {
                sequenceTime.text = $"시퀀스 {host.CurrentTime:0.###}초";
                localTime.text = $"클립 내 {host.CurrentTime - clip.start:0.###}초";
                bool inside = StudioCurrentKeyEditing.TryLocalTime(clip, host.CurrentTime, out _);
                availability.text = host.Playing ? "재생 중 / 현재 값을 확인할 수 있습니다. 편집하려면 재생을 멈추세요." :
                    !inside ? "클립 범위 밖 / 이전 또는 다음 키로 이동한 뒤 편집하세요." : "";
                availability.style.display = availability.text.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                all.SetEnabled(CanRecord(clip));
            }
        }

        private void BuildCurrentTransformFields(VisualElement section, TimelineClip clip, bool camera, bool dialogue)
        {
            bool background = host.Selected.Kind == StudioKind.Background;
            if (camera && !string.IsNullOrEmpty(host.Selected.CameraTargetId))
                Note(section, "위치는 따라갈 대상에 더하는 키 보정값입니다. 대상 위치 / 클립 전체 위치 보정 / 현재 위치 키를 합산합니다.");
            var position = new Vector2Field { name = "current-position", tooltip = dialogue ? "기본 말풍선 위치에 더하는 X / Y 이동 값" : $"{StudioSequence.Width:0} × {StudioSequence.Height:0} 기준 화면 좌표" };
            position.Query<FloatField>().ForEach(field => field.isDelayed = true);
            BuildCurrentProperty(section, clip, "position", dialogue ? "말풍선 이동" : "위치", PositionProperties, position,
                () => position.SetValueWithoutNotify(new Vector2(currentKeys.Value(StudioProperty.PositionX), currentKeys.Value(StudioProperty.PositionY))));
            position.RegisterValueChangedCallback(e =>
            {
                currentKeys.Observe(clip, host.CurrentTime);
                if (CanRecord(clip) && StudioPropertyEditing.IsFinite(e.newValue.x) && StudioPropertyEditing.IsFinite(e.newValue.y))
                {
                    CompleteCurrentEdit(clip, currentKeys.Edit(clip, host.CurrentTime,
                        new Dictionary<StudioProperty, float> { [StudioProperty.PositionX] = e.newValue.x, [StudioProperty.PositionY] = e.newValue.y }, host.Snap));
                }
                SyncTimingFields();
            });

            if (!camera)
                BuildCurrentFloat(section, clip, StudioProperty.Rotation, "rotation", "회전 (°)", dialogue ? "말풍선 전체의 회전 각도" : (background ? "배경" : "오브젝트") + "의 회전 각도");
            if (host.Selected.Kind == StudioKind.Visual)
                BuildCurrentFlip(section, clip);
            BuildCurrentFloat(section, clip, StudioProperty.Scale, camera ? "zoom" : "scale", camera ? "줌 (배)" : "크기 (배)",
                camera ? "1배는 기준 화면 크기 / 값이 클수록 확대됩니다." : dialogue ? "1배는 기본 말풍선 크기 / 최소 0.01배입니다." : background ? "1배는 기준 화면을 채우는 크기 / 최소 0.01배입니다." : "1배는 오브젝트의 기본 크기 / 최소 0.01배입니다.");
            if (!camera)
                BuildCurrentFloat(section, clip, StudioProperty.Opacity, "opacity", "불투명도", dialogue ? "0은 투명 / 1은 불투명 / 말풍선 배경과 글자에 함께 적용됩니다." : background ? "0은 투명 / 1은 불투명 / 배경 이미지에 적용됩니다." : "0은 투명 / 1은 불투명 / 오브젝트 공통 색상의 알파 값과 함께 적용됩니다.");

        }

        private void BuildCurrentFlip(VisualElement parent, TimelineClip clip)
        {
            const StudioProperty property = StudioProperty.FlipX;
            var field = new Toggle { name = "current-FlipX", tooltip = "키 시점에서 즉시 전환 / 다음 키까지 유지" };
            BuildCurrentProperty(parent, clip, "flip", "좌우 반전", new[] { property }, field, () =>
            {
                bool flipped = currentKeys.Value(property) >= .5f;
                field.SetValueWithoutNotify(flipped);
                field.text = StudioPropertyPresentation.Value(property, flipped ? 1 : 0);
            });
            field.RegisterValueChangedCallback(e =>
            {
                currentKeys.Observe(clip, host.CurrentTime);
                if (CanRecord(clip))
                    CompleteCurrentEdit(clip, currentKeys.Edit(clip, host.CurrentTime,
                        new Dictionary<StudioProperty, float> { [property] = e.newValue ? 1 : 0 }, host.Snap));
                SyncTimingFields();
            });
        }

        private void BuildCurrentFloat(VisualElement parent, TimelineClip clip, StudioProperty property, string id, string label, string tooltip)
        {
            var properties = new[] { property };
            var field = new FloatField { name = "current-" + property, isDelayed = true, tooltip = tooltip };
            BuildCurrentProperty(parent, clip, id, label, properties, field,
                () => field.SetValueWithoutNotify(currentKeys.Value(property)));
            field.RegisterValueChangedCallback(e =>
            {
                currentKeys.Observe(clip, host.CurrentTime);
                if (CanRecord(clip))
                    CompleteCurrentEdit(clip, currentKeys.Edit(clip, host.CurrentTime,
                        new Dictionary<StudioProperty, float> { [property] = e.newValue }, host.Snap));
                SyncTimingFields();
            });
        }

        private void BuildCurrentProperty(VisualElement parent, TimelineClip clip, string id, string label,
            IReadOnlyList<StudioProperty> properties, VisualElement input, Action refreshInput)
        {
            var row = new VisualElement(); row.AddToClassList("studio-properties-current-property"); parent.Add(row);
            var heading = new VisualElement(); heading.AddToClassList("studio-properties-property-heading"); row.Add(heading);
            heading.Add(new Label(label));
            var automatic = new Toggle("자동 키 기록") { name = "current-auto-" + id, tooltip = "속성 입력 또는 미리보기 조작으로 값이 바뀔 때 현재 시점의 키만 기록합니다. 켜기만 하면 키를 만들지 않습니다." };
            heading.Add(automatic); row.Add(input);
            var footer = new VisualElement(); footer.AddToClassList("studio-properties-property-footer"); row.Add(footer);
            var status = new Label { name = "current-status-" + id }; footer.Add(status);
            var buttons = new VisualElement(); buttons.AddToClassList("studio-properties-key-buttons"); footer.Add(buttons);
            var previous = StudioGUI.ActionButton("◀", () => SeekKey(false), "current-prev-" + id);
            var add = StudioGUI.ActionButton("◇", () => RecordCurrent(clip, properties), "current-key-" + id);
            var next = StudioGUI.ActionButton("▶", () => SeekKey(true), "current-next-" + id);
            previous.tooltip = label + " 이전 키로 이동"; next.tooltip = label + " 다음 키로 이동";
            buttons.Add(previous); buttons.Add(add); buttons.Add(next);
            automatic.RegisterValueChangedCallback(e =>
            {
                if (!CanRecord(clip)) { Refresh(); return; }
                currentKeys.Observe(clip, host.CurrentTime);
                foreach (var property in properties) currentKeys.Arm(property, e.newValue);
                Refresh();
                host.Window?.Repaint();
            });
            refreshFields.Add(Refresh);
            Refresh();
            void SeekKey(bool forward)
            {
                if (host.Playing) return;
                double? time = StudioCurrentKeyEditing.AdjacentTime(clip, properties, host.CurrentTime, forward);
                if (!time.HasValue) return;
                host.Seek(time.Value); currentKeys.Observe(clip, host.CurrentTime); SyncTimingFields();
            }
            void Refresh()
            {
                refreshInput();
                bool enabled = CanRecord(clip), dirty = false, armed = true;
                int keyed = 0, explicitKeys = 0;
                float local = (float)(host.CurrentTime - clip.start);
                foreach (var property in properties)
                {
                    dirty |= currentKeys.HasPending(property); armed &= currentKeys.IsArmed(property);
                    if (!StudioCurrentKeyEditing.HasKey(clip, property, local)) continue;
                    keyed++;
                    if (StudioCurrentKeyEditing.HasRecordedKey(clip, property, local)) explicitKeys++;
                }
                input.SetEnabled(enabled); automatic.SetEnabled(enabled); add.SetEnabled(enabled);
                automatic.SetValueWithoutNotify(armed);
                status.text = dirty ? "미기록 변경" : explicitKeys == properties.Count ? "현재 키 있음" :
                    explicitKeys > 0 ? "일부 속성에 키 있음" : keyed > 0 ? "기본 경계값" : "현재 키 없음";
                status.EnableInClassList("has-key", explicitKeys > 0 && !dirty);
                bool recorded = explicitKeys == properties.Count;
                add.text = recorded ? "◆" : "◇";
                add.tooltip = recorded ? label + " 현재 키 있음 / 값 변경 시 바로 갱신됩니다." : label + " 현재 시점에 키 추가";
                previous.SetEnabled(!host.Playing && StudioCurrentKeyEditing.AdjacentTime(clip, properties, host.CurrentTime, false).HasValue);
                next.SetEnabled(!host.Playing && StudioCurrentKeyEditing.AdjacentTime(clip, properties, host.CurrentTime, true).HasValue);
            }
        }

        private void RecordCurrent(TimelineClip clip, IReadOnlyList<StudioProperty> properties)
        {
            if (!CanRecord(clip)) return;
            CompleteCurrentEdit(clip, currentKeys.Record(clip, host.CurrentTime, properties, host.Snap));
            SyncTimingFields();
        }

        private void CompleteCurrentEdit(TimelineClip clip, float? recordedTime)
        {
            if (recordedTime.HasValue)
            {
                host.Session.ClearKey();
                if (Math.Abs(clip.start + recordedTime.Value - host.CurrentTime) > .000001)
                    host.Seek(clip.start + recordedTime.Value);
                currentKeys.Observe(clip, host.CurrentTime);
                host.Changed();
            }
            else host.Window?.Repaint();
        }
    }
}
