using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 속성 패널의 바인딩과 선택 상태를 관리하며 편집은 기존 데이터에 직접 반영함.
    [Serializable]
    internal sealed partial class StudioPropertiesPanel
    {
        [NonSerialized] private IStudioPropertiesHost host;
        [NonSerialized] private ScrollView propertiesBody;
        [NonSerialized] private Label typeLabel;
        [NonSerialized] private VisualElement validationUI;
        [NonSerialized] private string inspectorIdentity, actorNamesIdentity, scrollKey;
        [NonSerialized] private int buildVersion;
        [NonSerialized] private readonly List<SerializedObject> panelBindings = new();
        [NonSerialized] private readonly List<Action> refreshFields = new();
        [NonSerialized] private Action refreshSelectedKeyAvailability;
        [NonSerialized] private readonly Dictionary<string, bool> foldouts = new();
        [NonSerialized] private readonly Dictionary<string, Vector2> scrollPositions = new();
        [NonSerialized] private readonly Dictionary<string, StudioClipField> clipFields = new();
        private StudioCurrentKeyEditing currentKeys => host.Session.CurrentKeys;

        internal void Initialize(IStudioPropertiesHost value) { host = value; }

        internal void ReleaseToolkitBindings()
        {
            propertiesBody?.Unbind();
            foreach (var binding in panelBindings)
                binding.Dispose();
            panelBindings.Clear();
            refreshFields.Clear();
            refreshSelectedKeyAvailability = null;
        }

        private SerializedObject PanelBinding(UnityEngine.Object target)
        {
            var binding = new SerializedObject(target);
            panelBindings.Add(binding);
            return binding;
        }

        private PropertyField BoundField(VisualElement parent, SerializedObject data, string path,
            string label, bool enabled = true, Action changed = null)
        {
            var property = StudioSerializedFields.Find(data, path);
            var field = new PropertyField(property, label) { name = "property-" + path };
            field.AddToClassList("studio-property");
            field.SetEnabled(enabled);
            var observed = property.contentHash;
            field.RegisterCallback<SerializedPropertyChangeEvent>(OnChanged);
            parent.Add(field);
            field.BindProperty(property);
            return field;

            void OnChanged(SerializedPropertyChangeEvent _)
            {
                if (host.Sequence == null || data.targetObject == null)
                    return;
                var current = StudioSerializedFields.Find(data, path);
                if (current == null || current.contentHash == observed)
                    return;
                observed = current.contentHash;
                changed?.Invoke();
                host.Changed(data.targetObject == host.Sequence);
            }
        }

        private PropertyField ClipField(VisualElement parent, SerializedObject data, string path,
            bool enabled = true, Action changed = null)
        {
            var field = clipFields[path];
            return BoundField(parent, data, path, field.Label, enabled && field.Enabled, changed);
        }

        private Foldout Section(string title, string id, bool open = true)
        {
            string key = (host.Selected != null ? host.Selected.Kind.ToString() : "sequence") + "-" + id;
            var section = StudioGUI.Section(foldouts, propertiesBody, title, key, open);
            section.focusable = false;
            var toggle = section.Q<Toggle>(className: Foldout.toggleUssClassName);
            if (toggle != null) { toggle.focusable = false; toggle.tabIndex = -1; }
            return section;
        }

        private static void Note(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.AddToClassList("studio-properties-note");
            parent.Add(label);
        }

        private static TextField ReadOnly(VisualElement parent, string label, string name)
        {
            var field = new TextField(label) { name = name, isReadOnly = true };
            field.AddToClassList("studio-properties-readonly");
            parent.Add(field);
            return field;
        }

        internal VisualElement CreateGUI()
        {
            ReleaseToolkitBindings();
            inspectorIdentity = null;
            var panel = StudioGUI.MakePanel("properties-panel", "속성", out propertiesBody);
            typeLabel = new Label { name = "properties-clip-type", pickingMode = PickingMode.Ignore };
            panel.Add(typeLabel);
            return panel;
        }

        internal void BuildPropertiesPanel()
        {
            if (scrollKey != null)
                scrollPositions[scrollKey] = propertiesBody.scrollOffset;
            ReleaseToolkitBindings();
            propertiesBody.Clear();
            clipFields.Clear();
            validationUI = null;
            int version = ++buildVersion;
            scrollKey = host.Selected != null ? host.Selected.Kind.ToString() : "sequence";
            typeLabel.text = host.Selected != null ? StudioClipSchema.Definition(host.Selected.Kind).Label : "시퀀스";
            if (host.Sequence == null)
            {
                typeLabel.text = "";
                Note(propertiesBody, "시퀀스를 만들거나 열어 속성을 편집하세요.");
                return;
            }
            var clip = host.FindSelected();
            if (clip == null)
                BuildSequenceFields();
            else
            {
                BuildTimingFields(clip);
                if (host.Selected.Kind != StudioKind.Dialogue && !StudioPropertiesLayout.HasCurrentEditor(host.Selected.Kind))
                    BuildSelectedKeyFields(clip);
                foreach (var field in StudioClipSchema.Fields(host.Selected))
                    clipFields.Add(field.Path, field);
                var data = PanelBinding(host.Selected);
                foreach (var section in StudioPropertiesLayout.Sections(host.Selected.Kind))
                    BuildSection(section, clip, data);
                var actionSection = Section("클립 복제 / 삭제", "clip-actions", false);
                var actions = new VisualElement();
                actions.AddToClassList("studio-inspector-actions");
                actions.Add(StudioGUI.ActionButton("클립 복제", host.DuplicateClip, "duplicate-clip"));
                actions.Add(StudioGUI.ActionButton("클립 삭제", host.DeleteClip, "delete-clip"));
                actionSection.Add(actions);
            }
            validationUI = new VisualElement { name = "sequence-issues" };
            propertiesBody.Add(validationUI);
            propertiesBody.schedule.Execute(RestoreScroll);

            void RestoreScroll()
            {
                if (version == buildVersion)
                    propertiesBody.scrollOffset = scrollPositions.TryGetValue(scrollKey, out var offset) ? offset : Vector2.zero;
            }
        }

        private void BuildTimingFields(TimelineClip clip)
        {
            bool wait = host.Selected.Kind == StudioKind.Wait;
            bool visual = host.Selected.Kind == StudioKind.Visual;
            var section = Section("클립 정보", "clip-info", wait || visual);
            var title = new TextField("클립 이름") { name = "clip-name", isDelayed = true, isReadOnly = visual };
            if (visual) title.AddToClassList("studio-properties-readonly");
            var start = new DoubleField(wait ? "대기 시작 (초)" : "시작 (초)") { name = "clip-start", isDelayed = true };
            var duration = new DoubleField(wait ? "클립 길이 (초)" : "길이 (초)") { name = "clip-duration", isDelayed = true };
            section.Add(title);
            section.Add(start);
            section.Add(duration);
            var end = ReadOnly(section, "종료 (초)", "clip-end");
            if (visual) BuildLinkedVisualSource(section);
            if (wait)
                Note(section, "대기 시작은 클립의 시작 시간입니다. 클립 길이 / 종료 시간은 타임라인 구간이며, 입력 대기 제한 시간이 아닙니다.");
            title.RegisterValueChangedCallback(ChangeName);
            start.RegisterValueChangedCallback(ChangeTime);
            duration.RegisterValueChangedCallback(ChangeTime);
            refreshFields.Add(Refresh);
            Refresh();

            void ChangeName(ChangeEvent<string> e)
            {
                if (visual) { Refresh(); return; }
                Undo.RecordObject(clip.GetParentTrack(), "클립 이름 변경");
                clip.displayName = e.newValue;
                host.Changed();
            }
            void ChangeTime(ChangeEvent<double> _)
            {
                if (StudioPropertyEditing.SetTiming(host.Sequence, clip, start.value, duration.value))
                    host.Changed();
                Refresh();
            }
            void Refresh()
            {
                title.SetValueWithoutNotify(visual ? host.Sequence.FindActor(host.Selected.ActorId)?.Name ?? clip.displayName : clip.displayName);
                start.SetValueWithoutNotify(clip.start);
                duration.SetValueWithoutNotify(clip.duration);
                end.SetValueWithoutNotify(clip.end.ToString("0.###"));
            }
        }

        private void BuildSequenceFields()
        {
            var section = Section("시퀀스 설정", "settings");
            var data = PanelBinding(host.Sequence);
            BoundField(section, data, "Id", "게임 호출 ID");
            BoundField(section, data, "BackgroundColor", "배경 색상");
            BoundField(section, data, "AllowSkip", "Esc 스킵 허용");
            var duration = new DoubleField("전체 길이 (초)") { name = "sequence-duration", isDelayed = true };
            duration.SetEnabled(host.Sequence.Timeline != null);
            section.Add(duration);
            duration.RegisterValueChangedCallback(ChangeDuration);
            refreshFields.Add(Refresh);
            Refresh();

            void ChangeDuration(ChangeEvent<double> e)
            {
                if (StudioPropertyEditing.IsFinite(e.newValue))
                {
                    Undo.RecordObject(host.Sequence.Timeline, "전체 길이 변경");
                    host.Sequence.Timeline.fixedDuration = Math.Max(.1, e.newValue);
                    host.Changed();
                }
                Refresh();
            }
            void Refresh() { duration.SetValueWithoutNotify(host.Sequence.Duration); }
        }

        private void BuildSelectedKeyFields(TimelineClip clip, VisualElement parent = null)
        {
            if (host.KeySelectionOwner != host.Selected || host.KeyIndex < 0)
                return;
            var keys = StudioKeyframes.GetKeys(host.Selected, host.KeyProperty, clip.duration);
            if (host.KeyIndex >= keys.Length)
                return;
            var section = parent ?? Section("선택한 키프레임", "selected-key");
            if (parent != null)
                Note(section, "선택한 키프레임");
            ReadOnly(section, "속성", "key-property").value = StudioPropertyPresentation.Label(host.Selected, host.KeyProperty);
            var time = new FloatField("클립 내 시간 (초)") { name = "key-time", isDelayed = true };
            var value = new FloatField("값") { name = "key-value", isDelayed = true };
            var toggle = new Toggle("값") { name = "key-boolean-value" };
            bool discrete = StudioKeyframes.IsDiscrete(host.KeyProperty);
            section.Add(time);
            var absolute = ReadOnly(section, "시퀀스 시간 (초)", "key-sequence-time");
            section.Add(discrete ? toggle : value);
            time.RegisterValueChangedCallback(ChangeTime);
            value.RegisterValueChangedCallback(ChangeValue);
            toggle.RegisterValueChangedCallback(e => UpdateKey(time.value, e.newValue ? 1 : 0));
            var delete = StudioGUI.ActionButton("선택 키 삭제", DeleteKey, "delete-selected-key");
            section.Add(delete);
            refreshSelectedKeyAvailability = RefreshAvailability;
            RefreshAvailability();
            refreshFields.Add(Refresh);
            Refresh();

            bool CanEdit() => !host.Playing;
            void RefreshAvailability()
            {
                bool enabled = CanEdit();
                time.SetEnabled(enabled);
                value.SetEnabled(enabled);
                toggle.SetEnabled(enabled);
                delete.SetEnabled(enabled);
            }
            void DeleteKey()
            {
                if (!CanEdit()) return;
                StudioKeyframeEditing.Delete(clip, host.KeyProperty, host.KeyIndex);
                host.Session.ClearKey();
                host.Changed();
            }
            void ChangeTime(ChangeEvent<float> e) { UpdateKey((float)host.Snap(e.newValue), discrete ? (toggle.value ? 1 : 0) : value.value); }
            void ChangeValue(ChangeEvent<float> e) { UpdateKey(time.value, e.newValue); }
            void UpdateKey(float nextTime, float nextValue)
            {
                if (CanEdit() && StudioPropertyEditing.IsFinite(nextTime) && StudioPropertyEditing.IsFinite(nextValue))
                {
                    host.Session.UpdateKeyIndex(StudioKeyframeEditing.Set(clip, host.KeyProperty, host.KeyIndex, nextTime, nextValue));
                    var updated = StudioKeyframes.GetKeys(host.Selected, host.KeyProperty, clip.duration);
                    host.Seek(clip.start + updated[host.KeyIndex].Time);
                    host.Changed();
                }
                Refresh();
            }
            void Refresh()
            {
                var current = StudioKeyframes.GetKeys(host.Selected, host.KeyProperty, clip.duration);
                if (host.KeyIndex < 0 || host.KeyIndex >= current.Length)
                    return;
                time.SetValueWithoutNotify(current[host.KeyIndex].Time);
                value.SetValueWithoutNotify(current[host.KeyIndex].Value);
                toggle.SetValueWithoutNotify(current[host.KeyIndex].Value >= .5f);
                toggle.text = StudioPropertyPresentation.Value(host.KeyProperty, current[host.KeyIndex].Value);
                absolute.SetValueWithoutNotify((clip.start + current[host.KeyIndex].Time).ToString("0.###"));
            }
        }

        internal void SyncTimingFields()
        {
            // 재생 잠금은 텍스트 입력 중에도 즉시 갱신하며, 데이터 변경 직전에도 같은 상태를 확인함.
            refreshSelectedKeyAvailability?.Invoke();
            // 편집 중인 문자열을 보존하고 외부 편집과 실행 취소 결과를 갱신함.
            if (host.NativeTextEditing())
                return;
            foreach (var refresh in refreshFields)
                refresh();
        }

        internal void RefreshValidation()
        {
            if (validationUI == null || host.Sequence == null)
                return;
            validationUI.Clear();
            foreach (string issue in host.Sequence.Validate())
                validationUI.Add(new HelpBox(issue, HelpBoxMessageType.Warning));
        }

        internal void ResetForSequence()
        {
            currentKeys.Reset();
            inspectorIdentity = null;
            scrollKey = null;
            scrollPositions.Clear();
        }

        private string Identity()
        {
            var clip = host.Selected;
            var identity = new StringBuilder();
            identity.Append(host.Sequence?.GetInstanceID()).Append(':').Append(clip?.GetInstanceID())
                .Append(':').Append(clip?.Kind).Append(':').Append(clip?.ActorId).Append(':').Append(host.ActorIndex)
                .Append(':').Append(host.KeySelectionOwner?.GetInstanceID()).Append(':').Append(host.KeyIndex)
                .Append(':').Append(host.KeyProperty)
                .Append(':').Append(clip?.Effect).Append(':').Append(clip?.WaitForInput)
                .Append(':').Append(clip?.Frames?.Length);
            identity.Append(':').Append(clip?.DialoguePrefab?.GetInstanceID()).Append(':').Append(clip?.CameraTargetId);
            if (clip != null)
                identity.Append(':').Append(StudioPropertyEditing.UsesSourceTiming(clip))
                    .Append(':').Append(StudioKeyframes.HasOverrides(clip))
                    .Append(':').Append(StudioKeyframes.HasKeys(clip, StudioProperty.Strength))
                    .Append(':').Append(StudioKeyframes.HasKeys(clip, StudioProperty.Volume));
            if (host.Sequence != null)
                foreach (var actor in host.Sequence.Actors)
                    identity.Append('|').Append(actor?.Id);
            return identity.ToString();
        }

        private string ActorNamesIdentity()
        {
            var names = new StringBuilder();
            if (host.Sequence != null)
                foreach (var actor in host.Sequence.Actors)
                    names.Append('|').Append(actor?.Name);
            return names.ToString();
        }

        internal void Sync(bool contentChanged)
        {
            if (propertiesBody == null)
                return;
            currentKeys.Observe(host.FindSelected(), host.CurrentTime);
            if (host.Playing) currentKeys.ClearPending();
            string identity = Identity();
            string names = ActorNamesIdentity();
            // 오브젝트 이름을 입력하는 동안은 바인딩을 유지하고 입력 완료 후 목록을 갱신함.
            if (inspectorIdentity != identity || actorNamesIdentity != names && !host.NativeTextEditing())
            {
                inspectorIdentity = identity;
                actorNamesIdentity = names;
                BuildPropertiesPanel();
                contentChanged = true;
            }
            SyncTimingFields();
            if (contentChanged)
                RefreshValidation();
        }
    }
}
