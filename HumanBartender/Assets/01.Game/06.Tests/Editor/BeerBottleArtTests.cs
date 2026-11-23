using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 맥주 튜토리얼의 '뚜껑 딴 병' 그림(bottled_beer_off.png). 따르기 미니게임의 병과 병따기 성공 화면에 쓰고,
/// 레시피·해금 화면의 닫힌 병 그림은 그대로 둔다(웹 pour-fluid.js openArt, open-view.js).
/// </summary>
public sealed class BeerBottleArtTests
{
    const string OpenedBeer = "Assets/01.Game/03.Content/01.ArtResources/03.Crafting/04.Minigames/03.pour/03.Ingredients/bottled_beer_off.png";
    const string Catalog = "Assets/01.Game/04.Data/01.Definitions/UI/CocktailRecipeVisualCatalog.asset";
    const string CapGame = "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames/04.Cap/NewCapGame.prefab";

    [Test]
    public void PourUsesTheOpenedBottleWhileRecipeScreensKeepTheClosedOne()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<CocktailRecipeVisualCatalog>(Catalog);
        var pour = catalog.GetPourBottleSprite("beer");
        Assert.That(AssetDatabase.GetAssetPath(pour), Is.EqualTo(OpenedBeer));
        Assert.That(AssetDatabase.GetAssetPath(catalog.GetIngredientSprite("beer")), Is.Not.EqualTo(OpenedBeer),
            "레시피·해금 화면에 열린 병이 뜨면 안 된다.");
        Assert.That(catalog.GetPourBottleSprite("ginger_ale"), Is.SameAs(catalog.GetIngredientSprite("ginger_ale")),
            "따로 정하지 않은 재료는 지금처럼 재료 그림을 쓴다.");
        // 투명 여백을 잘라 둬야 PourManager가 병 크기·입구 위치를 그림 전체가 아닌 병 기준으로 맞춘다.
        Assert.That(pour.rect, Is.EqualTo(new Rect(25, 1, 43, 157)));
    }

    [Test]
    public void OpenedBeerFollowsThePixelArtImportRules()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(OpenedBeer);
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100));
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
    }

    [Test]
    public void RegisteredPourBottlesHaveTheirPivotAtTheMouthAndKeepPixelArtSettings()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<CocktailRecipeVisualCatalog>(Catalog);
        using var data = new SerializedObject(catalog);
        var entries = data.FindProperty("pourBottleSprites");
        for (int i = 0; i < entries.arraySize; i++)
        {
            string id = entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue;
            var sprite = catalog.GetPourBottleSprite(id);
            Assert.That(sprite, Is.Not.Null, id);
            string path = AssetDatabase.GetAssetPath(sprite);
            Assert.That(path, Does.EndWith("_off.png"), id);
            Assert.That(sprite.pivot.x, Is.EqualTo(sprite.rect.width * .5f).Within(.001f), id);
            Assert.That(sprite.pivot.y, Is.EqualTo(sprite.rect.height).Within(.001f), id);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100), id);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), id);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), id);
        }
    }

    [Test]
    public void CapGameSwapsToTheOpenedBottleOnSuccess()
    {
        var view = AssetDatabase.LoadAssetAtPath<GameObject>(CapGame).GetComponentInChildren<CapOpenView>(true);
        using var data = new SerializedObject(view);
        var opened = data.FindProperty("openedBottleTexture").objectReferenceValue;
        Assert.That(AssetDatabase.GetAssetPath(opened), Is.EqualTo(OpenedBeer));
        Assert.That(data.FindProperty("bottleTexture").objectReferenceValue, Is.Not.SameAs(opened), "닫힌 병은 그대로 둔다.");
    }
}
