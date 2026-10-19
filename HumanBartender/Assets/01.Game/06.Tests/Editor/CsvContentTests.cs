using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

public class CsvContentTests
{
    public class Sample
    {
        [JsonProperty("amount")] public int Amount;
        [JsonProperty("enabled")] public bool Enabled;
        [JsonProperty("text")] public string Text;
        [JsonProperty("empty")] public string Empty;
        [JsonProperty("missing")] public int? Missing;
    }

    static string Csv(params string[][] rows) => string.Join("\r\n", rows.Select(r => string.Join(",", r.Select(v => "\"" + v.Replace("\"", "\"\"") + "\"")))) + "\r\n";

    static Dictionary<string, string> Fixture()
    {
        string[] columns = { "amount", "enabled", "text", "empty", "missing", "dataset_id", "row_id", "parent_id", "parent_field", "source_order", "template_id" };
        var columnRows = new List<string[]> { new[] { "table", "column", "column_order", "types" } };
        for (int i = 0; i < columns.Length; i++) columnRows.Add(new[] { "TestRows", columns[i], i.ToString(), "str" });
        var schemaRows = new List<string[]> {
            new[] { "template_id", "node_id", "parent_node", "field", "field_order", "kind", "column", "type", "table", "slot", "container", "key_column", "parent", "row_id", "map_key", "owner" },
            new[] { "root", "0", "", "", "0", "record", "", "", "TestRows", "", "", "", "", "r1", "", "" },
            new[] { "sample", "0", "", "", "0", "object", "", "", "", "", "", "", "", "", "", "TestRows" },
        };
        string[] kinds = { "int", "bool", "str", "str", "int" };
        for (int i = 0; i < 5; i++) schemaRows.Add(new[] { "sample", "0." + i, "0", columns[i], i.ToString(), "cell", columns[i], kinds[i], "", "", "", "", "", "", "", "" });
        return new Dictionary<string, string> {
            ["_Tables.csv"] = Csv(new[] { "table" }, new[] { "TestRows" }),
            ["_Columns.csv"] = Csv(columnRows.ToArray()),
            ["_Sources.csv"] = Csv(new[] { "dataset_id", "template_id" }, new[] { "test", "root" }),
            ["_Schemas.csv"] = Csv(schemaRows.ToArray()),
            ["TestRows.csv"] = Csv(columns, new[] { "0", "false", "한글, \"대사\"\r\n둘째 줄", @"\E", @"\N", "test", "r1", "@test", "", "0", "sample" }),
        };
    }

    [Test]
    public void DirectCsvPreservesZeroFalseNullEmptyAndMultilineText()
    {
        var sample = CsvContentCatalog.Parse(Fixture()).Read<Sample>("test");
        Assert.AreEqual(0, sample.Amount);
        Assert.IsFalse(sample.Enabled);
        Assert.AreEqual("", sample.Empty);
        Assert.IsNull(sample.Missing);
        Assert.AreEqual("한글, \"대사\"\r\n둘째 줄", sample.Text);
    }

    [TestCase("a\n\"unfinished")]
    [TestCase("a\na\"b")]
    [TestCase("a\n\"a\"x")]
    public void RejectMalformedQuotes(string text) => Assert.Throws<InvalidOperationException>(() => CsvContentCatalog.ParseCsv(text));

    [Test]
    public void RejectDuplicateRowIdsBeforePublishing()
    {
        var files = Fixture();
        var data = CsvContentCatalog.ParseCsv(files["TestRows.csv"]);
        data.Add((string[])data[1].Clone());
        files["TestRows.csv"] = Csv(data.ToArray());
        Assert.Throws<InvalidOperationException>(() => CsvContentCatalog.Parse(files));
    }

    [Test]
    public void ParseBomTrailingEmptyCellAndEmbeddedQuotes()
    {
        var rows = CsvContentCatalog.ParseCsv("\uFEFFid,text,last\r\none,\"say \"\"hi\"\"\",\r\n");
        Assert.AreEqual("id", rows[0][0]);
        Assert.AreEqual("say \"hi\"", rows[1][1]);
        Assert.AreEqual("", rows[1][2]);
    }

    [Test]
    public void ProjectCatalogLoadsNestedChoicesRecipesAndEmptyDays()
    {
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
        Assert.IsNotEmpty(catalog.Read<NewCocktailData[]>("cocktails").First().Recipe);
        Assert.IsEmpty(catalog.Read<NewDayScriptBase>("script/bar/day2").Scenes);
        Assert.IsEmpty(catalog.Read<NewQuestDataBase>("quests").Quests);
        var street = catalog.Read<NewStreetData>("script/street");
        Assert.IsTrue(street.Scenes.SelectMany(s => s.Steps).Any(s => s.Options != null && s.Options.Any(o => o.ResultSteps != null)));
    }
}
