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
            var section = Section("현재 시점 / 키프레임", "current-keys");
            section.AddToClassList("studio-properties-current");
            Note(section, (camera ? "재생 헤드 위치의 카메라 값" : dialogue ? "재생 헤드 위치의 말풍선 상태" : effect ? "재생 헤드 위치의 화면 효과 값" :
                host.Selected.Kind == StudioKind.Background ? "재생 헤드 위치의 배경 상태" :
                host.Selected.Kind is StudioKind.Visual or StudioKind.State ? "재생 헤드 위치의 오브젝트 상태" : "재생 헤드 위치의 액터 상태") + " / 자동 키가 꺼져 있으면 키 추가 시 저장됩니다.");
            if (host.Selected.Kind == StudioKind.State)
                Note(section, "이 클립의 키를 평가한 값입니다. 기존 상태 키도 같은 영역에서 편집하며, 다른 클립의 연출은 포함하지 않습니다.");
            if (dialogue) Note(section, "이동 X / Y는 기본 말풍선 위치에 더해지는 값입니다. 화자 오브젝트의 위치는 유지됩니다.");
            var times = new VisualElement();
            times.AddToClassList("studio-properties-current-times");
            var sequenceTime = new Label { name = "current-sequence-time" };
            var localTime = new Label { name = "current-local-time" };
            times.Add(sequenceTime); times.Add(localTime); section.Add(times);
            var availability = new Label { name = "current-key-availability" };
            availability.AddToClassList("studio-properties-note");
            section.Add(availability);

            if (effect) BuildCurrentEffectFields(section, clip);
            else BuildCurrentTransformFields(section, clip, camera, dialogue);

            var all = StudioGUI.ActionButton("현재 시점에 전체 키 추가", () => RecordCurrent(clip, StudioKeyframes.Properties(host.Selected)), "current-all-keys");
            all.AddToClassList("studio-properties-key-all"); section.Add(all);
            Note(section, effect ? "강도 / " + EffectOpacityLabel : camera ? "위치 X / Y / 줌" : "위치 X / Y / 회전 / 크기 / 불투명도");
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
            var position = new Vector2Field { name = "current-position", tooltip = dialogue ? "기본 말풍선 위치에 더하는 X / Y 이동 값" : $"{StudioSequence.Width:0} × {StudioSequence.Height:0} 기준 화면 좌표" };
            position.Query<FloatField>().ForEach(field => field.isDelayed = true);
            BuildCurrentProperty(section, clip, "position", dialogue ? "말풍선 이동" : "위치", PositionProperties, position,
                () => position.SetValueWithoutNotify(new Vector2(currentKeys.Value(StudioProperty.PositionX), currentKeys.Value(StudioProperty.PositionY))));
            position.RegisterValueChangedCallback(e =>
            {
                currentKeys.Observe(clip, host.CurrentTime);
                if (CanRecord(clip) && StudioPropertyEditing.IsFinite(e.newValue.x) && StudioPropertyEditing.IsFinite(e.newValue.y))
                {
                    currentKeys.SetPending(StudioProperty.PositionX, e.newValue.x);
                    currentKeys.SetPending(StudioProperty.PositionY, e.newValue.y);
                    if (currentKeys.IsArmed(StudioProperty.PositionX)) RecordCurrent(clip, PositionProperties);
                }
                SyncTimingFields();
            });

            if (!camera)
                BuildCurrentFloat(section, clip, StudioProperty.Rotation, "rotation", "회전 (°)", dialogue ? "말풍선 전체의 회전 각도" : (background ? "배경" : "오브젝트") + "의 회전 각도 / 좌우 반전은 외형의 클립 전체 설정입니다.");
            BuildCurrentFloat(section, clip, StudioProperty.Scale, camera ? "zoom" : "scale", camera ? "줌 (배)" : "크기 (배)",
                camera ? "1배는 기준 화면 크기 / 값이 클수록 확대됩니다." : dialogue ? "1배는 기본 말풍선 크기 / 최소 0.01배입니다." : background ? "1배는 기준 화면을 채우는 크기 / 최소 0.01배입니다." : "1배는 오브젝트의 기본 크기 / 최소 0.01배입니다.");
            if (!camera)
                BuildCurrentFloat(section, clip, StudioProperty.Opacity, "opacity", "불투명도", dialogue ? "0은 투명 / 1은 불투명 / 말풍선 배경과 글자에 함께 적용됩니다." : background ? "0은 투명 / 1은 불투명 / 배경 이미지에 적용됩니다." : "0은 투명 / 1은 불투명 / 오브젝트 공통 색상의 알파 값과 함께 적용됩니다.");

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
                if (CanRecord(clip) && currentKeys.SetPending(property, e.newValue) && currentKeys.IsArmed(property))
                    RecordCurrent(clip, properties);
                SyncTimingFields();
            });
        }

        private void BuildCurrentProperty(VisualElement parent, TimelineClip clip, string id, string label,
            IReadOnlyList<StudioProperty> properties, VisualElement input, Action refreshInput)
        {
            var row = new VisualElement(); row.AddToClassList("studio-properties-current-property"); parent.Add(row);
            var heading = new VisualElement(); heading.AddToClassList("studio-properties-property-heading"); row.Add(heading);
            heading.Add(new Label(label));
            var automatic = new Toggle("자동 키") { name = "current-auto-" + id, tooltip = "켜면 현재 값을 기록하고, 이후 값 변경 시 현재 시간의 키를 추가 / 갱신합니다." };
            heading.Add(automatic); row.Add(input);
            var footer = new VisualElement(); footer.AddToClassList("studio-properties-property-footer"); row.Add(footer);
            var status = new Label { name = "current-status-" + id }; footer.Add(status);
            var buttons = new VisualElement(); buttons.AddToClassList("studio-properties-key-buttons"); footer.Add(buttons);
            var previous = StudioGUI.ActionButton("이전", () => SeekKey(false), "current-prev-" + id);
            var add = StudioGUI.ActionButton("키 추가", () => RecordCurrent(clip, properties), "current-key-" + id);
            var next = StudioGUI.ActionButton("다음", () => SeekKey(true), "current-next-" + id);
            previous.tooltip = label + " 이전 키로 이동"; next.tooltip = label + " 다음 키로 이동";
            buttons.Add(previous); buttons.Add(add); buttons.Add(next);
            automatic.RegisterValueChangedCallback(e =>
            {
                if (!CanRecord(clip)) { Refresh(); return; }
                foreach (var property in properties) currentKeys.Arm(property, e.newValue);
                if (e.newValue) RecordCurrent(clip, properties);
                Refresh();
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
                    if (StudioKeyframes.Channel(host.Selected, property) != null || host.Selected.Kind == StudioKind.State) explicitKeys++;
                }
                input.SetEnabled(enabled); automatic.SetEnabled(enabled); add.SetEnabled(enabled);
                automatic.SetValueWithoutNotify(armed);
                status.text = dirty ? "미기록 변경" : explicitKeys == properties.Count ? "현재 키 있음" :
                    explicitKeys > 0 ? "일부 속성에 키 있음" : keyed > 0 ? "기본 경계값" : "현재 키 없음";
                status.EnableInClassList("has-key", explicitKeys > 0 && !dirty);
                add.text = explicitKeys == properties.Count ? "키 갱신" : "키 추가";
                previous.SetEnabled(!host.Playing && StudioCurrentKeyEditing.AdjacentTime(clip, properties, host.CurrentTime, false).HasValue);
                next.SetEnabled(!host.Playing && StudioCurrentKeyEditing.AdjacentTime(clip, properties, host.CurrentTime, true).HasValue);
            }
        }

        private void RecordCurrent(TimelineClip clip, IReadOnlyList<StudioProperty> properties)
        {
            currentKeys.Observe(clip, host.CurrentTime);
            if (!CanRecord(clip) || !StudioCurrentKeyEditing.TryLocalTime(clip, host.CurrentTime, out float local)) return;
            bool existing = false;
            foreach (var property in properties) existing |= StudioCurrentKeyEditing.HasKey(clip, property, local);
            // 기존 키의 값을 바꿀 때는 비정규 프레임에 기록된 키의 시간도 보존함.
            float at = existing ? local : Mathf.Clamp((float)host.Snap(local), 0, (float)clip.duration);
            var values = new Dictionary<StudioProperty, float>();
            foreach (var property in properties) values.Add(property, currentKeys.Value(property));
            if (!StudioCurrentKeyEditing.Write(clip, at, values, "현재 시점 키 기록")) return;
            currentKeys.Recorded(properties); host.Session.ClearKey();
            if (Math.Abs(at - local) > .000001) host.Seek(clip.start + at);
            currentKeys.Observe(clip, host.CurrentTime);
            host.Changed();
            SyncTimingFields();
        }
    }
}
