using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace HumanBartender.CutsceneStudio.Editor
{
    public enum StudioAssetType
    {
        All,
        Sprite,
        Animation,
        Object,
        Effect,
        Audio
    }

    public sealed class StudioLibraryItem
    {
        public Object Asset;
        public StudioAssetType Type;
        public string Path;
        public string Name => Asset == null ? "누락된 에셋" : Asset.name;

        private bool checkedSupport;
        private string reason, notice;
        public string Unsupported
        {
            get
            {
                Check();
                return reason;
            }
        }

        // 받을 수는 있지만 일부 내용을 무시하는 경우의 안내. 없으면 null.
        public string Notice
        {
            get
            {
                Check();
                return notice;
            }
        }

        private void Check()
        {
            if (checkedSupport)
                return;
            reason = StudioAssetLibrary.UnsupportedReason(Asset);
            notice = reason == null ? StudioAssetLibrary.IgnoredContent(Asset) : null;
            checkedSupport = true;
        }
    }

    // 에디터에서 에셋을 검색하고 변환하며 원본 프로젝트 에셋은 수정하지 않음.
    public static class StudioAssetLibrary
    {
        public static readonly string[] TypeNames =
        {
            "전체",
            "단일 스프라이트",
            "애니메이션",
            "오브젝트",
            "이펙트",
            "사운드"
        };
        public static StudioLibraryItem[] Scan(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return Array.Empty<StudioLibraryItem>();
            var found = new List<StudioLibraryItem>();
            var paths = new HashSet<string>();
            foreach (string filter in new[]
            {
                "t:Sprite",
                "t:AnimationClip",
                "t:AudioClip",
                "t:Prefab"
            }

            )
                foreach (var guid in AssetDatabase.FindAssets(filter, new[] { folder }))
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            foreach (string path in paths)
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Sprite || asset is AnimationClip || asset is AudioClip || asset is GameObject)
                        found.Add(new StudioLibraryItem { Asset = asset, Path = path, Type = Classify(asset, path) });
            return found.OrderBy(SelectFoundOrderBy, StringComparer.OrdinalIgnoreCase).ToArray();
            string SelectFoundOrderBy(StudioLibraryItem a)
            {
                return a.Name;
            }
        }

        public static StudioAssetType Classify(Object asset, string path)
        {
            if (asset is AudioClip)
                return StudioAssetType.Audio;
            bool effect = path.IndexOf("/03.Vfx/", StringComparison.OrdinalIgnoreCase) >= 0 || AssetDatabase.GetLabels(asset).Any(MatchesAssetAny);
            if (effect)
                return StudioAssetType.Effect;
            if (asset is AnimationClip)
                return StudioAssetType.Animation;
            return asset is GameObject ? StudioAssetType.Object : StudioAssetType.Sprite;
            bool MatchesAssetAny(string l)
            {
                return l.Equals("CutsceneEffect", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool Matches(StudioLibraryItem item, StudioAssetType type, string search) => (type == StudioAssetType.All || item.Type == type) && (string.IsNullOrWhiteSpace(search) || item.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || item.Path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        public static Object Normalize(Object source)
        {
            if (source is Texture2D)
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(source)).OfType<Sprite>().ToArray();
                if (sprites.Length == 1)
                    return sprites[0];
                return null; // 분할된 시트는 개별 프레임이나 애니메이션 선택이 필요함.
            }

            return source;
        }

        public static string UnsupportedReason(Object source)
        {
            source = Normalize(source);
            if (source is Sprite || source is AudioClip)
                return null;
            if (source is AnimationClip animation)
                return ReadSpriteKeys(animation, out _, out _);

            if (source is GameObject prefab)
            {
                var sprites = prefab.GetComponentsInChildren<SpriteRenderer>(true);
                // 단순 이미지 프리팹은 게임 스크립트를 실행하지 않고 이미지로 변환함.
                if (sprites.Length != 1 || sprites[0].gameObject != prefab || sprites[0].sprite == null || sprites[0].flipY || prefab.transform.localScale.y < 0 || sprites[0].drawMode != SpriteDrawMode.Simple || prefab.transform.childCount != 0 || prefab.GetComponents<Component>().Any(MatchesComponentAny))
                    return "단일 SpriteRenderer 프리팹을 지원합니다. 파티클 / Animator / 스크립트 / 여러 자식이 있는 프리팹은 별도 재생 지원이 필요합니다.";
                return null;
                bool MatchesComponentAny(UnityEngine.Component c)
                {
                    return c is not Transform && c is not SpriteRenderer;
                }
            }

            return "Sprite, Sprite 애니메이션, 단일 Sprite 프리팹, AudioClip을 선택하세요. 분할 시트는 개별 Sprite를 선택하세요.";
        }

        // 받을 수 있는 애니메이션에서 스프라이트 키 말고 무시하는 내용. 없거나 애니메이션이 아니면 null.
        public static string IgnoredContent(Object source)
        {
            if (Normalize(source) is not AnimationClip animation)
                return null;
            return ReadSpriteKeys(animation, out _, out string ignored) == null ? ignored : null;
        }

        // 스프라이트 교체 곡선이 정확히 하나면 그 키만 읽는다(대상 컴포넌트 종류는 묻지 않음).
        // 다른 곡선 / 이벤트 / 대상 종류는 재생하지 않고 ignored에 적는다. 받을 수 없으면 이유를 돌려준다.
        internal static string ReadSpriteKeys(AnimationClip animation, out ObjectReferenceKeyframe[] keys, out string ignored)
        {
            keys = null;
            ignored = null;
            var references = AnimationUtility.GetObjectReferenceCurveBindings(animation);
            var sprites = references.Where(binding => binding.propertyName == "m_Sprite").ToArray();
            if (sprites.Length == 0)
                return "Sprite 교체 곡선이 없는 애니메이션입니다. Transform / 기타 속성 / 이벤트만 있는 애니메이션은 지원하지 않습니다.";
            if (sprites.Length > 1)
                return "Sprite 교체 곡선이 여러 개인 애니메이션입니다. 오브젝트 하나의 Sprite만 바꾸는 애니메이션을 지원합니다.";
            var read = AnimationUtility.GetObjectReferenceCurve(animation, sprites[0]);
            if (read.Length == 0 || read.Any(key => key.value is not Sprite))
                return "Sprite 프레임이 없는 애니메이션입니다.";
            keys = read;
            var parts = new List<string>();
            int curves = AnimationUtility.GetCurveBindings(animation).Length + references.Length - 1;
            if (curves > 0)
                parts.Add($"다른 속성 곡선 {curves}개");
            int events = AnimationUtility.GetAnimationEvents(animation).Length;
            if (events > 0)
                parts.Add($"이벤트 {events}개");
            if (sprites[0].type != typeof(SpriteRenderer))
                parts.Add($"원래 대상 {(sprites[0].type != null ? sprites[0].type.Name : "알 수 없음")}");
            if (parts.Count > 0)
                ignored = "Sprite 교체 키만 사용합니다. 무시한 내용: " + string.Join(" / ", parts);
            return null;
        }

        public static TimelineClip Place(StudioSequence sequence, Object source, double time, Vector2 position, StudioTrack target = null)
        {
            source = Normalize(source);
            string reason = UnsupportedReason(source);
            if (reason != null)
                throw new InvalidOperationException(reason);
            var kind = source is AudioClip ? StudioKind.Audio : StudioKind.Visual;
            if (target != null && (target.timelineAsset != sequence.Timeline || target.GetClips().Any(MatchesGetclipsAny)))
                throw new InvalidOperationException("같은 종류의 트랙에 놓아 주세요.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("컷씬 에셋 배치");
            Undo.RecordObject(sequence, "컷씬 에셋 배치");
            string actorId = null;
            StudioEngineClip engine = null;
            if (source is not AudioClip)
            {
                var sprite = source as Sprite;
                var color = Color.white;
                float rotation = 0;
                var prefabScale = Vector2.one;
                if (source is AnimationClip animation)
                {
                    engine = StudioEngineClips.Load(animation) ?? throw new InvalidOperationException(UnsupportedReason(animation));
                    sprite = engine.Frames[0];
                }

                if (source is GameObject prefab)
                {
                    var renderer = prefab.GetComponent<SpriteRenderer>();
                    sprite = renderer.sprite;
                    color = renderer.color;
                    prefabScale = prefab.transform.localScale;
                    rotation = prefab.transform.localEulerAngles.z;
                }

                actorId = "actor_" + Guid.NewGuid().ToString("N").Substring(0, 10);
                float scale = Mathf.Min(1, 300f / Mathf.Max(sprite.rect.width, sprite.rect.height));
                sequence.EditableActors.Add(new StudioActor { Id = actorId, Name = source.name, Sprite = sprite, Position = position, Size = sprite.rect.size * scale * new Vector2(Mathf.Abs(prefabScale.x), Mathf.Abs(prefabScale.y)), Color = color, ClipControlled = true, Layer = sequence.Actors.Count });
                var clip = StudioEditorAssets.AddClip(sequence, kind, time, actorId, target);
                var data = (StudioClip)clip.asset;
                data.From = data.To = position;
                data.RotationFrom = data.RotationTo = rotation;
                data.Frames = new[]
                {
                    sprite
                };
                data.FrameTimes = null;
                data.AnimationLength = 0;
                data.Loop = false;
                if (engine != null)
                    StudioEngineClips.Apply(data, engine, StudioTiming.FrameDuration(sequence));
                // 반복 애니메이션을 새 트랙에 놓으면 그 트랙은 입력 대기 중에도 계속 재생함. 기존 트랙의 설정은 건드리지 않음.
                if (target == null && engine != null && data.Loop && clip.GetParentTrack() is StudioTrack created)
                {
                    Undo.RecordObject(created, "컷씬 에셋 배치");
                    created.ContinueDuringWait = true;
                    EditorUtility.SetDirty(created);
                }
                if (source is GameObject go)
                    data.FlipX = go.GetComponent<SpriteRenderer>().flipX ^ (prefabScale.x < 0);
                // 애니메이션은 엔진에서 한 바퀴 도는 길이로 시작함.
                clip.duration = engine != null ? Math.Max(StudioTiming.FrameDuration(sequence), StudioSpriteAnimation.CycleLength(data)) : 4;
                clip.displayName = source.name;
                sequence.Timeline.fixedDuration = Math.Max(sequence.Duration, clip.end);
                EditorUtility.SetDirty(data);
                Undo.CollapseUndoOperations(group);
                return clip;
            }

            var soundClip = StudioEditorAssets.AddClip(sequence, kind, time, null, target);
            var soundData = (StudioClip)soundClip.asset;
            soundData.Sound = (AudioClip)source;
            soundData.Loop = false;
            soundClip.duration = Math.Max(StudioTiming.FrameDuration(sequence), soundData.Sound.length);
            soundClip.displayName = source.name;
            sequence.Timeline.fixedDuration = Math.Max(sequence.Duration, soundClip.end);
            EditorUtility.SetDirty(soundData);
            Undo.CollapseUndoOperations(group);
            return soundClip;
            bool MatchesGetclipsAny(TimelineClip c)
            {
                return c.asset is StudioClip s && s.Kind != kind;
            }
        }
    }
}
