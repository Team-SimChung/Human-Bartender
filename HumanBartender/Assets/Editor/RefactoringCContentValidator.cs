using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>CSV 편집 후 참조·조건식·리소스 오류를 검사한다.</summary>
public static class RefactoringCContentValidator
{
    sealed class Finding
    {
        public string Severity, Location, Message;
        public override string ToString() => $"{Severity} | {Location} | {Message}";
    }

    [MenuItem("Tools/Story/Validate C Content")]
    public static void ValidateMenu()
    {
        var findings = ValidateProject();
        foreach (var finding in findings)
            if (finding.Severity == "ERROR") Debug.LogError("[C Content] " + finding);
            else Debug.LogWarning("[C Content] " + finding);
        Debug.Log($"[C Content] Errors: {findings.Count(f=>f.Severity=="ERROR")}, warnings: {findings.Count(f=>f.Severity=="WARN")}. No assets were changed.");
    }

    static List<Finding> ValidateProject()
    {
        var issues = new List<Finding>();
        void Add(string severity, string location, string message) => issues.Add(new Finding { Severity=severity, Location=location, Message=message });
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
        var cuts = catalog.Read<NewCutSceneRefData[]>("cutscenes");
        var characters = new HashSet<string>(catalog.Read<NewCharacterData[]>("characters").Select(c=>c.Id));
        var cutIds = new HashSet<string>(cuts.Select(c=>c.Id));
        var cocktails = new HashSet<string>(catalog.Read<NewCocktailData[]>("cocktails").Select(c=>c.Id));
        var streets = catalog.Read<NewStreetData>("script/street").Scenes ?? Array.Empty<NewSceneData>();
        var streetIds = new HashSet<string>(streets.Select(s=>s.Id));
        var spots = new HashSet<string>(catalog.Read<NewSpotData[]>("spots").Select(s=>s.Id));
        foreach (var source in catalog.SourceIds.Where(IsCSource)) Walk(catalog.ReadUntyped(source), source, Add);
        foreach (var duplicate in streets.GroupBy(s=>s.Id).Where(g=>g.Count()>1)) Add("ERROR","script/street", "Duplicate scene id: " + duplicate.Key);
        foreach (var scene in streets) ValidateStreetSteps(scene.Steps, "script/street/" + scene.Id, streetIds, Add);
        foreach (var source in catalog.SourceIds.Where(s=>s.StartsWith("script/bar/",StringComparison.Ordinal)))
        {
            var script = catalog.Read<NewDayScriptBase>(source);
            ValidateBar(script, source, characters, cutIds, cocktails, Add);
        }
        foreach (var source in new[] {"interact_points_outside", "interact_points_home"})
            foreach (var point in catalog.Read<NewInteractPointData[]>(source))
            {
                var location=source+"/"+point.Id;
                if (!spots.Contains(point.SpotId)) Add("ERROR",location,"Unknown spot: "+point.SpotId);
                if (point.ActionType == EActionType.Transition && !OutsideActions.TryGetScene(point.ActionRef, out _))
                    Add("ERROR",location,"Unsupported transition action_ref: "+point.ActionRef);
                if (point.ActionType == EActionType.Scene && !cutIds.Contains(point.ActionRef ?? ""))
                    Add("ERROR",location,"Unknown cutscene action_ref: "+point.ActionRef);
                if (point.ActionType == EActionType.System && point.ActionRef != OutsideActions.ElevatorToggle)
                    Add("WARN",location,"System action needs an implementation/owner contract: "+point.ActionRef);
                foreach (var flow in point.DialogueFlows ?? Array.Empty<NewInteractDialogueFlowData>())
                    if (!streetIds.Contains(flow.SceneId)) Add(source.EndsWith("home")?"WARN":"ERROR",location,"Dialogue scene is not in the registered street catalog: "+flow.SceneId);
            }
        var addresses = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void IndexKey(string key, string path)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!addresses.TryGetValue(key,out var paths)) addresses[key]=paths=new List<string>();
            if (!paths.Contains(path)) paths.Add(path);
        }
        var settings=AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) Add("ERROR","Addressables","Addressables settings are missing.");
        else
        {
            var entries=new List<UnityEditor.AddressableAssets.Settings.AddressableAssetEntry>();
            settings.GetAllAssets(entries,true);
            foreach (var entry in entries)
            {
                IndexKey(entry.address,entry.AssetPath);
                IndexKey(entry.guid,entry.AssetPath);
                foreach (var label in entry.labels) IndexKey(label,entry.AssetPath);
            }
            foreach (var duplicate in entries.GroupBy(e=>e.address).Where(g=>g.Select(e=>e.AssetPath).Distinct().Count()>1))
                Add("ERROR","Addressables", "Duplicate address: "+duplicate.Key);
        }
        var sceneTimelines = ReadSceneTimelines(Add);
        var localOnly = new HashSet<string>();
        foreach (var cut in cuts)
        {
            var location="cutscenes/"+cut.Id;
            if (cut.Kind != ENewCutSceneKind.Timeline && cut.Kind != ENewCutSceneKind.Sprite)
                Add("ERROR",location,"Unsupported cutscene kind: "+cut.Kind);
            if (!addresses.TryGetValue(cut.ResourceKey ?? "",out var paths))
            {
                if (cut.Kind == ENewCutSceneKind.Timeline && sceneTimelines.TryGetValue(cut.ResourceKey ?? "", out var scenes))
                {
                    localOnly.Add(cut.Id);
                    Add("WARN",location,"Scene-bound Timeline (requires that scene's director): "+string.Join(", ",scenes));
                }
                else Add("ERROR",location,"Missing Addressables resource_key: "+cut.ResourceKey);
            }
            else
            {
                var expected=cut.Kind==ENewCutSceneKind.Timeline?typeof(TimelineAsset):typeof(AnimationClip);
                int matches=paths.Sum(path=>AssetDatabase.LoadAllAssetsAtPath(path).Count(asset=>asset!=null && expected.IsInstanceOfType(asset)));
                if (matches==0) Add("ERROR",location,"Resource type must be "+expected.Name+": "+string.Join(", ",paths));
                else if (matches>1) Add("ERROR",location,"Resource key resolves to multiple "+expected.Name+" assets: "+cut.ResourceKey);
            }
        }
        foreach (var source in catalog.SourceIds.Where(s=>s.StartsWith("script/bar/",StringComparison.Ordinal)))
            foreach (var scene in catalog.Read<NewDayScriptBase>(source).Scenes ?? Array.Empty<NewScriptSceneData>())
                foreach (var step in scene.Steps ?? Array.Empty<NewDialogueStepData>())
                    if (step.Type == ENewStepType.Timeline && localOnly.Contains(step.Arg ?? ""))
                        Add("ERROR",source+"/"+scene.Id+"#"+step.Seq,"Outside scene binding is unavailable in Play: "+step.Arg);
        var expressions=catalog.Read<Dictionary<string,Dictionary<string,NewExpressionEntry>>>("character_anim");
        foreach (var actor in expressions)
            foreach (var expression in actor.Value)
            {
                var entry=expression.Value;var location="character_anim/"+actor.Key+"/"+expression.Key;
                if (entry.Mode==ENewExpressionMode.Sprite)
                {
                    if (string.IsNullOrEmpty(entry.Sprite) || !addresses.ContainsKey(entry.Sprite)) Add("ERROR",location,"Portrait sprite address is missing: "+entry.Sprite);
                }
                else if (entry.Parts!=null)
                    foreach(var part in entry.Parts)
                        if (!string.IsNullOrEmpty(part.Value.Clip) &&
                            !addresses.ContainsKey(CharacterLoader.LoopResourceKey(part.Value.Clip)) && !addresses.ContainsKey(part.Value.Clip))
                            Add("WARN",location+"/"+part.Key,"No animation/sprite at configured key; runtime default fallback may apply: "+part.Value.Clip);
            }
        return issues;
    }

    // 저장된 C 씬의 바인딩만 읽으며 씬을 열거나 저장하지 않는다.
    static Dictionary<string, HashSet<string>> ReadSceneTimelines(Action<string,string,string> add)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var scene in new[] { "Assets/00.Scenes/OutSide.unity", "Assets/00.Scenes/Home.unity" })
        {
            if (!File.Exists(scene)) continue;
            foreach (Match list in Regex.Matches(File.ReadAllText(scene), @"  sceneTimelines:\r?\n((?:  -[^\r\n]*\r?\n)*)"))
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (Match reference in Regex.Matches(list.Groups[1].Value, @"guid: ([0-9a-f]{32})"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(reference.Groups[1].Value);
                    var asset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
                    if (asset == null) { add("ERROR",scene,"Missing bound Timeline: "+reference.Groups[1].Value); continue; }
                    if (!names.Add(asset.name)) add("ERROR",scene,"Duplicate scene Timeline name: "+asset.name);
                    if (!result.TryGetValue(asset.name, out var scenes)) result[asset.name] = scenes = new HashSet<string>();
                    scenes.Add(scene);
                }
            }
        }
        return result;
    }

    static bool IsCSource(string source) => source.StartsWith("script/",StringComparison.Ordinal) ||
        source.StartsWith("interact_points_",StringComparison.Ordinal) || source is "cutscenes" or "character_anim" or "characters";

    static void Walk(object value, string path, Action<string,string,string> add)
    {
        if (value is IDictionary<string,object> fields)
            foreach(var field in fields)
            {
                if (field.Value is string text)
                {
                    string error = field.Key is "when" or "spawn_when" or "interact_when" ? ConditionUtil.ValidateConditionSyntax(text)
                        : field.Key=="effects" ? ConditionUtil.ValidateEffectSyntax(text) : null;
                    if (error != null) add("ERROR",path+"/"+field.Key,error);
                }
                Walk(field.Value,path+"/"+field.Key,add);
            }
        else if (value is IList list)
            for (int i=0;i<list.Count;i++) Walk(list[i],path+"["+i+"]",add);
    }

    static void ValidateStreetSteps(Step[] steps, string location, HashSet<string> scenes, Action<string,string,string> add)
    {
        if (steps==null || steps.Length==0) { add("ERROR",location,"No dialogue steps.");return; }
        foreach(var duplicate in steps.Where(s=>s.Seq != 0).GroupBy(s=>s.Seq).Where(g=>g.Count()>1)) add("ERROR",location,"Duplicate step seq: "+duplicate.Key);
        foreach(var step in steps)
        {
            var at=location+"#"+step.Seq;
            switch(step.Type?.ToLowerInvariant())
            {
                case "say": case "timeline":
                    if (string.IsNullOrWhiteSpace(step.Text?.Ko)) add("ERROR",at,"Dialogue text is empty (street timeline currently uses the dialogue presenter).");
                    break;
                case "set_state": case "effect": break;
                case "goto": if (!scenes.Contains(step.SceneId ?? "")) add("ERROR",at,"Missing goto scene: "+step.SceneId);break;
                case "choice":
                    if (step.Options==null || step.Options.Length==0) add("ERROR",at,"Choice has no options.");
                    foreach(var option in step.Options ?? Array.Empty<NewStreetOptionData>())
                    {
                        if (string.IsNullOrWhiteSpace(option.Text?.Ko)) add("ERROR",at,"Choice text is empty: "+option.Id);
                        if (option.ResultSteps?.Length>0) ValidateStreetSteps(option.ResultSteps,at+"/"+option.Id,scenes,add);
                    }
                    break;
                default: add("ERROR",at,"Unsupported street step: "+step.Type);break;
            }
        }
    }

    static void ValidateBar(NewDayScriptBase script, string source, HashSet<string> actors, HashSet<string> cutIds,
        HashSet<string> cocktails, Action<string,string,string> add)
    {
        var scenes=script.Scenes ?? Array.Empty<NewScriptSceneData>();
        var sceneIds=new HashSet<string>(scenes.Select(s=>s.Id));
        foreach(var duplicate in scenes.GroupBy(s=>s.Id).Where(g=>g.Count()>1)) add("ERROR",source,"Duplicate scene id: "+duplicate.Key);
        foreach(var scene in scenes)
        {
            var location=source+"/"+scene.Id;
            foreach(var duplicate in (scene.Steps ?? Array.Empty<NewDialogueStepData>()).GroupBy(s=>s.Seq).Where(g=>g.Count()>1)) add("ERROR",location,"Duplicate step seq: "+duplicate.Key);
            foreach(var step in scene.Steps ?? Array.Empty<NewDialogueStepData>())
            {
                var at=location+"#"+step.Seq;
                if (!string.IsNullOrEmpty(step.Actor) && !actors.Contains(step.Actor)) add("ERROR",at,"Unknown actor: "+step.Actor);
                switch(step.Type)
                {
                    case ENewStepType.Say:
                        if (string.IsNullOrWhiteSpace(step.Text?.Ko)) add("ERROR",at,"Say text is empty."); break;
                    case ENewStepType.Choice:
                        if (script.Choices==null || !script.Choices.ContainsKey(step.Arg ?? "")) add("ERROR",at,"Missing choice group: "+step.Arg);break;
                    case ENewStepType.Timeline:
                        if (!cutIds.Contains(step.Arg ?? "")) add("ERROR",at,"Unknown cutscene id: "+step.Arg);break;
                    case ENewStepType.Order:
                        if (step.Arg==null || !step.Arg.StartsWith("exact:",StringComparison.Ordinal) || !cocktails.Contains(step.Arg.Substring(6).Trim())) add("ERROR",at,"Invalid exact cocktail order: "+step.Arg);break;
                    case ENewStepType.Craft:
                        if (step.Arg!="order" && (step.Arg==null || !step.Arg.StartsWith("tutorial:",StringComparison.Ordinal) || !cocktails.Contains(step.Arg.Substring(9).Trim()))) add("ERROR",at,"Invalid craft argument: "+step.Arg);break;
                    case ENewStepType.Enter: case ENewStepType.Exit: case ENewStepType.Effect: case ENewStepType.Serve: case ENewStepType.EndPart: break;
                    default: add("ERROR",at,"Step has no StoryScriptRunner implementation: "+step.Type);break;
                }
            }
        }
        foreach(var group in script.Choices ?? new Dictionary<string,NewChoiceOptionData[]>())
        {
            if (group.Value==null || group.Value.Length==0) { add("ERROR",source+"/choices/"+group.Key,"Choice group is empty.");continue; }
            foreach(var option in group.Value)
                if (!string.IsNullOrEmpty(option.Goto) && !sceneIds.Contains(option.Goto)) add("ERROR",source+"/choices/"+group.Key,"Unknown goto scene: "+option.Goto);
        }
    }
}
