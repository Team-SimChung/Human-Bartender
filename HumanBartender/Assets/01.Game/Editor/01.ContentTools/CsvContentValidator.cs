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
        catalog.Read<NewDayScriptBase>("script/bar/day0");
        catalog.Read<NewDayScriptBase>("script/bar/day1");
        catalog.Read<NewDayScriptBase>("script/bar/day2");
        catalog.Read<NewDayScriptBase>("script/bar/day3");
        catalog.Read<NewDayScriptBase>("script/bar/day99");
        Debug.Log($"[CSV] {catalog.SourceIds.Count()}개 데이터 묶음 구조와 로더 타입 30개를 검증했습니다.");
    }
}
