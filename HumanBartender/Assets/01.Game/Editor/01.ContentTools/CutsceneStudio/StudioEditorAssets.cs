using System;
using System.IO;
using HumanBartender.CutsceneStudio;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace HumanBartender.CutsceneStudio.Editor
{
    public static class StudioEditorAssets
    {
        public const string Root = "Assets/01.Game/03.Content/02.Cutscenes/Studio";
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static StudioSequence Create(string path)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var sequence = ScriptableObject.CreateInstance<StudioSequence>();
            sequence.Id = Path.GetFileNameWithoutExtension(path);
            sequence.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/01.Game/03.Content/05.Fonts/NeoDunggeunmo SDF.asset");
            sequence.Timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            sequence.Timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            sequence.Timeline.fixedDuration = 10;
            sequence.Timeline.editorSettings.frameRate = StudioTiming.DefaultFrameRate;
            AssetDatabase.CreateAsset(sequence.Timeline, AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(path, ".playable")));
            AssetDatabase.CreateAsset(sequence, path);
            AddClip(sequence, StudioKind.Camera, 0);
            Save(sequence);
            return sequence;
        }

        public static StudioTrack AddTrack(StudioSequence sequence, StudioKind kind)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("새 트랙 추가");
            Undo.RegisterCompleteObjectUndo(sequence.Timeline, "새 트랙 추가");
            var track = sequence.Timeline.CreateTrack<StudioTrack>(null, Label(kind));
            track.StudioTrackKind = (int)kind;
            Undo.RegisterCreatedObjectUndo(track, "새 트랙 추가");
            EditorUtility.SetDirty(track);
            Dirty(sequence);
            return track;
        }

        public static bool DeleteEmptyTrack(StudioSequence sequence, StudioTrack track)
        {
            if (sequence == null || sequence.Timeline == null || track == null || track.timelineAsset != sequence.Timeline || System.Linq.Enumerable.Any(track.GetClips()) || System.Linq.Enumerable.Any(track.GetChildTracks()))
                return false;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("빈 트랙 삭제");
            Undo.RegisterCompleteObjectUndo(sequence.Timeline, "빈 트랙 삭제");
            bool deleted = sequence.Timeline.DeleteTrack(track);
            Undo.CollapseUndoOperations(group);
            if (deleted)
                Dirty(sequence);
            return deleted;
        }

        public static TimelineClip AddClip(StudioSequence sequence, StudioKind kind, double at, string actorId = null, StudioTrack target = null)
        {
            if (target != null && (target.timelineAsset != sequence.Timeline || !target.Accepts(kind)))
                throw new InvalidOperationException("같은 종류의 트랙에 놓아 주세요.");
            Undo.RegisterCompleteObjectUndo(sequence.Timeline, "클립 추가");
            var track = target ?? sequence.Timeline.CreateTrack<StudioTrack>(null, Label(kind));
            if (target == null)
                Undo.RegisterCreatedObjectUndo(track, "트랙 추가");
            else
                Undo.RegisterCompleteObjectUndo(track, "클립 추가");
            var clip = track.CreateClip<StudioClip>();
            Undo.RegisterCreatedObjectUndo(clip.asset, "클립 추가");
            clip.displayName = Label(kind);
            clip.start = Math.Max(0, at);
            clip.duration = 3;
            var asset = (StudioClip)clip.asset;
            asset.Kind = kind;
            asset.ActorId = actorId ?? (sequence.Actors.Count > 0 ? sequence.Actors[0].Id : "");
            switch (kind)
            {
                case StudioKind.Actor:
                    asset.From = sequence.FindActor(asset.ActorId)?.Position ?? Vector2.zero;
                    asset.To = asset.From + new Vector2(160, 0);
                    break;
                case StudioKind.Background:
                    asset.ActorId = "";
                    asset.HoldEnd = false;
                    break;
                case StudioKind.Visual:
                    asset.HoldEnd = false;
                    break;
                case StudioKind.Wait:
                    clip.duration = .1;
                    break;
                case StudioKind.State:
                    asset.Keys = new[] { new StudioPoseKey { Position = sequence.FindActor(asset.ActorId)?.Position ?? Vector2.zero } };
                    break;
                case StudioKind.Dialogue:
                    asset.WaitForInput = true;
                    asset.Font = sequence.Font != null ? sequence.Font : TMP_Settings.defaultFontAsset;
                    break;
            }

            sequence.Timeline.fixedDuration = Math.Max(sequence.Duration, clip.end);
            Dirty(sequence);
            return clip;
        }

        public static string Label(StudioKind kind)
        {
            return StudioClipSchema.Definition(kind).Label;
        }
        // 다른 유형이나 이미 클립이 있는 구간에 놓으면 새 트랙을 사용함.
        public static StudioTrack DropTarget(StudioTrack hovered, StudioKind kind, double time)
        {
            if (hovered == null || !hovered.Accepts(kind))
                return null;
            foreach (var clip in hovered.GetClips())
                if (time >= clip.start && time < clip.end)
                    return null;
            return hovered;
        }

        public static TimelineClip CreateObject(StudioSequence sequence, double time, StudioTrack target = null)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(sequence, "오브젝트 클립 추가");
            var actor = new StudioActor
            {
                Id = "actor_" + Guid.NewGuid().ToString("N").Substring(0, 10),
                Name = "새 오브젝트",
                ClipControlled = true
            };
            sequence.EditableActors.Add(actor);
            var clip = AddClip(sequence, StudioKind.Visual, time, actor.Id, DropTarget(target, StudioKind.Visual, time));
            ((StudioClip)clip.asset).From = ((StudioClip)clip.asset).To = actor.Position;
            clip.displayName = actor.Name;
            Undo.CollapseUndoOperations(group);
            return clip;
        }

        public static void DeleteClip(StudioSequence sequence, TimelineClip clip)
        {
            if (clip == null)
                return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("컷씬 클립 삭제");
            Undo.RegisterCompleteObjectUndo(sequence, "컷씬 클립 삭제");
            string actorId = (clip.asset as StudioClip)?.ActorId;
            DeleteTimelineClip(sequence, clip);
            if (!string.IsNullOrEmpty(actorId) && !StudioEvaluation.Clips(sequence, true).Exists(MatchesTrueExists))
            {
                // 화자를 삭제해도 대사는 유지하며 화자 없는 기본 위치를 사용함.
                foreach (var e in StudioEvaluation.Clips(sequence, true).FindAll(MatchesTrueFindAll))
                {
                    if (e.Asset.Kind == StudioKind.State && !e.Asset.StateCamera)
                        DeleteTimelineClip(sequence, e.Clip);
                    else
                    {
                        Undo.RecordObject(e.Asset, "화자 연결 해제");
                        e.Asset.ActorId = "";
                        EditorUtility.SetDirty(e.Asset);
                    }
                }

                sequence.EditableActors.RemoveAll(MatchesActorsRemoveAll);
                bool MatchesTrueFindAll(StudioEntry e)
                {
                    return e.Asset.ActorId == actorId;
                }

                bool MatchesActorsRemoveAll(StudioActor a)
                {
                    return a.Id == actorId;
                }
            }

            Dirty(sequence);
            Undo.CollapseUndoOperations(group);
            bool MatchesTrueExists(StudioEntry e)
            {
                return StudioEvaluation.OwnsActor(e.Asset) && e.Asset.ActorId == actorId;
            }
        }

        private static void DeleteTimelineClip(StudioSequence sequence, TimelineClip clip)
        {
            var track = clip.GetParentTrack();
            sequence.Timeline.DeleteClip(clip);
            if (!System.Linq.Enumerable.Any(track.GetClips()))
                sequence.Timeline.DeleteTrack(track);
        }

        public static void Dirty(StudioSequence sequence)
        {
            EditorUtility.SetDirty(sequence);
            EditorUtility.SetDirty(sequence.Timeline);
        }

        public static void Save(StudioSequence sequence)
        {
            if (sequence == null || sequence.Timeline == null)
                return;
            Dirty(sequence);
            foreach (var track in sequence.Timeline.GetOutputTracks())
                EditorUtility.SetDirty(track);
            foreach (var entry in StudioEvaluation.Clips(sequence, true))
                EditorUtility.SetDirty(entry.Asset);
            AssetDatabase.SaveAssetIfDirty(sequence.Timeline);
            AssetDatabase.SaveAssetIfDirty(sequence);
        }

        public static void Register(StudioSequence sequence)
        {
            StudioCatalogRegistration.Register(sequence);
        }

        public static string ExportPrefab(StudioSequence sequence, bool registerForGame = true)
        {
            var issues = sequence.Validate();
            if (issues.Count != 0)
                throw new InvalidOperationException(string.Join("\n", issues));
            Save(sequence);
            if (registerForGame)
                Register(sequence);
            string path = Path.ChangeExtension(AssetDatabase.GetAssetPath(sequence), ".prefab");
            // 다른 용도의 프리팹을 내보내기로 덮어쓰지 않도록 확인함.
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && existing.GetComponent<StudioPlayer>()?.Sequence != sequence)
                path = AssetDatabase.GenerateUniqueAssetPath(path);
            var root = new GameObject(sequence.name);
            try
            {
                var player = root.AddComponent<StudioPlayer>();
                player.Sequence = sequence;
                player.PlayOnStart = false;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            return path;
        }

        public static void ExportScene(StudioSequence sequence, string path, bool registerForGame = true)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                if (string.IsNullOrEmpty(open.path))
                    throw new InvalidOperationException("Unity는 이름 없는 씬이 열려 있으면 새 씬을 추가할 수 없습니다. 현재 씬을 한 번 저장한 뒤 다시 내보내세요. 컷씬 중간 저장은 그대로 사용할 수 있습니다.");
                if (open.path == path)
                    throw new InvalidOperationException("내보낼 씬이 현재 열려 있습니다. 해당 씬을 닫거나 다른 파일 이름을 선택하세요.");
            }

            string prefabPath = ExportPrefab(sequence, registerForGame);
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), scene);
                root.GetComponent<StudioPlayer>().PlayOnStart = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(root.GetComponent<StudioPlayer>());
                var camera = new GameObject("Cutscene Camera", typeof(Camera), typeof(AudioListener));
                SceneManager.MoveGameObjectToScene(camera, scene);
                camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
                camera.GetComponent<Camera>().backgroundColor = Color.black;
                camera.GetComponent<Camera>().cullingMask = 0;
                if (!EditorSceneManager.SaveScene(scene, path))
                    throw new IOException("씬을 저장하지 못했습니다: " + path);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static StudioSequence Duplicate(StudioSequence source, string path)
        {
            var copy = Create(path);
            var generatedTimeline = copy.Timeline;
            string timelinePath = AssetDatabase.GetAssetPath(generatedTimeline);
            AssetDatabase.DeleteAsset(timelinePath);
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source.Timeline), timelinePath))
                throw new IOException("Timeline 복제에 실패했습니다.");
            EditorUtility.CopySerialized(source, copy);
            copy.Id = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(copy));
            copy.name = copy.Id;
            copy.DisplayName = copy.Id;
            copy.Timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            Save(copy);
            return copy;
        }

        public static StudioSequence CreateSample(string path)
        {
            var sequence = Create(path);
            var background = AddClip(sequence, StudioKind.Background, 0);
            background.duration = 10;
            ((StudioClip)background.asset).BackgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/01.Game/03.Content/01.ArtResources/07.Legacy/Cutscenes/NewPortHouse.png");
            var actor = new StudioActor
            {
                Id = "luna",
                Name = "루나",
                Position = new Vector2(-220, -130),
                Size = new Vector2(100, 150),
                Sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/01.Game/03.Content/01.ArtResources/07.Legacy/Cutscenes/Luna.png")
            };
            sequence.EditableActors.Add(actor);
            AddClip(sequence, StudioKind.Actor, 0, actor.Id);
            var line = AddClip(sequence, StudioKind.Dialogue, 3, actor.Id);
            ((StudioClip)line.asset).Text = "새 컷씬 스튜디오입니다.\n화면비가 달라도 같은 구도로 재생됩니다.";
            var fade = AddClip(sequence, StudioKind.Effect, 8);
            fade.duration = 2;
            ((StudioClip)fade.asset).Effect = StudioEffect.FadeOut;
            sequence.Timeline.fixedDuration = 10;
            Save(sequence);
            return sequence;
        }
    }
}
