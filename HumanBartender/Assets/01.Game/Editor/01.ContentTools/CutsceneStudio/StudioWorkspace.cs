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
    // 창과 패널의 연결 및 이벤트 수명을 조정함.
    [Serializable]
    internal sealed partial class StudioWorkspace : IStudioPreviewHost, IStudioTimelineHost, IStudioSequenceHost, IStudioPropertiesHost, IStudioDialogueHost, IStudioCommandsHost, IStudioFileHost, IStudioInputHost
    {
        [NonSerialized] private CutsceneStudioWindow window;
        internal CutsceneStudioWindow Window => window;
        [SerializeField] private StudioPreviewPanel previewComponent = new();
        [SerializeField] private StudioTimelinePanel timelineComponent = new();
        [SerializeField] private StudioSequencePanel sequenceComponent = new();
        [SerializeField] private StudioPropertiesPanel propertiesComponent = new();
        [SerializeField] private StudioDialoguePanel dialogueComponent = new();
        [SerializeField] private StudioClipCommands commandsComponent = new();
        internal void Initialize(CutsceneStudioWindow value)
        {
            window = value;
            previewComponent ??= new StudioPreviewPanel();
            previewComponent.Initialize(this);
            timelineComponent ??= new StudioTimelinePanel();
            timelineComponent.Initialize(this);
            sequenceComponent ??= new StudioSequencePanel();
            sequenceComponent.Initialize(this);
            propertiesComponent ??= new StudioPropertiesPanel();
            propertiesComponent.Initialize(this);
            dialogueComponent ??= new StudioDialoguePanel();
            dialogueComponent.Initialize(this);
            commandsComponent ??= new StudioClipCommands();
            commandsComponent.Initialize(this);
        }
        internal bool cameraSelected => session.CameraSelected;


        internal StudioClip keySelectionOwner => session.KeyOwner;

        internal StudioProperty keyProperty => session.KeyProperty;

        internal int keyIndex => session.KeyIndex;

        internal void OnLostFocus()
        {
            previewComponent.PreviewToolFocus = false;
            previewComponent.EndPreviewTransform();
            layout.ReleaseDrag();
            timelineComponent.ReleaseTimelineResize();
            timelineComponent.ReleaseKeyDrag();
            timelineComponent.ReleaseTimelineScrub();
        }


        [NonSerialized] private StudioPanelLayout layout;
        [NonSerialized] private StudioInputRouter input;
        [NonSerialized] private StudioFileCommands files;
        private float Sidebar => layout.Sidebar;
        internal float Inspector => layout.Inspector;
        internal float TimelineHeight => layout.TimelineHeight;
        internal bool NativeTextEditing() => input.NativeTextEditing();

        [SerializeField] private StudioEditSession session = new();
        [SerializeField] private StudioViewState viewState = new();

        internal StudioSequence sequence { get { return session.Sequence; } set { session.Load(value); } }

        internal StudioClip selected => session.Selected;

        internal int actorIndex => session.ActorIndex;

        [SerializeField]
        private bool autosave = true;

        [SerializeField]
        private bool snap = true;

        [NonSerialized] private StudioPreviewController playback;

        [NonSerialized] private StudioSaveCoordinator saves;

        internal StudioClock clock => playback.Clock;

        internal StudioPreview preview => playback.Preview;

        internal bool previewRebuildPending => playback.RebuildPending;

        [NonSerialized] private bool drawingWorkbench;

        internal bool playing { get { return playback.Playing; } set { playback.Playing = value; } }

        private bool pendingSave { get { return saves.Pending; } set { if (value) saves.MarkChanged(EditorApplication.timeSinceStartup); else saves.AcknowledgeSave(); } }

        [NonSerialized] private string status = "새 컷씬을 만들거나 기존 컷씬을 여세요.";


        internal void OnEnable()
        {
            playback = new StudioPreviewController(session, Window.Repaint);
            saves = new StudioSaveCoordinator(new StudioUnitySequenceStore());
            layout = new StudioPanelLayout(Window);
            input = new StudioInputRouter(this);
            files = new StudioFileCommands(this, saves);
            // 복원된 창의 버퍼·크기를 GUI 연결 전에 초기화하며 UI Toolkit 잘라내기에 스텐실을 사용함.
            Window.depthBufferBits = 24;
            Window.SetAntiAliasing(1);
            Window.maxSize = new Vector2(10000, 10000);
            Window.minSize = new Vector2(1050, 690);
            Window.titleContent = new GUIContent("컷씬 스튜디오");
            Window.wantsMouseMove = true;
            Window.wantsMouseEnterLeaveWindow = true;
            layout.Load();
            timelineComponent.TimelineNameWidth = EditorPrefs.GetFloat(StudioPanelLayout.LayoutKey + "TrackNames", 250);
            EditorApplication.update += Tick;
            Undo.undoRedoPerformed += RefreshExternalChanges;
            ObjectChangeEvents.changesPublished += OnAssetsChanged;
            EditorApplication.projectChanged += sequenceComponent.InvalidateLibrary;
            sequenceComponent.InvalidateLibrary();
            if (sequence == null)
                sequence = AssetDatabase.LoadAssetAtPath<StudioSequence>(AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString("HumanBartender.CutsceneStudio.Last", "")));
            saves.Load(sequence);
            session.CaptureUndoState();
            QueuePreviewRebuild();
        }


        internal void OnDisable()
        {
            ReleaseToolkitEvents();
            timelineComponent.ReleaseClipDrag();
            timelineComponent.ReleaseTimelineScrub();
            previewComponent.EndPreviewTransform();
            layout.ReleaseDrag();
            timelineComponent.ReleaseTimelineResize();
            timelineComponent.ReleaseKeyDrag();
            EditorApplication.update -= Tick;
            Undo.undoRedoPerformed -= RefreshExternalChanges;
            ObjectChangeEvents.changesPublished -= OnAssetsChanged;
            EditorApplication.projectChanged -= sequenceComponent.InvalidateLibrary;
            sequenceComponent.Dispose();

            if (pendingSave && autosave && sequence != null)
                saves.Save();
            playback.Dispose();
            propertiesComponent.ReleaseToolkitBindings();
        }


        private void Tick()
        {
            playback.Tick(EditorApplication.timeSinceStartup, StudioPreviewPanel.PreviewSpeeds[Mathf.Clamp(previewComponent.PreviewSpeedIndex, 0, StudioPreviewPanel.PreviewSpeeds.Length - 1)], previewComponent.PreviewApplyInputWait);
            bool editing = timelineComponent.ClipDrag != null || timelineComponent.DraggingKey || previewComponent.PreviewDragAxis >= 0 || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode;
            if (saves.Tick(EditorApplication.timeSinceStartup, autosave, editing))
                status = "자동 저장됨 / " + DateTime.Now.ToString("HH:mm:ss");
        }


        private void OnAssetsChanged(ref ObjectChangeEventStream stream)
        {
            // 일반 씬 변경은 건너뛰고 현재 컷씬에 속한 에셋 알림만 확인함.
            for (int i = 0; i < stream.length; i++)
            {
                int instanceId;
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.ChangeAssetObjectProperties:
                        stream.GetChangeAssetObjectPropertiesEvent(i, out var changed);
                        instanceId = changed.instanceId;
                        break;
                    case ObjectChangeKind.DestroyAssetObject:
                        stream.GetDestroyAssetObjectEvent(i, out var destroyed);
                        instanceId = destroyed.instanceId;
                        break;
                    default:
                        continue;
                }
                if (!session.TracksUndoTarget(instanceId))
                    continue;
                RefreshExternalChanges();
                return;
            }
        }

        private void RefreshExternalChanges()
        {
            if (!session.HasUndoChanges())
                return;
            if (sequence == null)
            {
                Load(null);
                status = "현재 컷씬 에셋이 없습니다.";
                Window.Repaint();
                return;
            }
            previewComponent.EndPreviewTransform();
            timelineComponent.ReleaseClipDrag();
            timelineComponent.ReleaseKeyDrag();
            // 키는 인덱스로 선택하므로 Undo로 다른 키를 가리키지 않게 해제함.
            session.ClearKey();
            session.ValidateSelection();
            Changed(true);
        }


        internal void Load(StudioSequence asset)
        {
            timelineComponent.ReleaseClipDrag();
            timelineComponent.ReleaseTimelineScrub();
            previewComponent.EndPreviewTransform();
            saves.Load(asset);
            sequence = asset;
            panelsDirty = true;
            propertiesComponent.ResetForSequence();
            sequenceComponent.ResetForSequence();
            timelineComponent.ReleaseKeyDrag();
            session.ClearKey();
            timelineComponent.CollapsedKeyClips.Clear();
            session.ClearSelection();
            clock.Seek(0);
            playing = pendingSave = false;
            RebuildPreview();
            if (asset != null)
            {
                EditorPrefs.SetString("HumanBartender.CutsceneStudio.Last", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)));
                status = asset.name + " / 불러옴";
            }
        }


        private void RebuildPreview()
        {
            playback.Rebuild(drawingWorkbench);
        }


        private void QueuePreviewRebuild()
        {
            playback.QueueRebuild();
        }


        internal void Changed(bool rebuild = false)
        {
            if (sequence == null)
                return;
            panelsDirty = true;
            StudioEditorAssets.Dirty(sequence);
            session.CaptureUndoState();
            pendingSave = true;

            status = "변경됨";
            if (rebuild)
                RebuildPreview();
            Window.Repaint();
        }


        private void DrawWorkbench()
        {
            drawingWorkbench = true;
            try
            {
                DrawWorkbenchContents();
            }
            finally
            {
                drawingWorkbench = false;
            }
        }


        private void DrawWorkbenchContents()
        {
            if (Event.current.type == EventType.MouseDown && !timelineComponent.ContainsSearch(Event.current.mousePosition))
            {
                previewComponent.PreviewToolFocus = false;
                workbench?.Focus();
            }

            timelineComponent.UpdateTimelinePointer();
            if (sequenceComponent.HandlePickedAsset(Event.current.commandName))
                Event.current.Use();
            if (EditorApplication.isPlaying)
            {
                timelineComponent.ReleaseClipDrag();
                timelineComponent.ReleaseTimelineScrub();
                playing = false;
                playback.Dispose();
                EditorGUILayout.HelpBox("게임 실행 중에는 컷씬 편집을 잠시 멈춥니다. 재생을 끝내면 편집을 이어갈 수 있습니다.", MessageType.Info);
                return;
            }

            if (sequence != null && preview == null && sequence.Timeline != null)
                QueuePreviewRebuild();
            layout.Update();
            layout.HandleResize();
            StudioGUI.DrawToolbar(sequence, ref autosave, files.Create, Load, files.ShowSaveMenu);
            float bodyHeight = Window.position.height - TimelineHeight - StudioPanelLayout.MainToolbarHeight;
            var left = new Rect(0, StudioPanelLayout.MainToolbarHeight, Sidebar, bodyHeight);
            var center = new Rect(Sidebar + 1, StudioPanelLayout.MainToolbarHeight, Window.position.width - Sidebar - Inspector - 2, bodyHeight);
            var right = new Rect(Window.position.width - Inspector, StudioPanelLayout.MainToolbarHeight, Inspector, Window.position.height - StudioPanelLayout.MainToolbarHeight);
            StudioGUI.Panel(left);
            StudioGUI.Panel(center);
            StudioGUI.Panel(right);
            PositionToolkitPanels();
            previewComponent.DrawPreview(center);
            timelineComponent.DrawTimeline(new Rect(0, Window.position.height - TimelineHeight, Window.position.width - Inspector - 1, TimelineHeight));
            layout.DrawDividers();
            input.HandleIMGUI();
            // 컨트롤이 처리하지 않은 빈 공간 클릭은 시퀀스 설정 선택으로 처리함.
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && !right.Contains(Event.current.mousePosition) && !timelineComponent.ContainsSearch(Event.current.mousePosition))
                ClearClipSelection();
        }


        internal void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                status = error.Message;
                EditorUtility.DisplayDialog("컷씬 스튜디오", error.Message, "확인");
            }
        }


        internal double Snap(double value) => snap && (Event.current == null || !Event.current.shift) ? StudioTiming.Snap(sequence, value) : value;

        internal void Select(TimelineClip clip, bool reveal = true)
        {
            if (Event.current != null)
                GUI.FocusControl(null);
            panelsDirty = true;
            session.ClearKey();
            session.Select((StudioClip)clip.asset);
            playing = false;
            clock.Seek(clip.start);
            if (reveal)
                timelineComponent.RevealTimelineSelection(clip);
            Window.Repaint();
        }


        internal TimelineClip FindSelected()
        {
            return session.FindSelected();
        }


        internal void Seek(double time)
        {
            playback.Seek(time);
        }


        internal void TogglePlay()
        {
            playback.Toggle();
        }


        private const string PanelStylesPath = "Assets/01.Game/Editor/01.ContentTools/CutsceneStudio/StudioToolkitPanels.uss";

        [NonSerialized] private IMGUIContainer workbench;


        [NonSerialized] private VisualElement sequencePanel, propertiesPanel;


        [NonSerialized] private bool panelsDirty = true;


        [NonSerialized] private IVisualElementScheduledItem toolkitSync;

        public void CreateGUI()
        {
            ReleaseToolkitEvents();
            propertiesComponent.ReleaseToolkitBindings();
            Window.rootVisualElement.Clear();
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelStylesPath);
            if (style != null)
                Window.rootVisualElement.styleSheets.Add(style);
            workbench = new IMGUIContainer(DrawWorkbench)
            {
                name = "studio-workbench"
            };
            workbench.style.flexGrow = 1;
            Window.rootVisualElement.Add(workbench);
            sequencePanel = sequenceComponent.CreateGUI();
            propertiesPanel = propertiesComponent.CreateGUI();
            Window.rootVisualElement.Add(sequencePanel);
            Window.rootVisualElement.Add(propertiesPanel);
            dialogueComponent.BuildDialoguePanel();
            Window.rootVisualElement.Add(timelineComponent.CreateSearch());
            Window.rootVisualElement.RegisterCallback<GeometryChangedEvent>(UpdatePanelGeometry);
            // 입력창은 자체 편집 이벤트를 처리하고 카드·헤더 명령은 타임라인 동작에 한 번만 전달함.
            Window.rootVisualElement.RegisterCallback<KeyDownEvent>(input.HandleToolkitKey);
            Window.rootVisualElement.RegisterCallback<ValidateCommandEvent>(input.ValidateToolkitCommand);
            Window.rootVisualElement.RegisterCallback<ExecuteCommandEvent>(input.ExecuteToolkitCommand);
            Window.rootVisualElement.RegisterCallback<PointerDownEvent>(ReleasePreviewToolFocus, TrickleDown.TrickleDown);
            Window.rootVisualElement.RegisterCallback<ExecuteCommandEvent>(ReceivePickedAsset);
            toolkitSync = Window.rootVisualElement.schedule.Execute(SyncToolkitPanels).Every(100);
            propertiesComponent.ResetForSequence();
            panelsDirty = true;
            PositionToolkitPanels();
            SyncToolkitPanels();
        }


        private void UpdatePanelGeometry(GeometryChangedEvent e)
        {
            PositionToolkitPanels();
        }


        private void ReceivePickedAsset(ExecuteCommandEvent e)
        {
            if (sequenceComponent.HandlePickedAsset(e.commandName))
                e.StopPropagation();
        }


        private void ReleaseToolkitEvents()
        {
            toolkitSync?.Pause();
            toolkitSync = null;
            Window.rootVisualElement.UnregisterCallback<GeometryChangedEvent>(UpdatePanelGeometry);
            Window.rootVisualElement.UnregisterCallback<KeyDownEvent>(input.HandleToolkitKey);
            Window.rootVisualElement.UnregisterCallback<ValidateCommandEvent>(input.ValidateToolkitCommand);
            Window.rootVisualElement.UnregisterCallback<ExecuteCommandEvent>(input.ExecuteToolkitCommand);
            Window.rootVisualElement.UnregisterCallback<PointerDownEvent>(ReleasePreviewToolFocus, TrickleDown.TrickleDown);
            Window.rootVisualElement.UnregisterCallback<ExecuteCommandEvent>(ReceivePickedAsset);
        }


        internal void PositionToolkitPanels()
        {
            if (sequencePanel == null)
                return;
            layout.Update();
            sequencePanel.style.left = 0;
            sequencePanel.style.top = StudioPanelLayout.MainToolbarHeight;
            sequencePanel.style.width = Mathf.Max(0, Sidebar - 3);
            sequencePanel.style.height = Mathf.Max(0, Window.position.height - TimelineHeight - StudioPanelLayout.MainToolbarHeight - 3);
            propertiesPanel.style.left = Window.position.width - Inspector + 3;
            propertiesPanel.style.top = StudioPanelLayout.MainToolbarHeight;
            propertiesPanel.style.right = 0;
            propertiesPanel.style.bottom = 0;
            var display = EditorApplication.isPlaying ? DisplayStyle.None : DisplayStyle.Flex;
            sequencePanel.style.display = propertiesPanel.style.display = display;
            dialogueComponent.PositionDialoguePanel();
            timelineComponent.PositionSearch();
        }


        internal void SyncToolkitPanels()
        {
            if (sequencePanel == null || Window == null)
                return;
            PositionToolkitPanels();
            SetToolkitEditingEnabled(!EditorApplication.isPlaying);
            if (EditorApplication.isPlaying)
                return;
            bool changed = panelsDirty;
            panelsDirty = false;
            sequenceComponent.Sync(changed);
            propertiesComponent.Sync(changed);
            dialogueComponent.SyncDialoguePanel();
        }


        private void SetToolkitEditingEnabled(bool enabled)
        {
            // Play 중에는 입력만 막아 종료 후 같은 갱신 예약과 이벤트를 이어서 사용함.
            sequencePanel?.SetEnabled(enabled);
            propertiesPanel?.SetEnabled(enabled);
            dialogueComponent.SetEditingEnabled(enabled);
            timelineComponent.SetEditingEnabled(enabled);
        }


        internal void ClearClipSelection()
        {
            previewComponent.EndPreviewTransform();
            timelineComponent.ReleaseClipDrag();
            timelineComponent.ReleaseKeyDrag();
            session.ClearSelection();
            session.ClearKey();
            GUIUtility.hotControl = 0;
            if (Event.current != null)
                GUI.FocusControl(null);
            workbench?.Focus();
            panelsDirty = true;
            EditorGUIUtility.editingTextField = false;
            Window.Repaint();
        }


        internal void AddTimelineTrack(StudioSequence asset, StudioKind kind) { timelineComponent.AddTimelineTrack(asset, kind); }
        internal void RenameTimelineTrack(StudioSequence asset, StudioTrack track, string name) { timelineComponent.RenameTimelineTrack(asset, track, name); }
        internal void AppendDialogueEntries(StudioSequence asset, string[] lines, string[] speakers, double duration) { dialogueComponent.AppendDialogueEntries(asset, lines, speakers, duration); }
        internal void DisposeClipboard() { commandsComponent.ClipboardData.Dispose(); }
        internal bool UpdatesScheduled => toolkitSync != null;
        internal StudioSequence DisplayedSequence => sequenceComponent.DisplayedSequence;
        private void ReleasePreviewToolFocus(PointerDownEvent inputEvent)
        {
            for (var target = inputEvent.target as VisualElement; target != null; target = target.parent)
                if (target is IMGUIContainer)
                    return;
            previewComponent.PreviewToolFocus = false;
        }

        private void ExecuteInput(StudioInputCommand command)
        {
            switch (command)
            {
                case StudioInputCommand.Save:
                    workbench?.Focus();
                    files.Save();
                    break;
                case StudioInputCommand.ClearSelection: ClearClipSelection(); break;
                case StudioInputCommand.Undo: Undo.PerformUndo(); break;
                case StudioInputCommand.Redo: Undo.PerformRedo(); break;
                case StudioInputCommand.Copy: commandsComponent.CopyClip(false); break;
                case StudioInputCommand.Cut: commandsComponent.CopyClip(true); break;
                case StudioInputCommand.Paste: commandsComponent.PasteClip(); break;
                case StudioInputCommand.DeleteClip: commandsComponent.DeleteClip(); break;
                case StudioInputCommand.DeleteKey: timelineComponent.DeleteSelectedKey(); break;
                case StudioInputCommand.TogglePlay: TogglePlay(); break;
                case StudioInputCommand.Advance: clock.Advance(); break;
                case StudioInputCommand.FirstFrame: Seek(0); break;
                case StudioInputCommand.PreviousFrame: Seek(clock.Time - StudioTiming.FrameDuration(sequence)); break;
                case StudioInputCommand.NextFrame: Seek(clock.Time + StudioTiming.FrameDuration(sequence)); break;
            }
        }

    }
}
