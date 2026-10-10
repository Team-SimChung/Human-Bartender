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
    internal sealed class StudioDialoguePanel
    {
        [NonSerialized] private IStudioDialogueHost host;
        private CutsceneStudioWindow Window => host.Window;
        internal void Initialize(IStudioDialogueHost value) { host = value; }
        private bool dialogueMode { get { return host.ViewState.Dialogue; } set { host.ViewState.Dialogue = value; } }

        [NonSerialized] private VisualElement dialoguePanel;

        [NonSerialized] private ListView dialogueList, dialogueNavigator;

        [NonSerialized] private Label dialogueCount, dialogueEmpty;

        [NonSerialized] private StudioSequence dialogueSequence;

        [NonSerialized] private StudioEntry[] dialogueEntries = Array.Empty<StudioEntry>();

        private sealed class DialogueRow
        {
            public StudioEntry Entry;
            public int Index;
            public VisualElement Root;
            public Button Time;
            public PopupField<string> Speaker;
            public TextField Text;
            public Toggle Wait;
        }


        internal void SetDialogueMode()
        {
            if (dialogueMode)
                return;
            host.ReleaseKeyDrag();
            host.ReleaseTimelineResize();
            host.ReleaseTimelineScrub();
            host.KeyframeMode = false;
            dialogueMode = true;
            host.Session.ClearKey();
            if (Event.current != null)
                GUI.FocusControl(null);
            host.PositionToolkitPanels();
            SyncDialoguePanel();
            Window.Repaint();
        }


        internal void BuildDialoguePanel()
        {
            dialoguePanel = new VisualElement
            {
                name = "dialogue-panel"
            };
            dialoguePanel.AddToClassList("studio-dialogue-panel");
            dialoguePanel.RegisterCallback<PointerDownEvent>(ReleasePreviewToolFocus, TrickleDown.TrickleDown);
            var toolbar = new VisualElement();
            toolbar.AddToClassList("studio-dialogue-toolbar");
            dialogueCount = new Label
            {
                name = "dialogue-count"
            };
            toolbar.Add(dialogueCount);
            toolbar.Add(StudioGUI.ActionButton("＋ 대사 추가", AppendEmptyDialogue, "dialogue-add"));
            toolbar.Add(StudioGUI.ActionButton("대사 붙여넣기", OpenDialoguePaste, "dialogue-paste"));
            var hint = new Label("타임라인의 대사 클립과 함께 변경됩니다.");
            hint.AddToClassList("studio-dialogue-hint");
            toolbar.Add(hint);
            dialoguePanel.Add(toolbar);
            dialogueEmpty = new Label("대사가 없습니다. ‘대사 추가’ 또는 ‘대사 붙여넣기’로 시작하세요.");
            dialogueEmpty.AddToClassList("studio-dialogue-empty");
            dialoguePanel.Add(dialogueEmpty);
            dialogueList = new ListView
            {
                name = "dialogue-list",
                fixedItemHeight = 112,
                selectionType = SelectionType.None,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                makeItem = MakeDialogueRow,
                bindItem = BindDialogueRow,
                unbindItem = UnbindDialogueRow
            };
            dialogueList.style.flexGrow = 1;
            dialogueList.style.minHeight = 0;
            var body = new VisualElement();
            body.AddToClassList("studio-dialogue-body");
            dialogueNavigator = new ListView
            {
                name = "dialogue-navigator",
                fixedItemHeight = 52,
                selectionType = SelectionType.None,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                makeItem = MakeDialogueNavigationItem,
                bindItem = BindNavigationItem,
                unbindItem = UnbindNavigationItem
            };
            dialogueNavigator.AddToClassList("studio-dialogue-navigator");
            body.Add(dialogueNavigator);
            body.Add(dialogueList);
            dialoguePanel.Add(body);
            dialogueSequence = null;
            dialogueEntries = Array.Empty<StudioEntry>();
            Window.rootVisualElement.Add(dialoguePanel);
            void ReleasePreviewToolFocus(PointerDownEvent _)
            {
                host.PreviewToolFocus = false;
            }

            void AppendEmptyDialogue()
            {
                AppendDialogueLines(host.Sequence, new[] { "대사를 입력하세요." }, "", 3);
            }

            void OpenDialoguePaste()
            {
                StudioDialoguePasteWindow.Open(Window, host.Sequence);
            }

            void BindDialogueRow(VisualElement element, int index)
            {
                var row = (DialogueRow)element.userData;
                row.Entry = dialogueEntries[index];
                row.Index = index;
                SyncDialogueRow(row);
            }

            void UnbindDialogueRow(VisualElement element, int _)
            {
                ((DialogueRow)element.userData).Entry = default;
            }

            void BindNavigationItem(VisualElement element, int index)
            {
                element.userData = dialogueEntries[index];
                SyncDialogueNavigationItem(element, index);
            }

            void UnbindNavigationItem(VisualElement element, int _)
            {
                element.userData = null;
            }
        }


        internal void PositionDialoguePanel()
        {
            if (dialoguePanel == null)
                return;
            dialoguePanel.style.left = 0;
            dialoguePanel.style.top = Window.position.height - host.TimelineHeight + host.TimelineToolbarHeight;
            dialoguePanel.style.width = Mathf.Max(0, Window.position.width - host.Inspector - 3);
            dialoguePanel.style.height = Mathf.Max(0, host.TimelineHeight - host.TimelineToolbarHeight - 27);
            dialoguePanel.EnableInClassList("compact", Window.position.width - host.Inspector < 1000);
            dialoguePanel.style.display = dialogueMode && host.Sequence != null && host.Sequence.Timeline != null && !EditorApplication.isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
        }


        internal void SyncDialoguePanel()
        {
            if (dialoguePanel == null || !dialogueMode)
                return;
            var entries = StudioEvaluation.Clips(host.Sequence, true).Where(IsDialogue).ToArray();
            bool rebuild = dialogueSequence != host.Sequence || !entries.Select(GetTimelineClip).SequenceEqual(dialogueEntries.Select(GetPreviousTimelineClip));
            dialogueSequence = host.Sequence;
            dialogueEntries = entries;
            dialogueCount.text = "대사 " + entries.Length + "개";
            dialogueEmpty.style.display = entries.Length == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (rebuild)
            {
                dialogueList.itemsSource = dialogueEntries;
                dialogueList.RefreshItems();
                dialogueNavigator.itemsSource = dialogueEntries;
                dialogueNavigator.RefreshItems();
            }

            // 표시 중인 행만 갱신해 입력 위치와 텍스트 선택을 유지함.
            dialogueList.Query<VisualElement>(className: "studio-dialogue-row").ForEach(RefreshDialogueRow);
            dialogueNavigator.Query<VisualElement>(className: "studio-dialogue-navigation-item").ForEach(RefreshNavigationItem);
            bool IsDialogue(StudioEntry e)
            {
                return e.Asset.Kind == StudioKind.Dialogue;
            }

            TimelineClip GetTimelineClip(StudioEntry e)
            {
                return e.Clip;
            }

            TimelineClip GetPreviousTimelineClip(StudioEntry e)
            {
                return e.Clip;
            }

            void RefreshDialogueRow(VisualElement element)
            {
                SyncDialogueRow((DialogueRow)element.userData);
            }

            void RefreshNavigationItem(VisualElement element)
            {
                if (element.userData is StudioEntry entry)
                    SyncDialogueNavigationItem(element, Array.FindIndex(dialogueEntries, IsNavigationClip));
                bool IsNavigationClip(StudioEntry e)
                {
                    return e.Clip == entry.Clip;
                }
            }
        }


        private VisualElement MakeDialogueNavigationItem()
        {
            var item = new Button
            {
                name = "dialogue-navigation-item"
            };
            item.AddToClassList("studio-dialogue-navigation-item");
            item.Add(new Label { name = "dialogue-navigation-heading", pickingMode = PickingMode.Ignore });
            item.Add(new Label { name = "dialogue-navigation-summary", pickingMode = PickingMode.Ignore });
            item.clicked += NavigateToDialogue;
            return item;
            void NavigateToDialogue()
            {
                if (item.userData is StudioEntry entry)
                    NavigateDialogue(entry.Clip);
            }
        }


        private void NavigateDialogue(TimelineClip clip)
        {
            int index = Array.FindIndex(dialogueEntries, IsRequestedClip);
            if (index < 0)
                return;
            host.Select(clip, false);
            dialogueList.ScrollToItem(index);
            SyncDialoguePanel();
            bool IsRequestedClip(StudioEntry e)
            {
                return e.Clip == clip;
            }
        }


        private void SyncDialogueNavigationItem(VisualElement item, int index)
        {
            if (index < 0 || index >= dialogueEntries.Length)
                return;
            var entry = dialogueEntries[index];
            string speaker = DialogueSpeakerName(entry.Asset.ActorId);
            string text = (entry.Asset.Text ?? "").Replace('\r', ' ').Replace('\n', ' ');
            item.Q<Label>("dialogue-navigation-heading").text = (index + 1).ToString("00") + ".  " + entry.Clip.start.ToString("F1") + "초  " + speaker;
            item.Q<Label>("dialogue-navigation-summary").text = text;
            item.tooltip = speaker + " : " + text;
            item.EnableInClassList("selected", entry.Asset == host.Selected);
        }


        private VisualElement MakeDialogueRow()
        {
            var row = new DialogueRow
            {
                Root = new VisualElement()
            };
            row.Root.userData = row;
            row.Root.AddToClassList("studio-dialogue-row");
            row.Time = new Button(SelectDialogueRow)
            {
                name = "dialogue-time",
                tooltip = "이 대사의 시작 위치 미리보기 / 시간과 길이는 속성 패널에서 편집"
            };
            row.Time.AddToClassList("studio-dialogue-time");
            row.Root.Add(row.Time);
            var speakerColumn = new VisualElement();
            speakerColumn.AddToClassList("studio-dialogue-speaker");
            speakerColumn.Add(new Label("화자"));
            row.Speaker = new PopupField<string>(new System.Collections.Generic.List<string> { "" }, 0, DialogueSpeakerName, DialogueSpeakerName)
            {
                name = "dialogue-speaker"
            };
            row.Speaker.RegisterValueChangedCallback(ChangeSpeaker);
            speakerColumn.Add(row.Speaker);
            row.Root.Add(speakerColumn);
            var textColumn = new VisualElement();
            textColumn.AddToClassList("studio-dialogue-text-column");
            textColumn.Add(new Label("대사"));
            row.Text = new TextField
            {
                name = "dialogue-text",
                multiline = true
            };
            row.Text.verticalScrollerVisibility = ScrollerVisibility.Auto;
            row.Text.AddToClassList("studio-dialogue-text");
            row.Text.RegisterValueChangedCallback(ChangeDialogueText);
            textColumn.Add(row.Text);
            row.Root.Add(textColumn);
            var actions = new VisualElement();
            actions.AddToClassList("studio-dialogue-actions");
            row.Wait = new Toggle
            {
                text = "입력 대기",
                name = "dialogue-wait",
                tooltip = "대사 끝에서 E / Enter 입력을 기다립니다."
            };
            row.Wait.RegisterValueChangedCallback(ChangeInputWait);
            actions.Add(row.Wait);
            var delete = new Button(DeleteDialogueRow)
            {
                text = "−",
                tooltip = "이 대사 클립 삭제",
                name = "dialogue-delete"
            };
            actions.Add(delete);
            row.Root.Add(actions);
            row.Root.RegisterCallback<PointerDownEvent>(SelectRowOnPointerDown, TrickleDown.TrickleDown);
            return row.Root;
            void SelectDialogueRow()
            {
                if (row.Entry.Asset != null)
                    host.Select(row.Entry.Clip, false);
            }

            void ChangeSpeaker(ChangeEvent<string> e)
            {
                EditDialogue(row.Entry, "대사 화자 변경", SetSpeaker);
                void SetSpeaker(StudioClip data)
                {
                    data.ActorId = e.newValue;
                }
            }

            void ChangeDialogueText(ChangeEvent<string> e)
            {
                EditDialogue(row.Entry, "대사 내용 변경", SetDialogueText);
                void SetDialogueText(StudioClip data)
                {
                    data.Text = e.newValue;
                }
            }

            void ChangeInputWait(ChangeEvent<bool> e)
            {
                EditDialogue(row.Entry, "대사 입력 대기 변경", SetInputWait);
                void SetInputWait(StudioClip data)
                {
                    data.WaitForInput = e.newValue;
                }
            }

            void DeleteDialogueRow()
            {
                if (row.Entry.Asset == null)
                    return;
                host.Select(row.Entry.Clip, false);
                host.DeleteClip();
                SyncDialoguePanel();
            }

            void SelectRowOnPointerDown(PointerDownEvent e)
            {
                if (e.button == 0 && row.Entry.Asset != null && host.Selected != row.Entry.Asset)
                    host.Select(row.Entry.Clip, false);
            }
        }


        private string DialogueSpeakerName(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "없음 (내레이션)";
            return host.Sequence?.EditableActors.Find(IsSpeaker)?.Name ?? "누락: " + id;
            bool IsSpeaker(StudioActor a)
            {
                return a.Id == id;
            }
        }


        private void SyncDialogueRow(DialogueRow row)
        {
            var data = row.Entry.Asset;
            if (data == null || host.Sequence == null)
                return;
            row.Root.EnableInClassList("selected", data == host.Selected);
            bool muted = row.Entry.Clip.GetParentTrack().mutedInHierarchy;
            row.Time.text = (row.Index + 1).ToString("00") + "\n" + row.Entry.Clip.start.ToString("F2") + "초" + (muted ? "\n음소거" : "");
            var ids = new[]
            {
                ""
            }.Concat(host.Sequence.Actors.Select(GetActorId)).Append(data.ActorId ?? "").Distinct().ToList();
            if (!ids.SequenceEqual(row.Speaker.choices))
                row.Speaker.choices = ids;
            row.Speaker.SetValueWithoutNotify(data.ActorId ?? "");
            var focused = Window.rootVisualElement.focusController?.focusedElement as VisualElement;
            if (focused != row.Text && !row.Text.Contains(focused))
                row.Text.SetValueWithoutNotify(data.Text ?? "");
            row.Wait.SetValueWithoutNotify(data.WaitForInput);
            string GetActorId(StudioActor a)
            {
                return a.Id;
            }
        }


        private void EditDialogue(StudioEntry entry, string operation, Action<StudioClip> edit)
        {
            if (entry.Asset == null || host.Sequence == null)
                return;
            Undo.RecordObject(entry.Asset, operation);
            edit(entry.Asset);
            host.Changed();
        }


        internal void AppendDialogueLines(StudioSequence expectedSequence, string[] lines, string speakerId, double duration)
        {
            if (lines == null)
                return;
            var speakers = new string[lines.Length];
            for (int i = 0; i < speakers.Length; i++)
                speakers[i] = speakerId;
            AppendDialogueEntries(expectedSequence, lines, speakers, duration);
        }


        internal void AppendDialogueEntries(StudioSequence expectedSequence, string[] lines, string[] speakerIds, double duration)
        {
            if (host.Sequence != expectedSequence)
                return;
            var first = StudioDialogueImport.Append(host.Sequence, lines, speakerIds, duration, host.Clock.Time);
            if (first == null)
                return;
            host.Select(first, false);
            host.Changed();
            SyncDialoguePanel();
            for (int i = 0; i < dialogueEntries.Length; i++)
                if (dialogueEntries[i].Clip == first)
                {
                    dialogueList?.ScrollToItem(i);
                    break;
                }
        }

        internal bool DialogueMode { get { return dialogueMode; } set { dialogueMode = value; } }
        internal void SetEditingEnabled(bool enabled) { dialoguePanel?.SetEnabled(enabled); }
    }
}
