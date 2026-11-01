using System;
using System.IO;
using System.Linq;
using UnityEngine;

internal sealed class CsvStoryTestContent : IStoryContentSource
{
    readonly NewStreetData street, home;
    readonly NewDayScriptBase memories;
    readonly NewDayScriptBase[] bars;
    internal CsvStoryTestContent()
    {
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        street = StoryCsvCompiler.Compile(catalog.Read<NewStreetData>("script/street"));
        home = StoryCsvCompiler.Compile(catalog.Read<NewStreetData>("script/home"));
        memories = StoryCsvCompiler.Compile(catalog.Read<NewDayScriptBase>("script/cutscene"));
        bars = Enumerable.Range(0, 4).Select(day => StoryCsvCompiler.Compile(catalog.Read<NewDayScriptBase>("script/bar/day" + day))).ToArray();
    }
    public Step[] GetSceneSteps(string id) => street.Scenes.Concat(home.Scenes).Single(scene => scene.Id == id).Steps;
    public NewChoiceOptionData[] GetSharedChoices(string id) => bars.Select(bar => bar.Choices)
        .Where(choices => choices != null && choices.ContainsKey(id)).Single()[id];
    public NewDialogueStepData[] GetCutsceneSteps(string id) => memories.Scenes.Single(scene => scene.Id == id).Steps;
}
