using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 공통 정보가 원본이며 클립 이름은 기존 Timeline 표시를 위한 파생 값임.
    internal static class StudioSharedObjectEditing
    {
        internal static bool Rename(StudioSequence sequence, string actorId, string name)
        {
            var actor = sequence?.FindActor(actorId);
            if (actor == null || actor.Name == name) return false;
            var clips = LinkedClips(sequence, actorId);
            var targets = new HashSet<Object> { sequence };
            foreach (var entry in clips) targets.Add(entry.Clip.GetParentTrack());
            return Change(targets, "오브젝트 이름 변경", () =>
            {
                actor.Name = name;
                foreach (var entry in clips) entry.Clip.displayName = name;
            });
        }

        internal static bool SetSprite(StudioSequence sequence, string actorId, Sprite sprite)
        {
            var actor = sequence?.FindActor(actorId);
            if (actor == null) return false;
            var clips = LinkedClips(sequence, actorId);
            var targets = new HashSet<Object> { sequence };
            foreach (var entry in clips)
                if (HasStaticOverride(entry.Asset)) targets.Add(entry.Asset);
            if (actor.Sprite == sprite && targets.Count == 1) return false;
            return Change(targets, "오브젝트 기본 이미지 변경", () =>
            {
                actor.Sprite = sprite;
                // 이전 단일 이미지 오버라이드만 공통 참조로 전환하며 애니메이션은 보존함.
                foreach (var entry in clips)
                    if (HasStaticOverride(entry.Asset))
                    {
                        entry.Asset.Frames = Array.Empty<Sprite>();
                        entry.Asset.FrameTimes = null;
                        entry.Asset.AnimationLength = 0;
                    }
            });
        }

        internal static bool HasStaticOverride(StudioClip clip) => clip.Frames?.Length == 1 && !StudioPropertyEditing.UsesSourceTiming(clip);

        private static List<StudioEntry> LinkedClips(StudioSequence sequence, string actorId)
        {
            var result = new List<StudioEntry>();
            foreach (var entry in StudioEvaluation.Clips(sequence, true))
                if (entry.Asset.Kind == StudioKind.Visual && entry.Asset.ActorId == actorId) result.Add(entry);
            return result;
        }

        private static bool Change(HashSet<Object> targets, string label, Action change)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);
            foreach (var target in targets) Undo.RegisterCompleteObjectUndo(target, label);
            change();
            foreach (var target in targets) EditorUtility.SetDirty(target);
            Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
            return true;
        }
    }
}
