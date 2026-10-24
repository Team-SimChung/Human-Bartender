using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal sealed partial class StudioPropertiesPanel
    {
        private void BuildDialogueTypography(VisualElement section, SerializedObject data)
        {
            ClipField(section, data, "Font");
            ClipField(section, data, "CharactersPerSecond");
            var settings = new VisualElement { name = "dialogue-style-settings" };
            ClipField(section, data, "UseDialogueStyle", changed: Refresh);
            section.Add(settings);
            DialogueNumber(settings, data, "DialogueFontSize", "글자 크기", StudioDialoguePresentation.FontSize);
            ClipField(settings, data, "DialogueTextColor");
            ClipField(settings, data, "DialogueAlignment");
            Note(section, $"클립 전체 설정 / 끄면 기본 크기 {StudioDialoguePresentation.DefaultFontSize} / 흰색 / 왼쪽 정렬을 사용하며 지정한 값은 유지됩니다.");
            Note(section, "글꼴을 비워 두면 시퀀스 글꼴 / TMP 기본 글꼴을 사용합니다. 본문의 크기 / 색상 / 정렬 태그는 해당 구간에서 우선합니다.");
            refreshFields.Add(Refresh);
            Refresh();
            void Refresh() => settings.SetEnabled(host.Selected.UseDialogueStyle);
        }

        private void BuildDialogueLayout(VisualElement section, SerializedObject data)
        {
            var settings = new VisualElement { name = "dialogue-layout-settings" };
            ClipField(section, data, "UseBubbleLayout", changed: Refresh);
            section.Add(settings);
            DialogueNumber(settings, data, "BubbleWidth", "말풍선 폭", StudioDialoguePresentation.Width, changed: () =>
            {
                var padding = StudioSerializedFields.Find(data, "BubblePadding");
                padding.vector2Value = StudioDialoguePresentation.Padding(padding.vector2Value,
                    StudioSerializedFields.Find(data, "BubbleWidth").floatValue);
            });
            DialogueNumber(settings, data, "BubblePadding", "가로 안쪽 여백", value =>
                StudioDialoguePresentation.Padding(new Vector2(value, 0), host.Selected.BubbleWidth).x, component: 0);
            DialogueNumber(settings, data, "BubblePadding", "세로 안쪽 여백", value =>
                StudioDialoguePresentation.Padding(new Vector2(0, value), host.Selected.BubbleWidth).y, component: 1);
            Note(section, $"클립 전체 설정 / 폭과 여백은 {StudioSequence.Width} × {StudioSequence.Height} 연출 영역 기준입니다. 끄면 폭 {StudioDialoguePresentation.DefaultWidth} / 가로 여백 {StudioDialoguePresentation.DefaultPadding.x} / 세로 여백 {StudioDialoguePresentation.DefaultPadding.y}를 사용합니다.");
            Note(section, "스타일 또는 배치를 직접 지정하면 전체 본문에 맞춰 높이가 늘어납니다. 타이핑 중 높이는 유지됩니다. 이동 / 회전 / 크기 키는 이 말풍선 전체에 적용됩니다.");
            refreshFields.Add(Refresh);
            Refresh();
            void Refresh() => settings.SetEnabled(host.Selected.UseBubbleLayout);
        }

        // 유효 범위를 저장 시점에도 적용하며 폭과 여백 보정은 한 번에 실행 취소됨.
        private void DialogueNumber(VisualElement parent, SerializedObject data, string path, string label,
            Func<float, float> normalize, int component = -1, Action changed = null)
        {
            var field = new FloatField(label) { name = "dialogue-" + path + (component < 0 ? "" : "-" + component), isDelayed = true };
            field.AddToClassList("studio-property");
            parent.Add(field);
            refreshFields.Add(Refresh);
            Refresh();
            field.RegisterValueChangedCallback(evt =>
            {
                if (!field.enabledInHierarchy || data.targetObject == null) { Refresh(); return; }
                if (!StudioPropertyEditing.IsFinite(evt.newValue)) { Refresh(); return; }
                data.UpdateIfRequiredOrScript();
                var property = StudioSerializedFields.Find(data, path);
                float value = normalize(evt.newValue);
                if (component < 0) property.floatValue = value;
                else
                {
                    var padding = property.vector2Value;
                    padding[component] = value;
                    property.vector2Value = padding;
                }
                changed?.Invoke();
                if (data.ApplyModifiedProperties()) host.Changed();
                Refresh();
            });
            void Refresh()
            {
                if (data.targetObject == null) return;
                data.UpdateIfRequiredOrScript();
                var property = StudioSerializedFields.Find(data, path);
                field.SetValueWithoutNotify(component < 0 ? property.floatValue : property.vector2Value[component]);
            }
        }
    }
}
