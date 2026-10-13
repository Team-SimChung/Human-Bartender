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
    // 클립 편집 요청과 복사본 수명을 관리함.
    [Serializable]
    internal sealed class StudioClipCommands
    {
        [NonSerialized] private IStudioCommandsHost host;
        private CutsceneStudioWindow Window => host.Window;
        internal void Initialize(IStudioCommandsHost value) { host = value; }
        internal void AddPalette(StudioKind kind, double at, StudioTrack track = null)
        {
            if (host.Sequence == null)
                return;
            if (kind == StudioKind.Visual)
            {
                host.Run(CreateObjectClip);
                return;
                void CreateObjectClip()
                {
                    var clip = StudioEditorAssets.CreateObject(host.Sequence, at, track);
                    host.Select(clip);
                    host.Changed(true);
                }
            }

            if ((kind == StudioKind.Actor || kind == StudioKind.State && !host.CameraSelected) && host.ActorIndex < 0)
            {
                host.Status = "씬 오브젝트에서 대상을 먼저 선택하세요.";
                return;
            }

            host.Run(CreateTypedClip);
            void CreateTypedClip()
            {
                string id = host.ActorIndex >= 0 ? host.Sequence.Actors[host.ActorIndex].Id : null;
                var clip = StudioEditorAssets.AddClip(host.Sequence, kind, at, id, track);
                var data = (StudioClip)clip.asset;
                if (kind == StudioKind.State)
                {
                    host.Preview.Stage.Evaluate(at);
                    data.StateCamera = host.CameraSelected;
                    data.Keys = new[]
                    {
                        host.Preview.Stage.CapturePose(host.CameraSelected ? null : id)
                    };
                    data.FlipX = false;
                }

                host.Select(clip);
                host.Changed();
            }
        }


        internal void PlaceAsset(Object asset, double at, Vector2 position, StudioTrack track = null)
        {
            host.Run(PlaceRequestedAsset);
            void PlaceRequestedAsset()
            {
                var clip = StudioAssetLibrary.Place(host.Sequence, asset, at, position, track);
                host.Select(clip);
                host.Changed(true);
                host.Status = asset.name + " / " + at.ToString("F2") + "초에 배치됨";
            }
        }


        internal void ProcessDrop(Rect bounds, bool timeline, double at, StudioTrack hovered)
        {
            var e = Event.current;
            if ((e.type != EventType.DragUpdated && e.type != EventType.DragPerform) || !bounds.Contains(e.mousePosition))
                return;
            var source = DragAndDrop.objectReferences.FirstOrDefault();
            var payload = source == null ? DragAndDrop.GetGenericData(StudioAssetDrag.PayloadKey) : null;
            var kind = payload is StudioKind k ? k : source is AudioClip ? StudioKind.Audio : StudioKind.Visual;
            var track = StudioEditorAssets.DropTarget(hovered, kind, at);
            string reason = payload is StudioKind ? null : StudioAssetLibrary.UnsupportedReason(source);
            if (payload is StudioKind && !timeline)
                reason = "클립은 타임라인에 놓아 주세요.";
            if (payload is StudioKind && (kind == StudioKind.Actor || kind == StudioKind.State && !host.CameraSelected) && host.ActorIndex < 0)
                reason = "씬 오브젝트에서 대상을 먼저 선택하세요.";
            DragAndDrop.visualMode = reason == null ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            host.Status = reason ?? (timeline ? (track == null ? "새 트랙 / " : track.name + " / ") + at.ToString("F2") + "초에 추가" : "프리뷰에 배치");
            Window.Repaint();
            if (e.type == EventType.DragPerform && reason == null)
            {
                DragAndDrop.AcceptDrag();
                if (payload is StudioKind paletteKind)
                    AddPalette(paletteKind, at, track);
                else
                {
                    host.Preview.Stage.Evaluate(at);
                    var pos = timeline ? host.Preview.Stage.CameraCenter : host.Preview.Stage.FrameToWorld(new Vector2((e.mousePosition.x - bounds.center.x) * StudioSequence.Width / bounds.width, (bounds.center.y - e.mousePosition.y) * StudioSequence.Height / bounds.height));
                    PlaceAsset(source, at, pos, track);
                }

                DragAndDrop.SetGenericData(StudioAssetDrag.PayloadKey, null);
            }

            e.Use();
        }


        internal void DuplicateClip()
        {
            var source = host.FindSelected();
            if (source == null)
                return;
            var copy = StudioEditorAssets.AddClip(host.Sequence, host.Selected.Kind, source.end);
            EditorUtility.CopySerialized(host.Selected, copy.asset);
            copy.displayName = source.displayName + " 복사";
            copy.duration = source.duration;
            host.Sequence.Timeline.fixedDuration = Math.Max(host.Sequence.Duration, copy.end);
            host.Select(copy);
            host.Changed();
        }


        internal void DeleteClip()
        {
            var clip = host.FindSelected();
            if (clip == null)
                return;
            host.ReleaseClipDrag();
            StudioEditorAssets.DeleteClip(host.Sequence, clip);
            host.Session.ClearSelection();
            GUIUtility.hotControl = 0;
            host.Changed(true);
        }

        [SerializeField]
        private string clipboardName;

        [SerializeField] private StudioClipSnapshot clipboardData = new();

        [SerializeField] private StudioActor clipboardActor;
        [SerializeField] private bool clipboardHasActor;

        [SerializeField]
        private double clipboardDuration, clipboardClipIn, clipboardTimeScale;

        [SerializeField]
        private StudioKind clipboardKind;

        [SerializeField]
        private StudioSequence clipboardSequence;

        [SerializeField]
        private StudioTrack clipboardTrack;


        internal void CopyClip(bool cut)
        {
            var source = host.FindSelected();
            if (source == null)
                return;
            clipboardData.Capture(host.Selected);
            clipboardName = source.displayName;
            clipboardKind = host.Selected.Kind;
            clipboardDuration = source.duration;
            clipboardClipIn = source.clipIn;
            clipboardTimeScale = source.timeScale;
            clipboardSequence = host.Sequence;
            clipboardTrack = source.GetParentTrack() as StudioTrack;
            var actor = host.Sequence.FindActor(host.Selected.ActorId);
            clipboardHasActor = actor != null;
            clipboardActor = StudioClipSnapshot.CopyActor(actor);
            if (cut)
            {
                // 클립 하나를 옮겨도 다른 클립의 오브젝트와 화자 연결을 유지함.
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("클립 잘라내기");
                Undo.RegisterCompleteObjectUndo(host.Sequence.Timeline, "클립 잘라내기");
                Undo.RegisterCompleteObjectUndo(clipboardTrack, "클립 잘라내기");
                host.Sequence.Timeline.DeleteClip(source);
                Undo.CollapseUndoOperations(group);
                host.ClearClipSelection();
                host.Changed(true);
            }

            host.Status = cut ? "클립 잘라냄 / 현재 재생 위치에 Ctrl+V로 붙여넣기" : "클립 복사됨 / 현재 재생 위치에 Ctrl+V로 붙여넣기";
            Window.Repaint();
        }


        internal void PasteClip()
        {
            if (!clipboardData.HasValue || host.Sequence == null)
                return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("클립 붙여넣기");
            Undo.RegisterCompleteObjectUndo(host.Sequence, "클립 붙여넣기");
            double at = Math.Max(0, host.SnapEnabled ? StudioTiming.Snap(host.Sequence, host.Clock.Time) : host.Clock.Time);
            var target = host.FindSelected()?.GetParentTrack() as StudioTrack;
            if (!CanPasteInto(target, at))
                target = CanPasteInto(clipboardTrack, at) ? clipboardTrack : null;
            var copy = StudioEditorAssets.AddClip(host.Sequence, clipboardKind, at, target: target);
            var data = (StudioClip)copy.asset;
            clipboardData.Restore(data);
            if (clipboardHasActor && clipboardActor != null)
            {
                var actor = StudioClipSnapshot.CopyActor(clipboardActor);
                if (clipboardSequence != host.Sequence || host.Sequence.FindActor(actor.Id) == null)
                {
                    actor.Id = Guid.NewGuid().ToString("N");
                    host.Sequence.EditableActors.Add(actor);
                    data.ActorId = actor.Id;
                }

            }

            copy.displayName = clipboardName;
            copy.duration = clipboardDuration;
            copy.clipIn = clipboardClipIn;
            copy.timeScale = clipboardTimeScale;
            host.Sequence.Timeline.fixedDuration = Math.Max(host.Sequence.Duration, copy.end);
            EditorUtility.SetDirty(data);
            Undo.CollapseUndoOperations(group);
            host.Select(copy);
            host.Changed(true);
            host.Status = "클립 붙여넣기 / " + at.ToString("F2") + "초";
        }


        private bool CanPasteInto(StudioTrack track, double at)
        {
            if (track == null || track.timelineAsset != host.Sequence.Timeline || !track.Accepts(clipboardKind))
                return false;
            foreach (var clip in track.GetClips())
                if (clip.end > at && clip.start < at + clipboardDuration)
                    return false;
            return true;
        }

        internal StudioClipSnapshot ClipboardData { get { return clipboardData; } set { clipboardData = value; } }
    }
}
