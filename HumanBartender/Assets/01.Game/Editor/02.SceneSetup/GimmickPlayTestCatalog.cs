using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>CSV의 실제 칵테일 제조 큐와 조작 가능한 개별 단계를 준비한다.</summary>
internal static class GimmickPlayTestCatalog
{
    internal const string PlayPath = "Assets/01.Game/01.Scenes/Play.unity";
    internal const string QuickTestKey = "@quick-four";
    internal static readonly IReadOnlyList<ECraftGimmick> QuickTypes = Array.AsReadOnly(new[]
        { ECraftGimmick.Open, ECraftGimmick.Pour, ECraftGimmick.Stir, ECraftGimmick.Shake });

    internal sealed class Recipe
    {
        internal NewCocktailData Cocktail;
        internal GimmickStep[] Steps;
        internal string[] AutoSqueezeIds;
        internal string[] AutoPowderIds;
        internal string Key => Cocktail.Id;
        internal string ToolId => Steps.Any(value => value.Type == ECraftGimmick.Open)
            ? NewToolIds.BottleOpener : Cocktail.TargetToolId;
        internal string Label => (string.IsNullOrEmpty(Cocktail.Name.Ko) ? Cocktail.Id : Cocktail.Name.Ko) +
            " [" + Cocktail.Id + "]";

        internal CraftSession CreateSession()
        {
            var session = new CraftSession(Cocktail.Id);
            CraftPreset.ApplyTargetSetup(Cocktail, session.Actual);
            session.Actual.SetTool(ToolId);
            session.Actual.SetAutoCalledIngredients(AutoSqueezeIds, AutoPowderIds);
            return session;
        }
    }

    internal sealed class Case
    {
        internal Recipe Recipe;
        internal int Index;
        internal NewCocktailData Cocktail => Recipe.Cocktail;
        internal GimmickStep Step => Recipe.Steps[Index];
        internal string Key => Cocktail.Id + ":" + Index;
        internal string Label => Recipe.Label + " / " + Step;
        internal CraftSession CreateSession() => Recipe.CreateSession();
    }

    internal static Case[] Load() => Cases(LoadRecipes());

