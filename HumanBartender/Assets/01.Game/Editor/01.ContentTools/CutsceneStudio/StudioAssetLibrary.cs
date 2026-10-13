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
        private string reason;
        public string Unsupported
        {
            get
            {
                if (!checkedSupport)
                {
                    reason = StudioAssetLibrary.UnsupportedReason(Asset);
                    checkedSupport = true;
                }

                return reason;
            }
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
            {
                var bindings = AnimationUtility.GetObjectReferenceCurveBindings(animation);
                if (AnimationUtility.GetCurveBindings(animation).Length != 0 || bindings.Length != 1 || bindings[0].type != typeof(SpriteRenderer) || bindings[0].propertyName != "m_Sprite" || AnimationUtility.GetAnimationEvents(animation).Length != 0)
                    return "SpriteRenderer의 Sprite 교체 애니메이션을 지원합니다. Transform / 기타 속성 / 이벤트 애니메이션은 아직 지원하지 않습니다.";
                var keys = AnimationUtility.GetObjectReferenceCurve(animation, bindings[0]);
                if (keys.Length == 0 || keys.Any(MatchesKeysAny))
                    return "Sprite 프레임이 없는 애니메이션입니다.";
                return null;
                bool MatchesKeysAny(UnityEditor.ObjectReferenceKeyframe k)
                {
                    return k.value is not Sprite;
                }
            }

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
            Sprite[] frames = null;
            float[] times = null;
            float animationLength = 0;
            bool loop = false;
            if (source is not AudioClip)
            {
                var sprite = source as Sprite;
                var color = Color.white;
                float rotation = 0;
                var prefabScale = Vector2.one;
                if (source is AnimationClip animation)
                {
                    var binding = AnimationUtility.GetObjectReferenceCurveBindings(animation)[0];
                    var keys = AnimationUtility.GetObjectReferenceCurve(animation, binding);
                    frames = keys.Select(SelectKeysSelect).ToArray();
                    times = keys.Select(SelectKeysSelect2).ToArray();
                    sprite = frames[0];
                    animationLength = Mathf.Max((float)StudioTiming.FrameDuration(sequence), animation.length);
                    loop = animation.isLooping;
                    UnityEngine.Sprite SelectKeysSelect(UnityEditor.ObjectReferenceKeyframe k)
                    {
                        return (Sprite)k.value;
                    }

                    float SelectKeysSelect2(UnityEditor.ObjectReferenceKeyframe k)
                    {
                        return k.time;
                    }
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
                data.Frames = frames ?? new[]
                {
                    sprite
                };
                data.FrameTimes = times;
                data.AnimationLength = animationLength;
                data.Loop = loop;
                if (source is GameObject go)
                    data.FlipX = go.GetComponent<SpriteRenderer>().flipX ^ (prefabScale.x < 0);
                clip.duration = animationLength > 0 ? animationLength : 4;
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
