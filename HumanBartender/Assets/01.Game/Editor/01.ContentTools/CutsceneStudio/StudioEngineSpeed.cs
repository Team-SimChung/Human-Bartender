using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Animations;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal readonly struct StudioEngineSpeedInfo
    {
        internal readonly float Value;
        internal readonly string Reason;
        // 같은 클립을 엔진이 서로 다른 속도로 재생해 하나로 정할 수 없음. Value는 1로 두고 보정하지 않음.
        internal readonly bool Ambiguous;

        internal StudioEngineSpeedInfo(float value, string reason, bool ambiguous = false)
        {
            Value = value;
            Reason = reason;
            Ambiguous = ambiguous;
        }

        internal string Label => $"×{Value:0.##} ({Reason})";
    }

    // 엔진이 AnimationClip을 재생할 때 원본 속도에 곱하는 값. 규칙은 이 클래스에만 둔다.
    // - 바 대화: CharacterLoader가 {clip}_Loop / _Intro / _Dialogue 주소의 클립을 CH.controller의 세 상태에 넣고,
    //   CharacterPart.PlayAnimation("Intro")가 Intro 슬롯 클립 길이 ÷ TARGET_LENGTH를 ClipSpeed로 넣는다.
    //   세 상태 모두 같은 ClipSpeed(상태 속도 1)를 곱하므로 Loop·Dialogue도 Intro 슬롯 길이를 따른다.
    //   Intro 클립이 없으면 Loop 클립이 Intro 슬롯에 들어간다.
    // - 컨트롤러 상태가 직접 쓰는 클립: 상태 속도(블렌드 트리는 자식 timeScale까지 곱함).
    // - 그 외: 1. 속도 파라미터처럼 실행 중 코드가 바꾸는 속도는 계산에서 뺀다.
    internal static class StudioEngineSpeed
    {
        private const string ExpressionPartsFile = "csv/characters/expression_parts.csv";
        private const string ResourceBindingsFile = "unity/resource_bindings.csv";
        private const float SameSpeed = .0005f;
        private const int ShownSources = 2;

        internal sealed class Use
        {
            internal readonly float Speed; // NaN: 실행 중에 정해짐
            internal readonly string Source;
            internal readonly string BarIntroGuid; // 바 대화: ClipSpeed를 정하는 Intro 슬롯 클립
            internal readonly bool InExpressionCsv;

            private Use(float speed, string source, string barIntroGuid, bool inExpressionCsv)
            {
                Speed = speed;
                Source = source;
                BarIntroGuid = barIntroGuid;
                InExpressionCsv = inExpressionCsv;
            }

            internal static Use State(float speed, string source) => new(speed, source, null, false);
            internal static Use Runtime(string source) => new(float.NaN, source, null, false);
            internal static Use BarDialogue(string introGuid, bool inExpressionCsv) => new(float.NaN, "바 대화", introGuid, inExpressionCsv);
            internal bool IsBarDialogue => BarIntroGuid != null;
        }

        private static Dictionary<string, List<Use>> uses;
        private static readonly Dictionary<string, StudioEngineSpeedInfo> resolved = new(StringComparer.Ordinal);

        internal static void Invalidate()
        {
            uses = null;
            resolved.Clear();
        }

        internal static StudioEngineSpeedInfo Resolve(AnimationClip clip)
        {
            if (clip == null)
                return new StudioEngineSpeedInfo(1, "원본 클립 없음");
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long _))
                return new StudioEngineSpeedInfo(1, "프로젝트 에셋이 아님 · 원본 속도");
            if (resolved.TryGetValue(guid, out var info))
                return info;
            info = Combine(Uses().TryGetValue(guid, out var found) ? found : new List<Use>(), clip.length);
            resolved.Add(guid, info);
            return info;
        }

        // 같은 클립의 쓰임을 하나의 값과 이유로 합친다. 서로 다른 고정 속도가 있으면 고르지 않는다.
        internal static StudioEngineSpeedInfo Combine(IReadOnlyList<Use> clipUses, float clipLength)
        {
            var fixedUses = new List<(float Speed, string Source)>();
            var runtime = new List<string>();
            foreach (var use in clipUses)
            {
                if (use.IsBarDialogue)
                    fixedUses.Add(BarDialogueSpeed(use, clipLength));
                else if (float.IsNaN(use.Speed))
                    runtime.Add(use.Source);
                else
                    fixedUses.Add((use.Speed, use.Source));
            }

            var speeds = new List<float>();
            foreach (var use in fixedUses)
                if (!speeds.Any(speed => Mathf.Abs(speed - use.Speed) <= SameSpeed))
                    speeds.Add(use.Speed);
            string runtimeNote = runtime.Count == 0 ? "" : " · 계산에서 뺀 곳: " + Join(runtime);
            if (speeds.Count == 0)
                return new StudioEngineSpeedInfo(1, runtime.Count == 0 ? "엔진이 원본 속도로 재생" : "원본 속도 기준" + runtimeNote);
            if (speeds.Count == 1)
                return new StudioEngineSpeedInfo(speeds[0], Join(fixedUses.Select(use => use.Source)) + runtimeNote);
            string all = Join(fixedUses.Select(use => $"{use.Source} ×{use.Speed:0.##}"), int.MaxValue);
            return new StudioEngineSpeedInfo(1, $"여러 곳에서 다른 속도로 재생: {all} · 보정하지 않음, 재생 배속으로 맞추세요" + runtimeNote, true);
        }

        private static (float Speed, string Source) BarDialogueSpeed(Use use, float clipLength)
        {
            var intro = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(use.BarIntroGuid));
            float speed = Mathf.Max(.01f, (intro != null ? intro.length : clipLength) / AnimationPart.TARGET_LENGTH);
            float cycle = clipLength / speed;
            string source = use.InExpressionCsv ? "바 대화" : "바 대화 규칙 · 표정 CSV에서는 아직 쓰지 않음";
            source += Mathf.Abs(cycle - AnimationPart.TARGET_LENGTH) <= SameSpeed
                ? $" · {AnimationPart.TARGET_LENGTH:0.##}초에 한 바퀴"
                : $" · Intro 슬롯 '{(intro != null ? intro.name : "?")}' 길이 기준, 한 바퀴 {cycle:0.00}초";
            return (speed, source);
        }

        private static string Join(IEnumerable<string> sources, int shown = ShownSources)
        {
            var distinct = sources.Distinct().ToList();
            string text = string.Join(" / ", distinct.Take(shown));
            return distinct.Count > shown ? $"{text} 외 {distinct.Count - shown}곳" : text;
        }

        private static Dictionary<string, List<Use>> Uses()
        {
            if (uses != null)
                return uses;
            uses = new Dictionary<string, List<Use>>(StringComparer.Ordinal);
            CollectBarDialogue(uses);
            foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                // 같은 이름의 컨트롤러가 여럿이라(예: Actor.controller) 폴더 이름까지 적는다.
                if (controller != null)
                    CollectController(controller, $"{Path.GetFileName(Path.GetDirectoryName(path))}/{Path.GetFileName(path)}", uses);
            }
            return uses;
        }

        private static void Add(Dictionary<string, List<Use>> into, string clipGuid, Use use)
        {
            if (!into.TryGetValue(clipGuid, out var list))
                into.Add(clipGuid, list = new List<Use>());
            list.Add(use);
        }

        internal static void CollectController(AnimatorController controller, string label, Dictionary<string, List<Use>> into)
        {
            foreach (var layer in controller.layers)
                if (layer.stateMachine != null)
                    Walk(layer.stateMachine);

            void Walk(AnimatorStateMachine machine)
            {
                foreach (var child in machine.states)
                {
                    var state = child.state;
                    if (state != null)
                        AddMotion(state.motion, state.speed, $"{label} '{state.name}'", state.speedParameterActive ? state.speedParameter : null);
                }
                foreach (var child in machine.stateMachines)
                    if (child.stateMachine != null)
                        Walk(child.stateMachine);
            }

            void AddMotion(Motion motion, float speed, string source, string parameter)
            {
                if (motion is BlendTree tree)
                {
                    foreach (var child in tree.children)
                        AddMotion(child.motion, speed * child.timeScale, source, parameter);
                    return;
                }
                if (motion is not AnimationClip clip || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long _))
                    return;
                if (parameter != null)
                    Add(into, guid, Use.Runtime($"{source} (실행 중 속도 파라미터 '{parameter}')"));
                else if (speed <= 0)
                    Add(into, guid, Use.Runtime($"{source} (상태 속도 {speed:0.##}는 스튜디오에서 재현하지 않음)"));
                else
                    Add(into, guid, Use.State(speed, source));
            }
        }

        private static void CollectBarDialogue(Dictionary<string, List<Use>> into)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[CutsceneStudio] Addressables 설정이 없어 바 대화 클립의 엔진 속도를 계산하지 않습니다.");
                return;
            }
            var entries = new List<AddressableAssetEntry>();
            settings.GetAllAssets(entries, true);
            var clips = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in entries)
                if (!string.IsNullOrEmpty(entry.address) && !clips.ContainsKey(entry.address) &&
                    AssetDatabase.GetMainAssetTypeAtPath(entry.AssetPath) == typeof(AnimationClip))
                    clips.Add(entry.address, entry.guid);
            var bindings = ReadAnimationBindings();

            string Find(string key) => clips.TryGetValue(bindings.TryGetValue(key, out string bound) ? bound : key, out string guid) ? guid : null;

            // 로더는 표정 데이터의 clip 값에 슬롯 이름을 붙인 주소로 찾는다. 표정 CSV가 아직 쓰지 않는 세트도
            // 같은 주소 규칙으로 올라와 있으면 같은 방식으로 재생될 클립으로 본다.
            var expressionClips = ReadExpressionClips();
            var bases = new HashSet<string>(expressionClips, StringComparer.Ordinal);
            string loopSuffix = "_" + CharacterLoader.SLOT_LOOP;
            foreach (string key in clips.Keys.Concat(bindings.Keys))
                if (key.EndsWith(loopSuffix, StringComparison.Ordinal))
                    bases.Add(key.Substring(0, key.Length - loopSuffix.Length));
            foreach (string clip in bases)
            {
                string loop = Find(CharacterLoader.LoopResourceKey(clip));
                if (loop == null)
                    continue; // Loop가 없으면 로더가 스프라이트 한 장으로 물러섬.
                string intro = Find(CharacterLoader.AnimationAddress(clip, CharacterLoader.SLOT_INTRO));
                string dialogue = Find(CharacterLoader.AnimationAddress(clip, CharacterLoader.SLOT_DIALOGUE));
                bool inCsv = expressionClips.Contains(clip);
                var use = Use.BarDialogue(intro ?? loop, inCsv);
                Add(into, loop, use);
                if (intro != null) Add(into, intro, use);
                if (dialogue != null) Add(into, dialogue, use);
            }
        }

        private static HashSet<string> ReadExpressionClips()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in ReadCsv(ExpressionPartsFile))
                if (row.TryGetValue("clip", out string clip) && !string.IsNullOrWhiteSpace(clip))
                    result.Add(clip.Trim());
            return result;
        }

        private static Dictionary<string, string> ReadAnimationBindings()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in ReadCsv(ResourceBindingsFile))
                if (row.TryGetValue("asset_type", out string type) && type == nameof(AnimationClip) &&
                    !string.IsNullOrWhiteSpace(row["source_key"]) && !string.IsNullOrWhiteSpace(row["unity_key"]))
                    result[row["source_key"]] = row["unity_key"];
            return result;
        }

        private static List<Dictionary<string, string>> ReadCsv(string file)
        {
            string path = Path.Combine(Application.streamingAssetsPath, file);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CutsceneStudio] '{file}'이 없어 바 대화 클립 판정에서 뺍니다.");
                return new List<Dictionary<string, string>>();
            }
            return CsvContentCatalog.ParseRows(File.ReadAllText(path), path);
        }
    }

    // 엔진 속도를 정하는 에셋(클립·컨트롤러·CSV·Addressables)이 바뀌면 캐시를 버린다.
    internal sealed class StudioEngineCachePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!Affects(imported) && !Affects(deleted) && !Affects(moved) && !Affects(movedFrom))
                return;
            StudioEngineSpeed.Invalidate();
            StudioEngineClips.Invalidate();
        }

        private static bool Affects(string[] paths)
        {
            foreach (string path in paths)
                if (path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".controller", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("Assets/AddressableAssetsData/", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