    internal static Recipe[] LoadRecipes()
    {
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
        var cocktails = catalog.Read<NewCocktailData[]>("cocktails");
        var shelfItems = catalog.Read<NewShelfItemData[]>("shelf_items");
        if (cocktails == null || shelfItems == null)
            throw new InvalidOperationException("[GimmickPlayTest] cocktails / shelf_items CSV를 읽지 못했습니다.");
        var shelf = ScriptableObject.CreateInstance<NewShelfItemDataSO>();
        shelf.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            shelf.shelfItemData = shelfItems;
            return BuildRecipes(cocktails, shelf);
        }
        finally { UnityEngine.Object.DestroyImmediate(shelf); }
    }

    internal static Case[] Build(IEnumerable<NewCocktailData> cocktails, NewShelfItemDataSO shelf)
        => Cases(BuildRecipes(cocktails, shelf));

    internal static Case[] Cases(IEnumerable<Recipe> recipes) => recipes.SelectMany(recipe => recipe.Steps
        .Select((step, index) => new Case { Recipe = recipe, Index = index })
        .Where(value => !value.Step.IsImmediateAddition)).ToArray();

    internal static Recipe[] BuildRecipes(IEnumerable<NewCocktailData> cocktails, NewShelfItemDataSO shelf)
    {
        var result = new List<Recipe>();
        foreach (var cocktail in cocktails.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            var actual = new ActualCraft();
            CraftPreset.ApplyTargetSetup(cocktail, actual);
            if (shelf != null && actual.IngredientIds.Any(shelf.RequiresOpen)) actual.SetBottleOpener(true);
            if (!actual.CanStartGimmicks) continue;
            var queue = GimmickQueueBuilder.Build(cocktail, actual, shelf);
            if (queue.UnresolvedIngredientIds.Count > 0)
                throw new InvalidOperationException("[GimmickPlayTest] 제조 동작이 없는 재료: " + cocktail.Id + " / " +
                    string.Join(", ", queue.UnresolvedIngredientIds));
            if (queue.Count > 0)
            {
                result.Add(new Recipe
                {
                    Cocktail = cocktail, Steps = queue.Steps.ToArray(),
                    AutoSqueezeIds = actual.AutoSqueezeIds.ToArray(),
                    AutoPowderIds = actual.AutoPowderIds.ToArray()
                });
            }
        }
        return result.ToArray();
    }

    internal static Case Find(string key) => Load().FirstOrDefault(value => value.Key == key)
        ?? throw new InvalidOperationException("[GimmickPlayTest] 선택한 CSV 기믹을 찾을 수 없습니다: " + key);

    internal static Recipe FindRecipe(string key) => LoadRecipes().FirstOrDefault(value => value.Key == key)
        ?? throw new InvalidOperationException("[GimmickPlayTest] 선택한 CSV 칵테일을 찾을 수 없습니다: " + key);

    internal static Case[] SelectQuickCases(IEnumerable<Case> source)
    {
        var cases = source.ToArray();
        return QuickTypes.Select(type => cases.Where(value => value.Step.Type == type &&
                !value.Step.IsImmediateAddition && (type != ECraftGimmick.Pour || value.Step.HasTarget && value.Step.TargetUnit.HasValue))
            .OrderBy(value => value.Cocktail.Status != ENewDataStatus.Confirmed)
            .ThenBy(value => value.Step.TargetValue ?? float.MaxValue)
            .ThenBy(value => value.Cocktail.Id, StringComparer.Ordinal).ThenBy(value => value.Index).FirstOrDefault()
            ?? throw new InvalidOperationException("[GimmickPlayTest] 간단 테스트용 CSV 예시가 없습니다: " + Name(type))).ToArray();
    }

    internal static void ValidateQuickTest() => ValidatePrefabs(SelectQuickCases(Load()).Select(value => value.Step.Type));

    internal static void ValidatePrefab(ECraftGimmick type) => ValidatePrefabs(new[] { type });

    internal static void ValidateRecipe(Recipe recipe) => ValidatePrefabs(recipe.Steps
        .Where(value => !value.IsImmediateAddition).Select(value => value.Type).Distinct());

    private static void ValidatePrefabs(IEnumerable<ECraftGimmick> types)
    {
        var preview = EditorSceneManager.OpenPreviewScene(PlayPath);
        try
        {
            var runner = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GimmickRunner>(true)).Single();
            using var data = new SerializedObject(runner);
            var prefabs = data.FindProperty("prefabs");
            GameObject Find(ECraftGimmick requested)
            {
                for (int i = 0; i < prefabs.arraySize; i++)
                {
                    var entry = prefabs.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("type").intValue == (int)requested &&
                        entry.FindPropertyRelative("prefab").objectReferenceValue is GameObject prefab) return prefab;
                }
                return null;
            }
            foreach (var type in types)
            {
                var selected = Find(type) ?? (type == ECraftGimmick.FillUp ? Find(ECraftGimmick.Pour) : null);
                if (selected == null || selected.GetComponentInChildren<ICraftGimmick>(true) == null)
                    throw new InvalidOperationException("[GimmickPlayTest] Play 씬에 실행 가능한 기믹 프리팹이 없습니다: " + type);
            }
            foreach (string field in new[] { "gimmickRoot", "hud", "gimmickCamera", "inputRouter" })
                if (data.FindProperty(field).objectReferenceValue == null)
                    throw new InvalidOperationException("[GimmickPlayTest] Play 씬 GimmickRunner 연결을 확인하세요: " + field);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    internal static string Name(ECraftGimmick type) => type switch
    {
        ECraftGimmick.Open => "병따기", ECraftGimmick.Pour => "따르기", ECraftGimmick.Shake => "셰이크",
        ECraftGimmick.Stir => "스터", ECraftGimmick.FillUp => "필업", ECraftGimmick.Squeeze => "스퀴즈",
        ECraftGimmick.Powder => "파우더", _ => type.ToString()
    };
}
