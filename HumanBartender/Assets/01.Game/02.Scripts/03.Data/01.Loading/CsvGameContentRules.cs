using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>Pre-publication checks for game IDs and order contracts. Does not execute effects or own game state.</summary>
public static class CsvGameContentRules
{
    /// <summary>
    /// craft.arg의 튜토리얼 표기와 실제 칵테일 id의 대응. 정본 CSV는 `tutorial_recipe`처럼 수업 이름만 적으므로
    /// 칵테일 id는 이 표 한 곳에서만 정한다. 실행기(StoryScriptRunner)와 검증기가 같은 표를 쓴다.
    /// </summary>
    static readonly Dictionary<string, string> CraftTutorialCocktails = new(StringComparer.Ordinal)
    {
        ["tutorial_recipe"] = "gin_tonic",
        ["tutorial_beer"] = "bottle_beer",
    };

    /// <summary>
    /// craft.arg가 튜토리얼 제조면 그 칵테일 id를, 주문대로 만드는 제조("order"·빈 값)나 알 수 없는 표기면 null을 돌려준다.
    /// `tutorial:&lt;id&gt;` 직접 표기와 수업 이름 표기를 모두 받는다.
    /// </summary>
    public static string ResolveCraftTutorialCocktail(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg)) return null;
        string value = arg.Trim();
        const string prefix = "tutorial:";
        if (value.StartsWith(prefix, StringComparison.Ordinal))
        {
            string id = value.Substring(prefix.Length).Trim();
            return id.Length > 0 ? id : null;
        }
        return CraftTutorialCocktails.TryGetValue(value, out string cocktail) ? cocktail : null;
    }

    public static void Validate(CsvContentCatalog catalog)
    {
        var outsideIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string source in new[] { "script/street", "script/home" })
            foreach (var scene in StoryCsvCompiler.Compile(catalog.Read<NewStreetData>(source)).Scenes ?? Array.Empty<NewSceneData>())
                if (!outsideIds.Add(scene.Id)) throw new InvalidOperationException("Duplicate outside scene id: " + scene.Id);
        foreach (string id in StoryCsvCompiler.RequiredScenes)
            if (!outsideIds.Contains(id)) throw new InvalidOperationException("Missing native event scene reference: " + id);
        foreach (string source in catalog.SourceIds)
        {
            bool qa = source == "script/bar/day99" || source.StartsWith("script/qa/", StringComparison.Ordinal) || source.StartsWith("qa/", StringComparison.Ordinal);
            // QA-only scripts retain unsupported feature probes (e.g. reputation). Their structure/IDs/types
            // are checked, while execution still rejects unsupported effects through IConditionUtil.
            bool active = source.StartsWith("script/bar/", StringComparison.Ordinal) || source == "script/street" || source == "script/home" || source.StartsWith("interact_points_", StringComparison.Ordinal);
            Check(catalog.ReadUntyped(source), source, !qa && active);
        }
        var choiceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string source in catalog.SourceIds)
        {
            if (!source.StartsWith("script/bar/", StringComparison.Ordinal)) continue;
            var script = StoryCsvCompiler.Compile(catalog.Read<NewDayScriptBase>(source));
            foreach (var id in script.Choices?.Keys ?? (IEnumerable<string>)Array.Empty<string>())
                if (!choiceIds.Add(id)) throw new InvalidOperationException("Duplicate shared choice id: " + id);
            var scenes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scene in script.Scenes ?? Array.Empty<NewScriptSceneData>()) scenes.Add(scene.Id);
            foreach (var scene in script.Scenes ?? Array.Empty<NewScriptSceneData>())
            {
                int previous = -1;
                foreach (var step in scene.Steps ?? Array.Empty<NewDialogueStepData>())
                {
                    if (step.Seq <= previous) throw new InvalidOperationException("Bar source_order/seq mismatch: " + source + "/" + scene.Id);
                    previous = step.Seq;
                    if (step.Type == ENewStepType.Choice && (script.Choices == null || !script.Choices.ContainsKey(step.Arg ?? "")))
                        throw new InvalidOperationException("Missing choice group: " + step.Arg);
                }
            }
            foreach (var options in script.Choices?.Values ?? (IEnumerable<NewChoiceOptionData[]>)Array.Empty<NewChoiceOptionData[]>())
                foreach (var option in options)
                    if (!string.IsNullOrEmpty(option.Goto) && !scenes.Contains(option.Goto))
                        throw new InvalidOperationException("Missing goto scene: " + option.Goto);
        }
    }

    static void Check(object value, string path, bool storySyntax)
    {
        if (value is IDictionary<string, object> fields)
        {
            foreach (var field in fields)
            {
                if (storySyntax && field.Value is string text)
                {
                    string error = field.Key == "effects" ? ConditionUtil.ValidateEffectSyntax(text)
                        : field.Key is "when" or "spawn_when" or "interact_when" ? ConditionUtil.ValidateConditionSyntax(text) : null;
                    if (error != null) throw new InvalidOperationException(path + "/" + field.Key + ": " + error);
                }
                Check(field.Value, path + "/" + field.Key, storySyntax);
            }
        }
        else if (value is IList list)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in list)
            {
                if (item is IDictionary<string, object> record && record.TryGetValue("id", out var id) && id is string key && !ids.Add(key))
                    throw new InvalidOperationException("Duplicate game id: " + path + "/" + key);
                Check(item, path, storySyntax);
            }
        }
    }
}
