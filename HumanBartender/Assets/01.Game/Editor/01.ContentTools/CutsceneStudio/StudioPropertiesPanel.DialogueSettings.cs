using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal sealed partial class StudioPropertiesPanel
    {
        [NonSerialized] private string dialogueTimingNotice = "";

        private void ChangeDialogueTiming(StudioSequence sequence, StudioClip target, Action mutation, string label, bool fitOnly = false)
        {
            if (host.Playing || sequence == null || host.Sequence != sequence) return;
            bool changed = StudioDialogueEditing.Edit(sequence, target, mutation, label,
                out dialogueTimingNotice, fitOnly);
            if (changed) host.Changed();
            // 늦게 도착한 이전 패널의 이벤트도 원래 대상을 편집한다. 새 선택 표시는 다음 Sync에 맡긴다.
            if (host.Selected != target) return;
            foreach (var binding in panelBindings) binding.Update();
            foreach (var refresh in refreshFields) refresh();
            RefreshValidation();
        }

        private void BuildDialogueText(VisualElement section)
        {
            var sequence = host.Sequence;
            var target = host.Selected;
            var field = new TextField("본문") { name = "dialogue-text", multiline = true, isDelayed = true };
            field.AddToClassList("studio-properties-dialogue"); section.Add(field);
            field.RegisterValueChangedCallback(e => ChangeDialogueTiming(sequence, target, () => target.Text = e.newValue, "대사 내용·시간 변경"));
            refreshFields.Add(Refresh); Refresh();
            void Refresh() { field.SetValueWithoutNotify(target.Text); field.SetEnabled(!host.Playing); }
        }

        private void BuildDialogueLength(VisualElement section)
        {
            var sequence = host.Sequence;
            var target = host.Selected;
            var mode = new PopupField<string>("길이 조정", new System.Collections.Generic.List<string>
                { "수동", "자동 — 이 대사만", "자동 — 뒤 클립도 이동" }, (int)target.DialogueLengthMode);
            mode.name = "dialogue-length-mode"; section.Add(mode);
            mode.RegisterValueChangedCallback(_ => ChangeDialogueTiming(sequence, target, () => target.DialogueLengthMode = (StudioDialogueLengthMode)mode.index, "대사 길이 조정 방식 변경"));
            TimingFloat(section, "글자 / 초", sequence, target, () => target.CharactersPerSecond, v => target.CharactersPerSecond = v, .01f);
            refreshFields.Add(Refresh); Refresh();
            void Refresh()
            {
                mode.SetValueWithoutNotify(mode.choices[(int)target.DialogueLengthMode]);
                mode.SetEnabled(!host.Playing);
            }
            Note(section, "편집 확정 시 적용합니다. 뒤 클립 이동은 이전 끝 이후에 시작하는 모든 트랙이 대상입니다. 경계를 가로지르는 긴 배경·음원과 키프레임 시간은 유지합니다. 잠긴 대상이 있으면 전체 편집을 취소합니다.");
        }

        private void TimingFloat(VisualElement section, string label, StudioSequence sequence, StudioClip target, Func<float> read, Action<float> write, float minimum = 0)
        {
            var field = new FloatField(label) { isDelayed = true }; section.Add(field);
            field.RegisterValueChangedCallback(e =>
            {
                if (!StudioPropertyEditing.IsFinite(e.newValue)) { Refresh(); return; }
                ChangeDialogueTiming(sequence, target, () => write(Math.Max(minimum, e.newValue)), "타이핑 설정·대사 시간 변경");
            });
            refreshFields.Add(Refresh); Refresh();
            void Refresh() { field.SetValueWithoutNotify(read()); field.SetEnabled(!host.Playing); }
        }

        private void BuildTypingRhythm(VisualElement section)
        {
            var sequence = host.Sequence;
            var rhythm = sequence.TypingRhythm;
            var group = new Foldout { text = "타이핑 리듬", value = false }; section.Add(group);
            TimingFloat(group, "마침표 뒤 휴식 (초)", sequence, null, () => rhythm.PeriodPause, v => rhythm.PeriodPause = v);
            TimingFloat(group, "연속 마침표 간격 (초)", sequence, null, () => rhythm.RepeatedPeriodPause, v => rhythm.RepeatedPeriodPause = v);
            var extra = new TextField("추가 문장부호") { isDelayed = true }; group.Add(extra);
            extra.RegisterValueChangedCallback(e => ChangeDialogueTiming(sequence, null, () => rhythm.ExtraPunctuation = e.newValue, "문장부호·대사 시간 변경"));
            refreshFields.Add(Refresh); Refresh();
            void Refresh() { extra.SetValueWithoutNotify(rhythm.ExtraPunctuation); extra.SetEnabled(!host.Playing); }
            TimingFloat(group, "추가 휴식 (초)", sequence, null, () => rhythm.ExtraPause, v => rhythm.ExtraPause = v);
            Note(group, "자동 길이 조정을 켠 대사들만 시작 순서대로 다시 계산합니다. 수동 대사의 길이는 유지합니다.");
        }

        private void BuildTypingAudio(VisualElement section, SerializedObject data, bool sequence)
        {
            var settings = new UnityEditor.UIElements.PropertyField(StudioSerializedFields.Find(data, "TypingAudio"), sequence ? "공통 타이핑 효과음" : "직접 지정 설정");
            if (!sequence) BoundField(section, data, "TypingAudioMode", "타이핑 효과음", changed: Refresh);
            section.Add(settings); settings.Bind(data);
            settings.RegisterValueChangeCallback(_ => host.Changed());
            refreshFields.Add(Refresh); Refresh();
            void Refresh()
            {
                settings.style.display = sequence || host.Selected.TypingAudioMode == StudioTypingAudioMode.Custom ? DisplayStyle.Flex : DisplayStyle.None;
                settings.SetEnabled(!host.Playing);
            }
            Note(section, "음원이 없거나 사용을 끄면 무음입니다. 전체 표시·탐색 시 지나간 소리를 몰아 재생하지 않습니다. 입력 대기 반복은 별도 옵션이며 설정창에서는 모두 멈춥니다.");
        }
    }
}
