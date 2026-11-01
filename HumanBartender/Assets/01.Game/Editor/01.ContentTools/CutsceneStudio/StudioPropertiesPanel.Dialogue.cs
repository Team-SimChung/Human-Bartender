using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal sealed partial class StudioPropertiesPanel
    {
        private void BuildDialoguePrefab(VisualElement section, SerializedObject data)
        {
            var field = new ObjectField("프리팹") { name = "dialogue-prefab", objectType = typeof(GameObject), allowSceneObjects = false };
            field.SetValueWithoutNotify(host.Selected.DialoguePrefab);
            section.Add(field);
            var error = new HelpBox("DynamicSpeechBubble / 본문 TMP가 연결된 UI 프리팹을 선택하세요.", HelpBoxMessageType.Error);
            error.style.display = DisplayStyle.None; section.Add(error);
            field.RegisterValueChangedCallback(e =>
            {
                var prefab = e.newValue as GameObject;
                if (!StudioDialoguePrefabEditing.IsValid(prefab))
                {
                    error.style.display = DisplayStyle.Flex;
                    field.SetValueWithoutNotify(host.Selected.DialoguePrefab); return;
                }
                error.style.display = DisplayStyle.None;
                if (StudioDialoguePrefabEditing.Assign(data, prefab)) host.Changed(true);
            });
            Note(section, "비워 두면 기본 말풍선 / 지정하면 프리팹의 외형, 크기 계산, 글꼴과 여백을 사용합니다. 원본 프리팹은 수정하지 않습니다.");
            Note(section, "프리팹을 선택하면 스타일 직접 지정 / 배치 직접 지정이 꺼지고 프리팹 글꼴을 사용합니다. 필요하면 아래에서 다시 직접 지정할 수 있습니다.");
            Note(section, "연결을 해제하면 연결 전의 직접 지정 여부 / 대사 글꼴을 복원합니다. 대사 본문 / 화자 / 입력해 둔 스타일과 배치 값은 유지됩니다.");
        }

        private void BuildDialogueHeight(VisualElement section, SerializedObject data)
        {
            if (host.ActorIndex >= 0)
            {
                ClipField(section, data, "BubbleOffset").tooltip = "화자의 위치를 기준으로 말풍선 높이를 조절합니다.";
                Note(section, "화자를 따라가는 기준 높이입니다.");
            }
            else
            {
                DialogueNumber(section, data, "BubbleScreenHeight", "말풍선 높이", value => value);
                Note(section, "화자 없음 / 화면 중앙이 0이며 위쪽은 양수, 아래쪽은 음수입니다.");
            }
            if (StudioKeyframes.HasOverrides(host.Selected))
                Note(section, "이전 버전의 말풍선 속성 키는 재생 호환을 위해 유지됩니다.");
        }

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
            Note(section, host.Selected.DialoguePrefab != null ? "끄면 프리팹의 글자 크기 / 색상 / 정렬을 사용합니다. 직접 지정한 값은 유지됩니다." :
                $"클립 전체 설정 / 끄면 기본 크기 {StudioDialoguePresentation.DefaultFontSize} / 흰색 / 왼쪽 정렬을 사용하며 지정한 값은 유지됩니다.");
            Note(section, "글꼴을 비워 두면 프리팹 글꼴 / 시퀀스 글꼴 / TMP 기본 글꼴 순서로 사용합니다. 본문의 크기 / 색상 / 정렬 태그는 해당 구간에서 우선합니다.");
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
            Note(section, host.Selected.DialoguePrefab != null ? "끄면 프리팹의 폭 / 여백 / 크기 계산 방식을 사용합니다. 직접 지정한 폭과 여백은 프리팹 내부 좌표이며 원본 배율을 유지합니다." :
                $"클립 전체 설정 / 폭과 여백은 {StudioSequence.Width} × {StudioSequence.Height} 연출 영역 기준입니다. 끄면 폭 {StudioDialoguePresentation.DefaultWidth} / 가로 여백 {StudioDialoguePresentation.DefaultPadding.x} / 세로 여백 {StudioDialoguePresentation.DefaultPadding.y}를 사용합니다.");
            Note(section, host.Selected.DialoguePrefab != null ?
                "배치를 직접 지정하면 전체 본문에 맞춰 높이를 계산하고 타이핑 중 유지합니다. 배치 직접 지정을 끄면 프리팹의 크기 계산 방식을 따릅니다." :
                "스타일 또는 배치를 직접 지정하면 전체 본문에 맞춰 높이가 늘어납니다. 타이핑 중 높이는 유지됩니다.");
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
