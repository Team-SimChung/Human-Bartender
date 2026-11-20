using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class WebCsvCompatibilityTests
{
    /// <summary>
    /// 실제 게시 해시를 검사한다. 부분 반입·Unity 효과 보존본을 웹 원본과 동일하다고 취급하지 않는다.
    /// </summary>
    [Test]
    public void PublishedFilesMatchRecordedSnapshotsAndProtectedOverrides()
    {
        string project = Path.GetDirectoryName(Application.dataPath);
        var manifest = JObject.Parse(File.ReadAllText(Path.Combine(project, "ContentAuthoring/web_content_source.json")));
        Assert.That((string)manifest["direction"], Is.EqualTo("web-to-unity"));
        if ((string)manifest["ids"] == "readable")
        {
            var web = ((JObject)manifest["web_files"]).Properties().Select(file => file.Name);
            Assert.That(web, Is.EquivalentTo(((JObject)manifest["files"]).Properties().Select(file => file.Name)));
            Assert.That(File.Exists(Path.Combine(project, "ContentAuthoring/web_id_map.json")), Is.True);
        }
        foreach (var file in ((JObject)manifest["files"]).Properties())
        {
            using var sha = SHA256.Create();
            string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, "csv", file.Name)))).Replace("-", "").ToLowerInvariant();
            Assert.That(hash, Is.EqualTo((string)file.Value), file.Name);
        }
        foreach (var file in ((JObject)manifest["unity_overrides"] ?? new JObject()).Properties())
            Assert.That((string)manifest["files"][file.Name], Is.EqualTo((string)file.Value), file.Name);
    }

    [Test]
    public void PartialDialogueImportRecordsItsReviewAndKeepsFullImportProtected()
    {
        string authoring = Path.Combine(Path.GetDirectoryName(Application.dataPath), "ContentAuthoring");
        var manifest = JObject.Parse(File.ReadAllText(Path.Combine(authoring, "web_content_source.json")));
        var entry = ((JArray)manifest["partial_imports"]).Single(row =>
            (string)row["scope"] == "existing_production_dialogue");
        Assert.That((string)entry["commit"], Is.EqualTo("d9a34746761bc3a0f3a67bcd04cdf737d9884d70"));
        Assert.That(entry["files"].Values<string>(), Is.EquivalentTo(new[]
            { "story/steps.csv", "story/bar_choices.csv", "story/street_choices.csv" }));
        using var sha = SHA256.Create();
        string reviewHash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(
            Path.Combine(authoring, (string)entry["review"])))).Replace("-", "").ToLowerInvariant();
        Assert.That(reviewHash, Is.EqualTo((string)entry["review_sha256"]));
        Assert.That((string)manifest["unity_overrides"]["story/steps.csv"],
            Is.EqualTo((string)manifest["files"]["story/steps.csv"]));
        Assert.That((string)manifest["web_files"]["story/steps.csv"],
            Is.Not.EqualTo((string)manifest["files"]["story/steps.csv"]));
    }

    [Test]
    public void RevisedCommuteKeepsSamhosEffectOnHisBoastAndDepartsBeforeLunasReply()
    {
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        var street = StoryCsvCompiler.Compile(catalog.Read<NewStreetData>("script/street"));
        var steps = street.Scenes.Single(scene => scene.Id == "commute2").Steps;
        Assert.That(steps.Select(step => step.Seq), Is.EqualTo(Enumerable.Range(1, 26)));
        Assert.That(steps[21].Actor, Is.EqualTo("samho"));
        Assert.That(steps[21].Text.Ko, Does.Contain("<pop peak=1.15>애니멀 소속이 될 몸이지.</pop>"));
        Assert.That(steps[21].Text.En, Does.Contain("<pop peak=1.15>"));
        Assert.That(steps[24].Command, Is.EqualTo(OutsideStoryCommand.SamhoDepart));
        Assert.That(steps[25].Actor, Is.EqualTo("luna"));
        Assert.That(steps[25].Text.Ko, Is.EqualTo("... 서비스?"));
        var trade = street.Scenes.Single(scene => scene.Id == "outside_day1_samho_smuggler").Steps;
        Assert.That(trade[2].Text.Ko, Does.Contain("<fx=emphasis>대금을 치를 방법이 없는</fx>"));
        Assert.That(trade[3].Text.Ko, Does.Not.Contain("<fx"));
    }

    [Test]
    public void AllRuntimeDtosMapAndWebBehaviorMetadataIsRetained()
    {
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        CsvContentValidator.ValidateCatalog(catalog);
        var steps = Enumerable.Range(0, 4).SelectMany(day => StoryCsvCompiler.Compile(catalog.Read<NewDayScriptBase>("script/bar/day" + day)).Scenes).SelectMany(scene => scene.Steps).ToArray();
        Assert.That(steps.Count(step => step.Type == ENewStepType.EmptyGlass), Is.EqualTo(9));
        Assert.That(steps.Count(step => step.Type == ENewStepType.Coaster), Is.EqualTo(11));
        Assert.That(steps.Any(step => step.Type == ENewStepType.Order && !step.IsPaid), Is.True);
        Assert.That(steps.Any(step => step.Entrance == "seated"), Is.True);
        var home = StoryCsvCompiler.Compile(catalog.Read<NewStreetData>("script/home"));
        // 웹 24022cd에서 night2의 선택지(바 공용 선택지 ch_night2_1 참조)가 빠져, 집 대본이 바 공용 선택지를
        // 참조하는 데이터는 더 이상 없다. 원시 웹 메타데이터가 실행 DTO에서 비워지는지만 본다.
        Assert.That(home.Scenes.SelectMany(scene => scene.Steps).All(step => step.WebFields == null), Is.True);
    }

    [Test]
    public void DisplayingAnEmptyGlassDoesNotChangeTheCraftedDrinkOrOtherVisit()
    {
        var state = new StoryTableState();
        state.Enter("tom"); state.Enter("chris"); state.PlaceCoaster("tom"); state.PlaceCoaster("chris");
        var session = new CraftSession("gin_tonic");
        var drink = new CraftedDrink(session, null, new NewCocktailData { Id = "gin_tonic", Name = new LocalizedText { Ko = "진토닉" }, Color = "#ffffff" });
        state.Serve("tom", drink); state.Serve("chris", drink);
        state.EmptyGlass("tom"); state.EmptyGlass("tom");
        Assert.That(state.Get("tom").GlassEmpty, Is.True);
        Assert.That(state.Get("chris").GlassEmpty, Is.False);
        Assert.That(state.Get("tom").Drink, Is.SameAs(drink));
        Assert.That(drink.Session, Is.SameAs(session));
        state.Serve("tom", drink);
        Assert.That(state.Get("tom").GlassEmpty, Is.False);
    }

    [Test]
    public void PaymentProjectionDoesNotTurnAFreeOrderIntoAPaidOrder()
    {
        var step = new NewDialogueStepData { WebFields = new Dictionary<string, object> { ["payment"] = "none" } };
        Assert.That(StoryCsvCompiler.Compile(step).IsPaid, Is.False);
        step.Payment = "paid";
        Assert.That(step.IsPaid, Is.True);
    }
}
