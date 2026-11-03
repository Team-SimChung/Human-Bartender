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
    /// Unity CSV는 반입 도구가 게시한 그대로여야 한다. 읽기 쉬운 ID로 게시했다면 같은 커밋의 웹 원본 해시도 함께 기록된다.
    /// </summary>
    [Test]
    public void PublishedFilesAreExactlyThePinnedWebSnapshot()
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
