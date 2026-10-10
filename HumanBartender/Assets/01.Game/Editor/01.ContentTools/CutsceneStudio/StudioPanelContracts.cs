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
    internal interface IStudioPanelHost
    {
        CutsceneStudioWindow Window { get; }
    }
    internal interface IStudioPreviewHost : IStudioPanelHost
    {
        void Changed(bool rebuild = false);
        void ChooseActor(int index);
        void ClearClipSelection();
        TimelineClip FindSelected();
        void HandleLibraryDrop(Rect frame, bool timeline);
        bool NativeTextEditing();
        void Seek(double time);
        void Select(TimelineClip clip, bool reveal = true);
        void TogglePlay();
        int ActorIndex { get; }
        StudioClock Clock { get; }
        bool Playing { get; }
        StudioPreview Preview { get; }
        bool PreviewRebuildPending { get; }
        StudioClip Selected { get; }
        StudioSequence Sequence { get; }
        bool SnapEnabled { get; }
    }
    internal interface IStudioTimelineHost : IStudioPanelHost
    {
        StudioViewState ViewState { get; }
        void Changed(bool rebuild = false);
        void ClearClipSelection();
        TimelineClip FindSelected();
        float Inspector { get; }
        void PositionToolkitPanels();
        void ProcessDrop(Rect bounds, bool timeline, double at, StudioTrack hovered);
        void Seek(double time);
        void Select(TimelineClip clip, bool reveal = true);
        void SetDialogueMode();
        double Snap(double value);
        float TimelineHeight { get; }
        StudioClock Clock { get; }
        bool DialogueMode { get; set; }
        int KeyIndex { get; }
        StudioProperty KeyProperty { get; }
        StudioClip KeySelectionOwner { get; }
        StudioClip Selected { get; }
        StudioSequence Sequence { get; }
        StudioEditSession Session { get; }
        bool SnapEnabled { get; set; }
        string Status { get; }
    }
    internal interface IStudioSequenceHost : IStudioPanelHost
    {
        void AddPalette(StudioKind kind, double at, StudioTrack track = null);
        void Changed(bool rebuild = false);
        void PlaceAsset(Object asset, double at, Vector2 position, StudioTrack track = null);
        void ProcessDrop(Rect bounds, bool timeline, double at, StudioTrack hovered);
        void Select(TimelineClip clip, bool reveal = true);
        int ActorIndex { get; }
        bool CameraSelected { get; }
        StudioClock Clock { get; }
        StudioPreview Preview { get; }
        StudioClip Selected { get; }
        StudioSequence Sequence { get; }
    }
    internal interface IStudioPropertiesHost : IStudioPanelHost
    {
        void Changed(bool rebuild = false);
        void DeleteClip();
        void DeleteSelectedKey();
        void DuplicateClip();
        TimelineClip FindSelected();
        bool NativeTextEditing();
        void Seek(double time);
        double Snap(double value);
        int ActorIndex { get; }
        int KeyIndex { get; }
        StudioProperty KeyProperty { get; }
        StudioClip KeySelectionOwner { get; }
        bool KeyframeMode { get; }
        StudioClip Selected { get; }
        StudioSequence Sequence { get; }
        StudioEditSession Session { get; }
    }
    internal interface IStudioDialogueHost : IStudioPanelHost
    {
        StudioViewState ViewState { get; }
        void Changed(bool rebuild = false);
        void DeleteClip();
        float Inspector { get; }
        void PositionToolkitPanels();
        void ReleaseKeyDrag();
        void ReleaseTimelineResize();
        void ReleaseTimelineScrub();
        void Select(TimelineClip clip, bool reveal = true);
        float TimelineHeight { get; }
        float TimelineToolbarHeight { get; }
        StudioClock Clock { get; }
        bool KeyframeMode { get; set; }
        bool PreviewToolFocus { get; set; }
        StudioClip Selected { get; }
        StudioSequence Sequence { get; }
        StudioEditSession Session { get; }
    }
    internal interface IStudioCommandsHost : IStudioPanelHost
    {
        void Changed(bool rebuild = false);
        void ClearClipSelection();
        TimelineClip FindSelected();
        void ReleaseClipDrag();
        void Run(Action action);
        void Select(TimelineClip clip, bool reveal = true);
        int ActorIndex { get; }
        bool CameraSelected { get; }
        StudioClock Clock { get; }
        StudioPreview Preview { get; }
        StudioClip Selected { get; }
        StudioSequence Sequence { get; }
        StudioEditSession Session { get; }
        bool SnapEnabled { get; }
        string Status { get; set; }
    }

}
