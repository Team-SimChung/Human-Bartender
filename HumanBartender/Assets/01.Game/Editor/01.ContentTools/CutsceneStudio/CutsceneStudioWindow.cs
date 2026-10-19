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
    // Unity 창의 생명주기만 담당하고 작성 기능은 작업 공간에 연결함.
    public sealed class CutsceneStudioWindow : EditorWindow
    {
        [SerializeField] private StudioWorkspace workspace = new();
        internal StudioWorkspace Workspace => workspace;
        [MenuItem("Tools/Cutscene Studio")]
        public static void Open()
        {
            var window = GetWindow<CutsceneStudioWindow>("컷씬 스튜디오");
            window.minSize = new Vector2(1050, 690);
            window.Show();
        }


        [OnOpenAsset]
        private static bool OpenAsset(int instanceId, int line)
        {
            if (EditorUtility.EntityIdToObject((EntityId)instanceId)is not StudioSequence asset)
                return false;
            Open();
            GetWindow<CutsceneStudioWindow>().Load(asset);
            return true;
        }

        private void OnEnable() { workspace ??= new StudioWorkspace(); workspace.Initialize(this); workspace.OnEnable(); }
        private void OnDisable() { workspace?.OnDisable(); }
        private void OnDestroy() { workspace?.DisposeClipboard(); }
        private void OnLostFocus() { workspace?.OnLostFocus(); }
        public void CreateGUI() { workspace.CreateGUI(); }
        internal void Load(StudioSequence asset) { workspace.Load(asset); }
        internal void AddTimelineTrack(StudioSequence asset, StudioKind kind) { workspace.AddTimelineTrack(asset, kind); }
        internal void RenameTimelineTrack(StudioSequence asset, StudioTrack track, string name) { workspace.RenameTimelineTrack(asset, track, name); }
        internal void AppendDialogueEntries(StudioSequence asset, string[] lines, string[] speakers, double duration) { workspace.AppendDialogueEntries(asset, lines, speakers, duration); }
        internal static void ApplyPreviewProperty(TimelineClip clip, StudioProperty property, float at, float delta, bool multiply, bool recordKey) { StudioPreviewPanel.ApplyPreviewProperty(clip, property, at, delta, multiply, recordKey); }
    }
}
