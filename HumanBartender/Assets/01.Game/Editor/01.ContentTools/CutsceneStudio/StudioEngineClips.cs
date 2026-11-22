using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 엔진이 쓰는 스프라이트 교체 애니메이션 하나. 원본 에셋은 읽기만 한다.
    internal sealed class StudioEngineClip
    {
        internal readonly string Guid;
        internal readonly AnimationClip Clip;
        internal readonly Sprite[] Frames;
        internal readonly float[] Times;
        // 스프라이트 키 말고 무시한 내용. 없으면 null.
        internal readonly string Ignored;

        internal StudioEngineClip(string guid, AnimationClip clip, ObjectReferenceKeyframe[] keys, string ignored)
        {
            Guid = guid;
            Clip = clip;
            Frames = keys.Select(key => (Sprite)key.value).ToArray();
            Times = keys.Select(key => key.time).ToArray();
            Ignored = ignored;
        }

        internal string Name => Clip.name;
        internal StudioEngineSpeedInfo Speed => StudioEngineSpeed.Resolve(Clip);
        // 엔진에서 한 바퀴 도는 시간(초).
        internal float Cycle => Clip.length / Speed.Value;
    }

    // 스튜디오 프레임 목록을 엔진 클립과 잇고, 엔진과 속도가 다른 클립을 찾는다.
    internal static class StudioEngineClips
    {
        // 게임이 쓰는 클립만 후보로 찾는다. 연습용 폴더(Assets/02.Dev)는 뺀다.
        private const string EngineFolder = "Assets/01.Game";
        // 같은 칸을 다르게 자른 스프라이트도 같은 그림으로 본다. 같은 텍스처에서 작은 쪽 넓이의 이 비율 이상이 겹치면 같음.
        // (예: 스튜디오용 eyes_0 87×32가 엔진용 Game_eyes 339×385 칸 안에 들어 있음)
        private const float SamePictureOverlap = .5f;

        private static Dictionary<string, List<string>> clipsByTexture;
        private static readonly Dictionary<string, StudioEngineClip> loaded = new(StringComparer.Ordinal);

        // 캐시를 버릴 때마다 올라감. 검사 개수처럼 판정 결과를 들고 있는 곳이 다시 계산할 때 씀.
        internal static int Version { get; private set; }

        internal static void Invalidate()
        {
            clipsByTexture = null;
            loaded.Clear();
            Version++;
        }

        internal static StudioEngineClip Load(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return null;
            if (loaded.TryGetValue(guid, out var cached))
                return cached;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
            var result = clip != null ? Read(guid, clip) : null;
            loaded[guid] = result;
            return result;
        }

        internal static StudioEngineClip Load(AnimationClip clip)
        {
            if (clip == null)
                return null;
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long _) && AssetDatabase.IsMainAsset(clip)
                ? Load(guid)
                : Read(null, clip);
        }

        private static StudioEngineClip Read(string guid, AnimationClip clip) =>
            StudioAssetLibrary.ReadSpriteKeys(clip, out var keys, out string ignored) == null ? new StudioEngineClip(guid, clip, keys, ignored) : null;

        internal static StudioEngineClip Source(StudioClip clip) => Load(clip?.SourceAnimationGuid);

        // 같은 그림을 같은 순서로 쓰는 엔진 클립. 프레임이 2장 미만이면 찾지 않는다.
        internal static List<StudioEngineClip> FindMatches(Sprite[] frames)
        {
            var result = new List<StudioEngineClip>();
            if (frames == null || frames.Length < 2 || frames.Any(frame => frame == null))
                return result;
            string texture = AssetDatabase.GetAssetPath(frames[0]);
            if (string.IsNullOrEmpty(texture) || !Index().TryGetValue(texture, out var guids))
                return result;
            foreach (string guid in guids)
            {
                var candidate = Load(guid);
                if (candidate != null && SamePictures(frames, candidate.Frames))
                    result.Add(candidate);
            }
            // 같은 프레임을 쓰는 바 대화 Loop / Dialogue 중 기본 상태인 Loop를 먼저 고르게 함.
            string loop = "_" + CharacterLoader.SLOT_LOOP;
            result.Sort((a, b) =>
            {
                int order = (a.Name.EndsWith(loop, StringComparison.Ordinal) ? 0 : 1) - (b.Name.EndsWith(loop, StringComparison.Ordinal) ? 0 : 1);
                return order != 0 ? order : string.CompareOrdinal(a.Name, b.Name);
            });
            return result;
        }

        private static Dictionary<string, List<string>> Index()
        {
            if (clipsByTexture != null)
                return clipsByTexture;
            clipsByTexture = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { EngineFolder }))
                foreach (string dependency in AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(guid), false))
                {
                    if (!clipsByTexture.TryGetValue(dependency, out var list))
                        clipsByTexture.Add(dependency, list = new List<string>());
                    if (!list.Contains(guid))
                        list.Add(guid);
                }
            return clipsByTexture;
        }

        internal static bool SamePictures(Sprite[] a, Sprite[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (!SamePicture(a[i], b[i]))
                    return false;
            return true;
        }

        internal static bool SamePicture(Sprite a, Sprite b)
        {
            if (a == null || b == null)
                return false;
            if (a == b)
                return true;
            if (a.texture != b.texture)
                return false;
            Rect r1 = a.rect, r2 = b.rect;
            float width = Mathf.Min(r1.xMax, r2.xMax) - Mathf.Max(r1.xMin, r2.xMin);
            float height = Mathf.Min(r1.yMax, r2.yMax) - Mathf.Max(r1.yMin, r2.yMin);
            if (width <= 0 || height <= 0)
                return false;
            return width * height >= SamePictureOverlap * Mathf.Min(r1.width * r1.height, r2.width * r2.height);
        }

        // 원본의 키 시간 / 길이 / 반복 / 엔진 속도 보정 / 원본 연결을 넣는다. 같은 그림이면 지금 프레임 이미지는 유지함.
        // 재생 배속 / 원본 시작 / 타임라인 클립 길이는 바꾸지 않는다. Undo 기록은 부르는 쪽이 한다.
        internal static void Apply(StudioClip data, StudioEngineClip source, double frameDuration)
        {
            if (!SamePictures(data.Frames, source.Frames))
                data.Frames = (Sprite[])source.Frames.Clone();
            data.FrameTimes = (float[])source.Times.Clone();
            data.AnimationLength = Mathf.Max((float)frameDuration, source.Clip.length);
            data.Loop = source.Clip.isLooping;
            data.EngineSpeed = source.Speed.Value;
            data.SourceAnimationGuid = source.Guid;
        }

        // 연결이 끊긴 클립은 엔진 속도 보정도 원래대로 둔다.
        internal static void ClearLink(StudioClip data)
        {
            data.EngineSpeed = 1;
            data.SourceAnimationGuid = null;
        }

        // 엔진 클립 칸에 넣은 클립으로 연결한다. 이미 연결돼 있으면 그 클립으로 바꾼다.
        // 받으면 null, 받을 수 없으면 데이터를 건드리지 않고 이유를 돌려준다. Undo 기록은 부르는 쪽이 한다.
        internal static string Link(StudioClip data, AnimationClip clip, double frameDuration)
        {
            string problem = LinkProblem(clip);
            if (problem == null)
                Apply(data, Load(clip), frameDuration);
            return problem;
        }

        // 엔진 클립 칸에 넣을 수 없는 이유. 넣을 수 있으면 null. 데이터를 바꾸기 전에 확인할 때 씀.
        internal static string LinkProblem(AnimationClip clip)
        {
            if (clip == null)
                return "엔진 클립이 비어 있습니다.";
            var engine = Load(clip);
            if (engine == null)
                return StudioAssetLibrary.UnsupportedReason(clip) ?? "Sprite 교체 애니메이션이 아닙니다.";
            return string.IsNullOrEmpty(engine.Guid) ? "프로젝트에 .anim 파일로 저장된 클립만 연결할 수 있습니다." : null;
        }

        // 초당 프레임 방식으로 되돌린다. 프레임 / 반복 / 재생 배속 / 원본 시작 / 초당 프레임 / 타임라인 길이는 그대로 둔다.
        internal static void Unlink(StudioClip data)
        {
            data.FrameTimes = null;
            data.AnimationLength = 0;
            ClearLink(data);
        }

        // 연결이 있거나, 연결 없이 원본 키 시간으로 재생하는 예전 클립(1단계 이전에 애니메이션을 놓아 만든 것)이면 해제할 수 있다.
        internal static bool CanUnlink(StudioClip data) =>
            !string.IsNullOrEmpty(data.SourceAnimationGuid) || StudioPropertyEditing.UsesSourceTiming(data);

        // 연결된 원본 AnimationClip. Sprite 교체 애니메이션이 아니게 바뀌었어도 칸에 보여 줄 수 있게 따로 읽는다.
        internal static AnimationClip SourceClip(StudioClip data) => string.IsNullOrEmpty(data?.SourceAnimationGuid) ? null :
            AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(data.SourceAnimationGuid));

        // 비교할 엔진 클립. 연결된 원본과 프레임이 같으면 그 원본 하나, 아니면 같은 그림을 쓰는 후보들.
        internal static List<StudioEngineClip> References(StudioClip clip, out bool linked)
        {
            var source = Source(clip);
            linked = source != null && SamePictures(clip.Frames, source.Frames);
            return linked ? new List<StudioEngineClip> { source } : FindMatches(clip.Frames);
        }

        internal static bool SameLength(float current, float engine) =>
            Mathf.Abs(current - engine) <= Mathf.Max(.005f, engine * .01f);

        // 속성 창 상태 줄과 검사 목록이 함께 쓰는 판정. 둘이 다른 말을 하지 않도록 여기서만 정한다.
        internal static StudioEngineStatus Evaluate(StudioClip clip)
        {
            float current = StudioSpriteAnimation.CycleLength(clip);
            bool hasLink = !string.IsNullOrEmpty(clip.SourceAnimationGuid);
            var source = Source(clip);
            if (hasLink && source == null)
                return new StudioEngineStatus(StudioEngineMatch.MissingSource, current, null, false);
            if (clip.Frames == null || clip.Frames.Length < 2)
                return new StudioEngineStatus(StudioEngineMatch.None, current, null, hasLink);
            var engines = References(clip, out _).Where(engine => !engine.Speed.Ambiguous).ToList();
            if (engines.Count == 0)
                return new StudioEngineStatus(StudioEngineMatch.None, current, null, hasLink);
            var same = engines.FirstOrDefault(engine => SameLength(current, engine.Cycle));
            return same != null
                ? new StudioEngineStatus(StudioEngineMatch.Same, current, same, hasLink)
                : new StudioEngineStatus(StudioEngineMatch.Different, current, engines[0], hasLink);
        }

        // 엔진과 한 바퀴 길이가 다르거나 연결된 원본을 잃은 그림 클립. 재생은 가능하므로 참고 항목으로만 알린다.
        internal static List<string> CollectWarnings(StudioSequence sequence)
        {
            var result = new List<string>();
            foreach (var entry in StudioEvaluation.Clips(sequence))
            {
                if (entry.Asset.Kind != StudioKind.Visual)
                    continue;
                var status = Evaluate(entry.Asset);
                string name = entry.Clip.displayName;
                if (status.Match == StudioEngineMatch.MissingSource)
                    result.Add($"{name}: 연결된 엔진 클립을 찾을 수 없어 저장된 값으로 재생합니다 (한 바퀴 {status.Current:0.00}초). " +
                        "속성 창의 '엔진 클립' 칸에 클립을 다시 넣거나 연결을 해제하세요.");
                else if (status.Match == StudioEngineMatch.Different)
                    result.Add($"{name}: 엔진과 애니메이션 속도가 다릅니다. 현재 한 바퀴 {status.Current:0.00}초, 엔진 {status.Engine.Cycle:0.00}초 " +
                        $"('{status.Engine.Name}' · {status.Engine.Speed.Reason}). " +
                        (status.Linked ? "재생 배속을 확인하거나 속성 창 '엔진 클립' 옆의 ↻(원본에서 다시 가져오기)를 누르세요." :
                            $"속성 창의 '엔진 클립' 칸에 '{status.Engine.Name}'을 넣으면 맞출 수 있습니다."));
            }
            return result;
        }
    }

    internal enum StudioEngineMatch
    {
        None, // 비교할 엔진 클립이 없거나 엔진 속도를 하나로 정할 수 없음
        Same,
        Different,
        MissingSource // 연결된 원본 파일을 찾을 수 없음
    }

    internal readonly struct StudioEngineStatus
    {
        internal readonly StudioEngineMatch Match;
        // 지금 한 바퀴 길이(초). 재생 배속까지 반영한 실제 값.
        internal readonly float Current;
        // 비교한 엔진 클립. Same / Different일 때만 있음.
        internal readonly StudioEngineClip Engine;
        internal readonly bool Linked;

        internal StudioEngineStatus(StudioEngineMatch match, float current, StudioEngineClip engine, bool linked)
        {
            Match = match;
            Current = current;
            Engine = engine;
            Linked = linked;
        }
    }
}
