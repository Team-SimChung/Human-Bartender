using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using System.Linq;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.IO;
using HumanBartender.CutsceneStudio;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 패널의 표시와 입력을 처리하며 다른 패널은 호스트 계약으로 연결함.
    [Serializable]
    internal sealed class StudioPropertiesPanel
    {
        [NonSerialized] private IStudioPropertiesHost host;
        private CutsceneStudioWindow Window => host.Window;
        internal void Initialize(IStudioPropertiesHost value) { host = value; }
        [SerializeField]
        private bool timingOpen = true, objectPropertiesOpen = true, sequencePropertiesOpen = true;

        [NonSerialized] private DoubleField clipStart, clipDuration, sequenceDuration;

        [NonSerialized] private FloatField selectedKeyTime, selectedKeyValue;

        [NonSerialized] private string inspectorIdentity;

        [NonSerialized] private readonly List<SerializedObject> panelBindings = new();

        internal void ReleaseToolkitBindings()
        {
            propertiesBody?.Unbind();
            foreach (var binding in panelBindings)
                binding.Dispose();
            panelBindings.Clear();
        }


        private SerializedObject PanelBinding(UnityEngine.Object target)
        {
            var binding = new SerializedObject(target);
            panelBindings.Add(binding);
            return binding;
        }


        private PropertyField BoundField(VisualElement parent, SerializedObject data, string path, string label, bool enabled = true, Action changed = null)
        {
            var field = new PropertyField(StudioSerializedFields.Find(data, path), label)
            {
                name = "property-" + path
            };
            field.AddToClassList("studio-property");
            field.SetEnabled(enabled);
            // 바인딩의 초기 알림은 무시하고 실제 값이 달라졌을 때만 편집으로 처리함.
            var observed = StudioSerializedFields.Find(data, path).contentHash;
            field.RegisterCallback<SerializedPropertyChangeEvent>(NotifyBoundPropertyChanged);
            parent.Add(field);
            field.BindProperty(StudioSerializedFields.Find(data, path));
            return field;
            void NotifyBoundPropertyChanged(UnityEditor.UIElements.SerializedPropertyChangeEvent _)
            {
                if (host.Sequence == null || data.targetObject == null)
                    return;
                var property = StudioSerializedFields.Find(data, path);
                if (property == null || property.contentHash == observed)
                    return;
                observed = property.contentHash;
                changed?.Invoke();
                host.Changed(data.targetObject == host.Sequence);
            }
        }


        internal void BuildPropertiesPanel()
        {
            var scroll = propertiesBody.scrollOffset;
            ReleaseToolkitBindings();
            propertiesBody.Clear();
            clipTitle = null;
            clipStart = clipDuration = sequenceDuration = null;
            selectedKeyTime = selectedKeyValue = null;
            validationUI = null;
            if (host.Sequence == null)
                return;
            var clip = host.FindSelected();
            if (clip != null)
            {
                BuildSelectedKeyFields(clip);
                var timing = StudioGUI.Section(foldouts, propertiesBody, "클립 정보", "clip-info", timingOpen, StoreTimingFoldout);
                clipTitle = new TextField("클립 이름")
                {
                    name = "clip-name",
                    value = clip.displayName,
                    isDelayed = true
                };
                clipStart = new DoubleField("시작 (초)")
                {
                    name = "clip-start",
                    value = clip.start,
                    isDelayed = true
                };
                clipDuration = new DoubleField("길이 (초)")
                {
                    name = "clip-duration",
                    value = clip.duration,
                    isDelayed = true
                };
                void TimingChanged()
                {
                    Undo.RecordObject(clip.GetParentTrack(), "클립 시간 변경");
                    Undo.RecordObject(host.Sequence.Timeline, "컷씬 길이 변경");
                    clip.displayName = clipTitle.value;
                    clip.start = Math.Max(0, clipStart.value);
                    clip.duration = Math.Max(StudioTiming.FrameDuration(host.Sequence), clipDuration.value);
                    host.Sequence.Timeline.fixedDuration = Math.Max(host.Sequence.Duration, clip.end);
                    host.Changed();
                }

                clipTitle.RegisterValueChangedCallback(ChangeClipName);
                clipStart.RegisterValueChangedCallback(ChangeClipStart);
                clipDuration.RegisterValueChangedCallback(ChangeClipDuration);
                timing.Add(clipTitle);
                timing.Add(clipStart);
                timing.Add(clipDuration);
                BuildClipFields(PanelBinding(host.Selected));
                if (StudioEvaluation.OwnsActor(host.Selected) && host.ActorIndex >= 0 && host.ActorIndex < host.Sequence.Actors.Count)
                    BuildActorFields(false);
                var buttons = new VisualElement();
                buttons.AddToClassList("studio-inspector-actions");
                buttons.Add(StudioGUI.ActionButton("복제", host.DuplicateClip, "duplicate-clip"));
                buttons.Add(StudioGUI.ActionButton("삭제", host.DeleteClip, "delete-clip"));
                propertiesBody.Add(buttons);
                void StoreTimingFoldout(bool v)
                {
                    timingOpen = v;
                }

                void ChangeClipName(ChangeEvent<string> _)
                {
                    TimingChanged();
                }

                void ChangeClipStart(ChangeEvent<double> _)
                {
                    TimingChanged();
                }

                void ChangeClipDuration(ChangeEvent<double> _)
                {
                    TimingChanged();
                }
            }
            else if (host.ActorIndex >= 0 && host.ActorIndex < host.Sequence.Actors.Count)
                BuildActorFields(true);
            else
            {
                var data = PanelBinding(host.Sequence);
                var settings = StudioGUI.Section(foldouts, propertiesBody, "시퀀스 설정", "sequence-properties", sequencePropertiesOpen, StoreSequencePropertiesFoldout);
                BoundField(settings, data, "Id", "게임 호출 ID");
                BoundField(settings, data, "BackgroundColor", "배경 색상");
                BoundField(settings, data, "AllowSkip", "Esc 스킵 허용");
                sequenceDuration = new DoubleField("전체 길이 (초)")
                {
                    name = "sequence-duration",
                    value = host.Sequence.Duration,
                    isDelayed = true
                };
                sequenceDuration.RegisterValueChangedCallback(ChangeSequenceDuration);
                settings.Add(sequenceDuration);
                void StoreSequencePropertiesFoldout(bool v)
                {
                    sequencePropertiesOpen = v;
                }

                void ChangeSequenceDuration(ChangeEvent<double> e)
                {
                    Undo.RecordObject(host.Sequence.Timeline, "전체 길이 변경");
                    host.Sequence.Timeline.fixedDuration = Math.Max(.1, e.newValue);
                    host.Changed();
                }
            }

            validationUI = new VisualElement
            {
                name = "sequence-issues"
            };
            propertiesBody.Add(validationUI);
            propertiesBody.scrollOffset = scroll;
        }


        private void BuildClipFields(SerializedObject data)
        {
            var clip = host.Selected;
            var section = StudioGUI.Section(foldouts, propertiesBody, StudioEditorAssets.Label(clip.Kind) + " 설정", "clip-settings", true);
            Foldout animation = null;
            foreach (var field in StudioClipSchema.Fields(clip))
            {
                if (field.Animation)
                    animation ??= StudioGUI.Section(foldouts, propertiesBody, "애니메이션", "clip-animation", true);
                BoundField(field.Animation ? animation : section, data, field.Path, field.Label, field.Enabled, UpdateClipTarget);
            }

            foreach (string message in StudioClipSchema.Help(clip))
                section.Add(new HelpBox(message, HelpBoxMessageType.Info));
            void UpdateClipTarget()
            {
                host.Session.ValidateSelection();
            }
        }


        private void BuildActorFields(bool standalone)
        {
            var actor = host.Sequence.Actors[host.ActorIndex];
            var clip = host.Selected;
            var data = PanelBinding(host.Sequence);
            string path = $"Actors.Array.data[{host.ActorIndex}].";
            var section = StudioGUI.Section(foldouts, propertiesBody, "오브젝트", "object-properties", objectPropertiesOpen, StoreObjectPropertiesFoldout);
            if (standalone)
            {
                string oldId = actor.Id;
                BoundField(section, data, path + "Id", "캐릭터 ID", changed: UpdateActorReferences);
                void UpdateActorReferences()
                {
                    if (oldId == actor.Id)
                        return;
                    foreach (var entry in StudioEvaluation.Clips(host.Sequence, true).Where(ReferencesOldActor))
                    {
                        Undo.RecordObject(entry.Asset, "캐릭터 연결 변경");
                        entry.Asset.ActorId = actor.Id;
                    }

                    oldId = actor.Id;
                    bool ReferencesOldActor(StudioEntry e)
                    {
                        return e.Asset.ActorId == oldId;
                    }
                }
            }

            BoundField(section, data, path + "Name", "이름");
            var oldSprite = actor.Sprite;
            BoundField(section, data, path + "Sprite", "이미지", changed: UpdateActorSprite);
            if (standalone)
                BoundField(section, data, path + "Position", "기본 위치");
            BoundField(section, data, path + "Size", "기본 크기");
            BoundField(section, data, path + "Color", "색상");
            BoundField(section, data, path + "Layer", "그리기 순서");
            if (!standalone)
                return;
            BoundField(section, data, path + "ClipControlled", "표시 클립 구간에만 보이기");
            section.Add(StudioGUI.ActionButton("캐릭터와 연결된 클립 삭제", DeleteActorAndClips));
            void StoreObjectPropertiesFoldout(bool v)
            {
                objectPropertiesOpen = v;
            }

            void UpdateActorSprite()
            {
                if (clip != null && oldSprite != actor.Sprite)
                {
                    Undo.RecordObject(clip, "오브젝트 이미지 변경");
                    clip.Frames = new[]
                    {
                        actor.Sprite
                    };
                    clip.FrameTimes = null;
                    clip.AnimationLength = 0;
                    EditorUtility.SetDirty(clip);
                }

                oldSprite = actor.Sprite;
            }

            void DeleteActorAndClips()
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.RecordObject(host.Sequence, "캐릭터 삭제");
                foreach (var entry in StudioEvaluation.Clips(host.Sequence, true).Where(ReferencesActor).ToArray())
                    StudioEditorAssets.DeleteClip(host.Sequence, entry.Clip);
                host.Sequence.EditableActors.Remove(actor);
                host.Session.ClearSelection();
                Undo.CollapseUndoOperations(group);
                host.Changed(true);
                bool ReferencesActor(StudioEntry e)
                {
                    return e.Asset.ActorId == actor.Id;
                }
            }
        }


        private void BuildSelectedKeyFields(TimelineClip clip)
        {
            if (!host.KeyframeMode || host.KeySelectionOwner != host.Selected || host.KeyIndex < 0)
                return;
            var keys = StudioKeyframes.GetKeys(host.Selected, host.KeyProperty, clip.duration);
            if (host.KeyIndex >= keys.Length)
                return;
            var section = StudioGUI.Section(foldouts, propertiesBody, StudioTimelinePanel.PropertyLabel(host.Selected, host.KeyProperty) + " 키프레임", "selected-key", true);
            selectedKeyTime = new FloatField("클립 내 시간 (초)")
            {
                name = "key-time",
                isDelayed = true,
                value = keys[host.KeyIndex].Time
            };
            selectedKeyValue = new FloatField("값")
            {
                name = "key-value",
                isDelayed = true,
                value = keys[host.KeyIndex].Value
            };
            void UpdateKey()
            {
                host.Session.UpdateKeyIndex(StudioKeyframeEditing.Set(clip, host.KeyProperty, host.KeyIndex, (float)host.Snap(selectedKeyTime.value), selectedKeyValue.value));
                host.Seek(clip.start + StudioKeyframes.GetKeys(host.Selected, host.KeyProperty, clip.duration)[host.KeyIndex].Time);
                host.Changed();
            }

            selectedKeyTime.RegisterValueChangedCallback(ChangeKeyTime);
            selectedKeyValue.RegisterValueChangedCallback(ChangeKeyValue);
            section.Add(selectedKeyTime);
            section.Add(selectedKeyValue);
            section.Add(StudioGUI.ActionButton("선택 키 삭제", host.DeleteSelectedKey));
            void ChangeKeyTime(ChangeEvent<float> _)
            {
                UpdateKey();
            }

            void ChangeKeyValue(ChangeEvent<float> _)
            {
                UpdateKey();
            }
        }


        internal void SyncTimingFields()
        {
            // 입력 중인 값은 유지하고 포커스가 없는 시간 필드만 타임라인에 맞추어 갱신함.
            if (host.NativeTextEditing())
                return;
            var clip = host.FindSelected();
            if (clip != null)
            {
                clipTitle?.SetValueWithoutNotify(clip.displayName);
                clipStart?.SetValueWithoutNotify(clip.start);
                clipDuration?.SetValueWithoutNotify(clip.duration);
                if (selectedKeyTime != null && selectedKeyValue != null && host.KeyIndex >= 0)
                {
                    var keys = StudioKeyframes.GetKeys(host.Selected, host.KeyProperty, clip.duration);
                    if (host.KeyIndex < keys.Length)
                    {
                        selectedKeyTime.SetValueWithoutNotify(keys[host.KeyIndex].Time);
                        selectedKeyValue.SetValueWithoutNotify(keys[host.KeyIndex].Value);
                    }
                }
            }

            if (host.Sequence != null)
                sequenceDuration?.SetValueWithoutNotify(host.Sequence.Duration);
        }


        internal void RefreshValidation()
        {
            if (validationUI == null || host.Sequence == null)
                return;
            validationUI.Clear();
            foreach (string issue in host.Sequence.Validate())
                validationUI.Add(new HelpBox(issue, HelpBoxMessageType.Warning));
        }

        [NonSerialized] private ScrollView propertiesBody;
        [NonSerialized] private TextField clipTitle;
        [NonSerialized] private VisualElement validationUI;
        [NonSerialized] private readonly Dictionary<string, bool> foldouts = new();

        internal VisualElement CreateGUI()
        {
            ReleaseToolkitBindings();
            inspectorIdentity = null;
            return StudioGUI.MakePanel("properties-panel", "속성", out propertiesBody);
        }

        internal void ResetForSequence() { inspectorIdentity = null; }

        internal void Sync(bool contentChanged)
        {
            if (propertiesBody == null)
                return;
            string identity = $"{host.Sequence?.GetInstanceID()}:{host.Selected?.GetInstanceID()}:{host.ActorIndex}:{host.KeyframeMode}:{host.KeyIndex}:{host.KeyProperty}:{host.KeySelectionOwner?.GetInstanceID()}:{host.Selected?.StateCamera}:{(host.Selected != null && StudioKeyframes.HasOverrides(host.Selected))}";
            if (inspectorIdentity != identity)
            {
                inspectorIdentity = identity;
                BuildPropertiesPanel();
                contentChanged = true;
            }
            SyncTimingFields();
            if (contentChanged)
                RefreshValidation();
        }

    }
}
