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
    internal sealed partial class StudioWorkspace
    {
        CutsceneStudioWindow IStudioPanelHost.Window => Window;
        StudioViewState IStudioTimelineHost.ViewState => viewState;
        StudioViewState IStudioDialogueHost.ViewState => viewState;
        void IStudioPreviewHost.Changed(bool rebuild) { Changed(rebuild); }
        void IStudioPreviewHost.ChooseActor(int index) { sequenceComponent.ChooseActor(index); }
        void IStudioPreviewHost.ClearClipSelection() { ClearClipSelection(); }
        TimelineClip IStudioPreviewHost.FindSelected() { return FindSelected(); }
        void IStudioPreviewHost.HandleLibraryDrop(Rect frame, bool timeline) { sequenceComponent.HandleLibraryDrop(frame, timeline); }
        bool IStudioPreviewHost.NativeTextEditing() { return NativeTextEditing(); }
        void IStudioPreviewHost.Seek(double time) { Seek(time); }
        void IStudioPreviewHost.Select(TimelineClip clip, bool reveal) { Select(clip, reveal); }
        void IStudioPreviewHost.TogglePlay() { TogglePlay(); }
        int IStudioPreviewHost.ActorIndex { get { return actorIndex; } }
        StudioClock IStudioPreviewHost.Clock { get { return clock; } }
        bool IStudioPreviewHost.Playing { get { return playing; } }
        StudioPreview IStudioPreviewHost.Preview { get { return preview; } }
        bool IStudioPreviewHost.PreviewRebuildPending { get { return previewRebuildPending; } }
        StudioClip IStudioPreviewHost.Selected { get { return selected; } }
        StudioSequence IStudioPreviewHost.Sequence { get { return sequence; } }
        bool IStudioPreviewHost.SnapEnabled { get { return snap; } }
        void IStudioTimelineHost.Changed(bool rebuild) { Changed(rebuild); }
        void IStudioTimelineHost.ClearClipSelection() { ClearClipSelection(); }
        TimelineClip IStudioTimelineHost.FindSelected() { return FindSelected(); }
        float IStudioTimelineHost.Inspector { get { return Inspector; } }
        void IStudioTimelineHost.PositionToolkitPanels() { PositionToolkitPanels(); }
        void IStudioTimelineHost.ProcessDrop(Rect bounds, bool timeline, double at, StudioTrack hovered) { commandsComponent.ProcessDrop(bounds, timeline, at, hovered); }
        void IStudioTimelineHost.Seek(double time) { Seek(time); }
        void IStudioTimelineHost.Select(TimelineClip clip, bool reveal) { Select(clip, reveal); }
        void IStudioTimelineHost.SetDialogueMode() { dialogueComponent.SetDialogueMode(); }
        double IStudioTimelineHost.Snap(double value) { return Snap(value); }
        float IStudioTimelineHost.TimelineHeight { get { return TimelineHeight; } }
        StudioClock IStudioTimelineHost.Clock { get { return clock; } }
        bool IStudioTimelineHost.DialogueMode { get { return dialogueComponent.DialogueMode; } set { dialogueComponent.DialogueMode = value; } }
        int IStudioTimelineHost.KeyIndex { get { return keyIndex; } }
        StudioProperty IStudioTimelineHost.KeyProperty { get { return keyProperty; } }
        StudioClip IStudioTimelineHost.KeySelectionOwner { get { return keySelectionOwner; } }
        StudioClip IStudioTimelineHost.Selected { get { return selected; } }
        StudioSequence IStudioTimelineHost.Sequence { get { return sequence; } }
        StudioEditSession IStudioTimelineHost.Session { get { return session; } }
        bool IStudioTimelineHost.SnapEnabled { get { return snap; } set { snap = value; } }
        string IStudioTimelineHost.Status { get { return status; } }
        void IStudioSequenceHost.AddPalette(StudioKind kind, double at, StudioTrack track) { commandsComponent.AddPalette(kind, at, track); }
        void IStudioSequenceHost.Changed(bool rebuild) { Changed(rebuild); }
        void IStudioSequenceHost.PlaceAsset(Object asset, double at, Vector2 position, StudioTrack track) { commandsComponent.PlaceAsset(asset, at, position, track); }
        void IStudioSequenceHost.ProcessDrop(Rect bounds, bool timeline, double at, StudioTrack hovered) { commandsComponent.ProcessDrop(bounds, timeline, at, hovered); }
        void IStudioSequenceHost.Select(TimelineClip clip, bool reveal) { Select(clip, reveal); }
        int IStudioSequenceHost.ActorIndex { get { return actorIndex; } }
        bool IStudioSequenceHost.CameraSelected { get { return cameraSelected; } }
        StudioClock IStudioSequenceHost.Clock { get { return clock; } }
        StudioPreview IStudioSequenceHost.Preview { get { return preview; } }
        StudioClip IStudioSequenceHost.Selected { get { return selected; } }
        StudioSequence IStudioSequenceHost.Sequence { get { return sequence; } }
        void IStudioPropertiesHost.Changed(bool rebuild) { Changed(rebuild); }
        void IStudioPropertiesHost.DeleteClip() { commandsComponent.DeleteClip(); }
        void IStudioPropertiesHost.DeleteSelectedKey() { timelineComponent.DeleteSelectedKey(); }
        void IStudioPropertiesHost.DuplicateClip() { commandsComponent.DuplicateClip(); }
        TimelineClip IStudioPropertiesHost.FindSelected() { return FindSelected(); }
        bool IStudioPropertiesHost.NativeTextEditing() { return NativeTextEditing(); }
        void IStudioPropertiesHost.Seek(double time) { Seek(time); }
        double IStudioPropertiesHost.Snap(double value) { return Snap(value); }
        int IStudioPropertiesHost.ActorIndex { get { return actorIndex; } }
        int IStudioPropertiesHost.KeyIndex { get { return keyIndex; } }
        StudioProperty IStudioPropertiesHost.KeyProperty { get { return keyProperty; } }
        StudioClip IStudioPropertiesHost.KeySelectionOwner { get { return keySelectionOwner; } }
        bool IStudioPropertiesHost.KeyframeMode { get { return timelineComponent.KeyframeMode; } }
        StudioClip IStudioPropertiesHost.Selected { get { return selected; } }
        StudioSequence IStudioPropertiesHost.Sequence { get { return sequence; } }
        StudioEditSession IStudioPropertiesHost.Session { get { return session; } }
        void IStudioDialogueHost.Changed(bool rebuild) { Changed(rebuild); }
        void IStudioDialogueHost.DeleteClip() { commandsComponent.DeleteClip(); }
        float IStudioDialogueHost.Inspector { get { return Inspector; } }
        void IStudioDialogueHost.PositionToolkitPanels() { PositionToolkitPanels(); }
        void IStudioDialogueHost.ReleaseKeyDrag() { timelineComponent.ReleaseKeyDrag(); }
        void IStudioDialogueHost.ReleaseTimelineResize() { timelineComponent.ReleaseTimelineResize(); }
        void IStudioDialogueHost.ReleaseTimelineScrub() { timelineComponent.ReleaseTimelineScrub(); }
        void IStudioDialogueHost.Select(TimelineClip clip, bool reveal) { Select(clip, reveal); }
        float IStudioDialogueHost.TimelineHeight { get { return TimelineHeight; } }
        float IStudioDialogueHost.TimelineToolbarHeight { get { return timelineComponent.TimelineToolbarHeight; } }
        StudioClock IStudioDialogueHost.Clock { get { return clock; } }
        bool IStudioDialogueHost.KeyframeMode { get { return timelineComponent.KeyframeMode; } set { timelineComponent.KeyframeMode = value; } }
        bool IStudioDialogueHost.PreviewToolFocus { get { return previewComponent.PreviewToolFocus; } set { previewComponent.PreviewToolFocus = value; } }
        StudioClip IStudioDialogueHost.Selected { get { return selected; } }
        StudioSequence IStudioDialogueHost.Sequence { get { return sequence; } }
        StudioEditSession IStudioDialogueHost.Session { get { return session; } }
        void IStudioCommandsHost.Changed(bool rebuild) { Changed(rebuild); }
        void IStudioCommandsHost.ClearClipSelection() { ClearClipSelection(); }
        TimelineClip IStudioCommandsHost.FindSelected() { return FindSelected(); }
        void IStudioCommandsHost.ReleaseClipDrag() { timelineComponent.ReleaseClipDrag(); }
        void IStudioCommandsHost.Run(Action action) { Run(action); }
        void IStudioCommandsHost.Select(TimelineClip clip, bool reveal) { Select(clip, reveal); }
        int IStudioCommandsHost.ActorIndex { get { return actorIndex; } }
        bool IStudioCommandsHost.CameraSelected { get { return cameraSelected; } }
        StudioClock IStudioCommandsHost.Clock { get { return clock; } }
        StudioPreview IStudioCommandsHost.Preview { get { return preview; } }
        StudioClip IStudioCommandsHost.Selected { get { return selected; } }
        StudioSequence IStudioCommandsHost.Sequence { get { return sequence; } }
        StudioEditSession IStudioCommandsHost.Session { get { return session; } }
        bool IStudioCommandsHost.SnapEnabled { get { return snap; } }
        string IStudioCommandsHost.Status { get { return status; } set { status = value; } }
        StudioSequence IStudioFileHost.Sequence => sequence;
        void IStudioFileHost.Load(StudioSequence asset) { Load(asset); }
        void IStudioFileHost.Run(Action action) { Run(action); }
        void IStudioFileHost.SetStatus(string message) { status = message; }
        bool IStudioInputHost.CanHandleInput => sequence != null && !EditorApplication.isPlayingOrWillChangePlaymode;
        bool IStudioInputHost.KeyframeMode => timelineComponent.KeyframeMode;
        bool IStudioInputHost.HandlePreviewToolKeys(Event inputEvent) { return previewComponent.HandlePreviewToolKeys(inputEvent); }
        void IStudioInputHost.ExecuteInput(StudioInputCommand command) { ExecuteInput(command); }

    }

}
