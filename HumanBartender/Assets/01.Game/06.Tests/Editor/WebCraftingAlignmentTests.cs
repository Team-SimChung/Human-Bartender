using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class WebCraftingAlignmentTests
{
    static CsvContentCatalog Csv() => CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
    static CraftPrepShelfSlot[] Slots(CraftPrepStageScreen screen) =>
        (CraftPrepShelfSlot[])typeof(CraftPrepStageScreen).GetField("slots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(screen);

    [Test]
    public void EveryCurrentManualRecipeInputHasAConnectedShelfSlot()
    {
        var data = Csv();
        var items = data.Read<NewShelfItemData[]>("shelf_items").ToDictionary(i => i.Id);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WebCraftingShelfSetup.PrefabPath);
        var slots = Slots(prefab.GetComponent<CraftPrepStageScreen>());
        Assert.That(slots.All(s => s != null), Is.True);
        Assert.That(slots.Select(s => s.ItemId).Distinct().Count(), Is.EqualTo(slots.Length));
        var ids = slots.Select(s => s.ItemId).ToArray();
        foreach (var cocktail in data.Read<NewCocktailData[]>("cocktails").Where(c => c.Status == ENewDataStatus.Confirmed && c.UnlockDay <= 3))
        {
            Assert.That(ids, Does.Contain(cocktail.Glass), cocktail.Id + " glass");
            if (!string.IsNullOrEmpty(cocktail.TargetToolId)) Assert.That(ids, Does.Contain(cocktail.TargetToolId), cocktail.Id + " tool");
            foreach (string id in cocktail.GetSelectableIngredientIds())
            {
                Assert.That(items.ContainsKey(id), Is.True, cocktail.Id + ": " + id);
                Assert.That(ids, Does.Contain(id), cocktail.Id + ": " + id);
                var slot = slots.Single(s => s.ItemId == id);
                Assert.That(slot.Icon, Is.Not.Null, id);
                Assert.That(slot.GetComponent<Collider2D>(), Is.Not.Null, id + " input collider");
                Assert.That(slot.ItemStage, Is.EqualTo(items[id].ShelfGroup == ENewShelfGroup.Liquor ?
                    CraftPrepStageScreen.Stage.Liquor : CraftPrepStageScreen.Stage.Fridge), id);
            }
        }
    }

    [Test]
    public void ShelfSetupPreservesExistingPositionsAndDoesNotDuplicateOnRerun()
    {
        var root = PrefabUtility.LoadPrefabContents(WebCraftingShelfSetup.PrefabPath);
        try
        {
            var screen = root.GetComponent<CraftPrepStageScreen>();
            var originals = Slots(screen).ToDictionary(s => s, s => s.transform.localPosition);
            WebCraftingShelfSetup.Complete(screen);
            var count = Slots(screen).Length;
            var cranberry = Slots(screen).Single(s => s.ItemId == "cranberry_juice");
            cranberry.transform.localPosition += Vector3.right;
            Vector3 edited = cranberry.transform.localPosition;
            WebCraftingShelfSetup.Complete(screen);
            Assert.That(Slots(screen).Length, Is.EqualTo(count));
            Assert.That(cranberry.transform.localPosition, Is.EqualTo(edited));
            foreach (var pair in originals.Where(p => p.Key != cranberry)) Assert.That(pair.Key.transform.localPosition, Is.EqualTo(pair.Value));
            foreach (string id in new[] { "cranberry_juice", "triple_sec", "kahlua", "milk", "amaretto", "wild_dog", "bitters", "sugar_cube" })
                Assert.That(Slots(screen).Single(s => s.ItemId == id).GetComponentInChildren<TMPro.TMP_Text>(true), Is.Not.Null, id);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void JohnnyRecipeAndIngredientsUnlockOnlyAfterAllPrerequisites()
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var conditions = new ConditionUtil(null, player, player);
            var data = Csv();
            var cocktail = data.Read<NewCocktailData[]>("cocktails").Single(c => c.Id == "johnny_old_fashioned");
            var sugar = data.Read<NewShelfItemData[]>("shelf_items").Single(i => i.Id == "sugar_cube");
            Assert.That(CocktailUnlockPolicy.CanSelect(cocktail, 3, true, conditions), Is.False, "주문만으로 획득 조건을 우회하지 않는다.");
            Assert.That(CraftShelf.IsAvailable(sugar, 3, conditions), Is.False);
            conditions.ApplyRequired("flag.gang_wild_dog_received = true; flag.shiba_bought_bitters = true; flag.campaign_observation = true");
            Assert.That(CampaignStoryRules.TryUnlockJohnnyRecipe(3, conditions), Is.False, "각설탕 수령 전");
            conditions.ApplyRequired("flag.day3_sugar_received = true");
            Assert.That(CampaignStoryRules.TryUnlockJohnnyRecipe(2, conditions), Is.False);
            Assert.That(CampaignStoryRules.TryUnlockJohnnyRecipe(3, conditions), Is.True);
            Assert.That(CampaignStoryRules.TryUnlockJohnnyRecipe(3, conditions), Is.False, "중복 해금 알림 방지");
            Assert.That(CocktailUnlockPolicy.IsUnlocked(cocktail, 3, conditions), Is.True);
            Assert.That(CraftShelf.IsAvailable(sugar, 3, conditions), Is.True);
            Assert.That(CraftShelf.IsAvailable(sugar, 2, conditions), Is.False);
        }
        finally { Object.DestroyImmediate(player); }
    }

    [Test]
    public void SugarIsNotAddedUnlessSelectedAndDoesNotRequireAPowderMinigame()
    {
        var data = Csv();
        var shelf = ScriptableObject.CreateInstance<NewShelfItemDataSO>();
        var runnerObject = new GameObject("AdditionRunnerTest");
        try
        {
            shelf.shelfItemData = data.Read<NewShelfItemData[]>("shelf_items");
            var cocktail = data.Read<NewCocktailData[]>("cocktails").Single(c => c.Id == "johnny_old_fashioned");
            var actual = new ActualCraft();
            Assert.That(GimmickQueueBuilder.Build(cocktail, actual, shelf).Steps.Any(s => s.IngredientId == "sugar_cube"), Is.False);
            actual.SelectIngredient("sugar_cube");
            var step = GimmickQueueBuilder.Build(cocktail, actual, shelf).Steps.Single(s => s.IngredientId == "sugar_cube");
            Assert.That(step.IsImmediateAddition, Is.True);
            var runner = runnerObject.AddComponent<GimmickRunner>();
            var result = runner.ExecuteAsync(step, default, new CraftTimer(), CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(result.ActualValue, Is.EqualTo(1));
            Assert.That(runnerObject.transform.childCount, Is.Zero, "파우더 프리팹을 생성하지 않는다.");
        }
        finally { Object.DestroyImmediate(shelf); Object.DestroyImmediate(runnerObject); }
    }
}
