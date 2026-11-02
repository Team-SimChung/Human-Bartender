using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CsvContentValidator
{
    [MenuItem("Tools/Data/Validate CSV Content")]
    public static void Validate()
    {
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
        ValidateCatalog(catalog);
    }

    public static void ValidateCatalog(CsvContentCatalog catalog)
    {
        CsvGameContentRules.Validate(catalog);
        catalog.Read<NewDayScriptBase>("script/common");
        catalog.Read<NewBalanceDataBase>("balance");
        catalog.Read<NewBarkData[]>("barks");
        catalog.Read<NewCharacterData[]>("characters");
        catalog.Read<NewCocktailData[]>("cocktails");
        catalog.Read<NewCutSceneRefData[]>("cutscenes");
        catalog.Read<NewDayInfoData[]>("days");
        catalog.Read<NewDossierData[]>("dossier");
        catalog.Read<NewEndingData[]>("endings");
        catalog.Read<Dictionary<string, Dictionary<string, NewExpressionEntry>>>("character_anim");
        catalog.Read<NewFieldAnimData[]>("field_anims");
        catalog.Read<NewGuestBodyDataBase>("guest_bodies");
        catalog.Read<NewInteractPointData[]>("interact_points_outside");
        catalog.Read<NewInteractPointData[]>("interact_points_home");
        catalog.Read<NewOrderRuleData[]>("order_rules");
        catalog.Read<NewPersonalityData[]>("personalities");
        catalog.Read<NewQuestDataBase>("quests");
        catalog.Read<NewRandomWaveData[]>("random_waves");
        catalog.Read<NewRegularSlotData[]>("regular_slots");
        catalog.Read<NewShelfItemData[]>("shelf_items");
        catalog.Read<NewSpotData[]>("spots");
        catalog.Read<NewTasteData[]>("tastes");
        catalog.Read<Dictionary<string, NewTextTagData>>("text_tags");
        catalog.Read<Dictionary<string, LocalizedText>>("ui_strings");
        catalog.Read<NewStreetData>("script/street");
        catalog.Read<NewStreetData>("script/home");
        catalog.Read<NewDayScriptBase>("script/cutscene");
        catalog.Read<NewDayScriptBase>("script/bar/day0");
        catalog.Read<NewDayScriptBase>("script/bar/day1");
        catalog.Read<NewDayScriptBase>("script/bar/day2");
        catalog.Read<NewDayScriptBase>("script/bar/day3");
        catalog.Read<NewDayScriptBase>("script/bar/day99");
        Debug.Log($"[CSV] {catalog.SourceIds.Count()}개 데이터 묶음 구조와 로더 타입 32개를 검증했습니다.");
    }

    // Batchmode uses the same parser and DTOs as the game, with a staged directory.
    public static void ValidateBatch()
    {
        try
        {
            var args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-csvContentPath");
            string path = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder);
            ValidateCatalog(CsvDataReader.LoadDirectory(path));
            int reportAt = System.Array.IndexOf(args, "-csvValidationReport");
            if (reportAt >= 0 && reportAt + 1 < args.Length)
            {
                var files = new Newtonsoft.Json.Linq.JObject();
                var paths = CsvContentCatalog.ParseRows(File.ReadAllText(Path.Combine(path, "system/table_index.csv")), "table_index.csv").Select(row => row["path"])
                    .Concat(new[] { "system/table_index.csv", "system/columns.csv", "system/data_sets.csv", "system/row_formats.csv" }).Distinct();
                foreach (string file in paths)
                {
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    files[file] = System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(path, file)))).Replace("-", "").ToLowerInvariant();
                }
                var receipt = new Newtonsoft.Json.Linq.JObject { ["success"] = true, ["files"] = files };
                File.WriteAllText(args[reportAt + 1], receipt.ToString(), new System.Text.UTF8Encoding(false));
            }
            Debug.Log("CSV_BATCH_VALIDATION_OK");
            EditorApplication.Exit(0);
        }
        catch (System.Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
