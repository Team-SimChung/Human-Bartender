using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class HiddenRecipeUnlockTests
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    const string CatalogPath = "Assets/01.Game/04.Data/01.Definitions/UI/CocktailRecipeVisualCatalog.asset";
    static object Field(object owner, string name) => owner.GetType().GetField(name, Fields).GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Fields).SetValue(owner, value);
    static T Find<T>(UnityEngine.SceneManagement.Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).FirstOrDefault();
    static CsvContentCatalog Csv() => CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));

    static NewCocktailRecipeStep Step(string id, float qty = 1, ENewUnit unit = ENewUnit.Ml) =>
        new() { Action = ENewRecipeAction.Pour, Ingredient = id, Qty = qty, Unit = unit };

    static NewCocktailData Drink(params NewCocktailRecipeStep[] steps) =>
        new() { Id = "test_drink", Status = ENewDataStatus.Confirmed, Name = new LocalizedText { Ko = "테스트" }, Recipe = steps };

    // Pure screen fixture: does not run the setup menu or touch saved assets.
    sealed class ScreenFixture : IDisposable
    {
        public readonly GameObject host;
        public readonly HiddenRecipeUnlockScreen screen;
        public readonly Button confirm;
        public readonly Image drink;
        public readonly TMP_Text drinkName;
        public readonly GameObject[] rows = new GameObject[3];
        public readonly Image[] icons = new Image[3];
        public readonly TMP_Text[] names = new TMP_Text[3];
        public readonly TMP_Text[] amounts = new TMP_Text[3];
        public readonly Sprite sprite;
        public readonly Sprite fallback;
        readonly CocktailRecipeVisualCatalog catalog;
        readonly Texture2D texture;

        public ScreenFixture()
        {
            host = new GameObject("HiddenRecipeUnlockTest", typeof(RectTransform));
            host.SetActive(false);
            screen = host.AddComponent<HiddenRecipeUnlockScreen>();
            var root = Child("Root", host.transform);
            Set(screen, "root", root);
            Set(screen, "canvasGroup", root.AddComponent<CanvasGroup>());
            confirm = Child("Confirm", root.transform).AddComponent<Button>();
            Set(screen, "confirmButton", confirm);
            drink = Child("Drink", root.transform).AddComponent<Image>();
            Set(screen, "drinkImage", drink);
            drinkName = Text("DrinkName", root.transform);
            Set(screen, "drinkNameText", drinkName);

            var serializedScreen = new SerializedObject(screen);
            var rowArray = serializedScreen.FindProperty("ingredientRows");
            rowArray.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = Child("Row" + i, root.transform);
                icons[i] = Child("Icon", rows[i].transform).AddComponent<Image>();
                names[i] = Text("Name", rows[i].transform);
                amounts[i] = Text("Amount", rows[i].transform);
                var element = rowArray.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("root").objectReferenceValue = rows[i];
                element.FindPropertyRelative("icon").objectReferenceValue = icons[i];
                element.FindPropertyRelative("nameText").objectReferenceValue = names[i];
                element.FindPropertyRelative("amountText").objectReferenceValue = amounts[i];
            }
            serializedScreen.ApplyModifiedPropertiesWithoutUndo();

            texture = new Texture2D(1, 1);
            sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            fallback = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            catalog = ScriptableObject.CreateInstance<CocktailRecipeVisualCatalog>();
            var serializedCatalog = new SerializedObject(catalog);
            Entries(serializedCatalog, "cocktailSprites", CampaignStoryRules.JohnnyCocktailId, "test_drink");
            // sugar_cube is intentionally missing so the fallback path is exercised.
            Entries(serializedCatalog, "ingredientSprites", "wild_dog", "bitters", "a", "b", "c", "d");
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            Set(screen, "visuals", catalog);
            Set(screen, "fallbackSprite", fallback);
            typeof(HiddenRecipeUnlockScreen).GetMethod("Awake", Fields).Invoke(screen, null);
            host.SetActive(true);
        }

        void Entries(SerializedObject serialized, string field, params string[] ids)
        {
            var entries = serialized.FindProperty(field);
            entries.arraySize = ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue = ids[i];
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("sprite").objectReferenceValue = sprite;
            }
        }

        public UniTask Show(NewCocktailData cocktail, NewShelfItemData[] shelf = null, CancellationToken ct = default) =>
            screen.ShowAsync(cocktail, shelf, ct);

        public UniTask ShowTest() => Show(Drink(Step("a")));

        public void Dispose()
        {
            screen.Hide();
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(fallback);
            Object.DestroyImmediate(texture);
        }

        static GameObject Child(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        static TMP_Text Text(string name, Transform parent) => Child(name, parent).AddComponent<TextMeshProUGUI>();
    }

    [Test]
    public void ShowAsyncFillsOldFashionedFromCsvAndCompletesOnConfirm()
    {
        var csv = Csv();
        var cocktail = csv.Read<NewCocktailData[]>("cocktails").Single(c => c.Id == CampaignStoryRules.JohnnyCocktailId);
        var shelf = csv.Read<NewShelfItemData[]>("shelf_items");
        using var f = new ScreenFixture();
        LogAssert.Expect(LogType.Warning, "[HiddenRecipeUnlock] 'sugar_cube'의 이미지가 없어 대체 이미지를 사용합니다.");
        var task = f.Show(cocktail, shelf);
        Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));
        Assert.That(f.screen.IsOpen, Is.True);
        Assert.That(f.drinkName.text, Is.EqualTo("올드 패션드"));
        Assert.That(f.drink.sprite, Is.SameAs(f.sprite));
        Assert.That(f.names.Select(t => t.text), Is.EqualTo(new[] { "와일드 독", "비터스", "각설탕" }));
        Assert.That(f.amounts.Select(t => t.text), Is.EqualTo(new[] { "45ml", "5ml", "1개" }));
        Assert.That(f.rows.All(r => r.activeSelf), Is.True);
        Assert.That(f.icons[2].sprite, Is.SameAs(f.fallback));
        f.confirm.onClick.Invoke();
        task.GetAwaiter().GetResult();
        Assert.That(f.screen.IsOpen, Is.False);
    }

    [Test]
    public void SurplusRowsAreHiddenAndOverflowWarns()
    {
        using var f = new ScreenFixture();
        var shortTask = f.Show(Drink(Step("a", 2, ENewUnit.Oz)));
        Assert.That(f.rows.Select(r => r.activeSelf), Is.EqualTo(new[] { true, false, false }));
        Assert.That(f.names[0].text, Is.EqualTo("a"), "선반 데이터가 없으면 ID를 표시합니다.");
        Assert.That(f.amounts[0].text, Is.EqualTo("2oz"));
        f.confirm.onClick.Invoke();
        shortTask.GetAwaiter().GetResult();

        LogAssert.Expect(LogType.Warning, "[HiddenRecipeUnlock] 'test_drink'의 재료 4줄 중 3줄만 표시합니다. 씬에 줄을 추가하세요.");
        var longTask = f.Show(Drink(Step("a"), Step("b"), Step("c"), Step("d")));
        Assert.That(f.rows.All(r => r.activeSelf), Is.True);
        f.confirm.onClick.Invoke();
        longTask.GetAwaiter().GetResult();
    }

    [Test]
    public void CancellationAndDisableReleaseThePendingScreen()
    {
        using var f = new ScreenFixture();
        using var ct = new CancellationTokenSource();
        var task = f.Show(Drink(Step("a")), null, ct.Token);
        ct.Cancel();
        Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
        Assert.That(f.screen.IsOpen, Is.False);
        var retry = f.ShowTest();
        typeof(HiddenRecipeUnlockScreen).GetMethod("OnDisable", Fields).Invoke(f.screen, null);
        Assert.Throws<OperationCanceledException>(() => retry.GetAwaiter().GetResult());
        Assert.That(f.screen.IsOpen, Is.False);
        var final = f.ShowTest();
        f.confirm.onClick.Invoke();
        final.GetAwaiter().GetResult();
    }

    [Test]
    public void ConcurrentShowIsRejectedWithoutReplacingTheOriginalWait()
    {
        using var f = new ScreenFixture();
        var first = f.ShowTest();
        var duplicate = f.ShowTest();
        Assert.Throws<InvalidOperationException>(() => duplicate.GetAwaiter().GetResult());
        Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Pending));
        Assert.That(f.screen.IsOpen, Is.True);
        f.confirm.onClick.Invoke();
        first.GetAwaiter().GetResult();
    }

    [Test]
    public void ConfirmIsIgnoredWhileOptionsAreOpen()
    {
        using var f = new ScreenFixture();
        var optionsHost = new GameObject("OptionsTest");
        var activeField = typeof(UIDisplayOptions).GetField("activeOptions", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = activeField.GetValue(null);
        try
        {
            var options = optionsHost.AddComponent<UIDisplayOptions>();
            var panel = new GameObject("OptionsRoot");
            panel.transform.SetParent(optionsHost.transform);
            Set(options, "optionUI", panel);
            activeField.SetValue(null, options);
            var task = f.ShowTest();
            Assert.That(UIDisplayOptions.IsOptionOpen, Is.True);
            f.confirm.onClick.Invoke();
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));
            panel.SetActive(false);
            f.confirm.onClick.Invoke();
            task.GetAwaiter().GetResult();
        }
        finally
        {
            Object.DestroyImmediate(optionsHost);
            activeField.SetValue(null, previous);
        }
    }

    [Test]
    public void MissingReferencesAreReportedBeforeShowing()
    {
        using var f = new ScreenFixture();
        Set(f.screen, "drinkImage", null);
        var task = f.ShowTest();
        Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult());
        Assert.That(f.screen.IsOpen, Is.False);
    }

    [Test]
    public void TestDaySeedingFillsOnlyMissingPrerequisitesAndThenUnlocks()
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var conditions = new ConditionUtil(null, player, player);
            Assert.That(CampaignStoryRules.MissingJohnnyPrerequisites(conditions),
                Is.EqualTo(new[] { "wild_dog", "bitters", "johnny_memory" }));
            conditions.ApplyRequired("flag.shiba_bought_wild_dog = true; flag.day3_sugar_received = true");
            Assert.That(CampaignStoryRules.TryUnlockJohnnyRecipe(3, conditions), Is.False, "Test Day만으로는 해금되지 않음");

            var seeded = CampaignStoryRules.SeedJohnnyPrerequisites(conditions);
            Assert.That(seeded, Is.EqualTo(new[] { "shiba_bought_bitters", "campaign_observation" }));
            Assert.That(conditions.CheckRequired("flag.gang_wild_dog_received"), Is.False, "이미 가진 와일드 독 기록은 유지");
            Assert.That(CampaignStoryRules.MissingJohnnyPrerequisites(conditions), Is.Empty);
            Assert.That(CampaignStoryRules.SeedJohnnyPrerequisites(conditions), Is.Empty, "두 번 불러도 바뀌지 않음");
            Assert.That(CampaignStoryRules.TryUnlockJohnnyRecipe(3, conditions), Is.True);
        }
        finally { Object.DestroyImmediate(player); }
    }

    [Test]
    public void BarDialogueLabDayThreeUsesTheSamePrerequisiteFlags()
    {
        var scenario = BarDialogueTestCatalog.Scenario(3);
        var flags = (System.Collections.Generic.IReadOnlyDictionary<string, bool>)Field(scenario, "flagOverrides");
        Assert.That(flags.Keys, Is.EquivalentTo(CampaignStoryRules.JohnnyPrerequisiteFlags));
        Assert.That(flags.Values.All(v => v), Is.True);
    }

    [Test]
    public void CatalogUsesTheOldFashionedArtInsteadOfGodfather()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<CocktailRecipeVisualCatalog>(CatalogPath);
        Assert.That(catalog, Is.Not.Null);
        var sprite = catalog.GetCocktailSprite(CampaignStoryRules.JohnnyCocktailId);
        Assert.That(sprite, Is.Not.Null);
        Assert.That(sprite, Is.Not.SameAs(catalog.GetCocktailSprite("godfather")));
        Assert.That(AssetDatabase.GetAssetPath(sprite), Does.EndWith("/01.Finished/Old_fashioned_final.png"));
        // Web item_/recipe_item_ for Johnny's ingredients: assets/backgrounds/campaign/johnny-*.png.
        foreach (string id in new[] { "wild_dog", "bitters", "sugar_cube" })
            Assert.That(AssetDatabase.GetAssetPath(catalog.GetIngredientSprite(id)),
                Does.EndWith($"/02.Ingredients/02.Shelf/johnny-{id}.png"), id);
    }

    [Test]
    public void PlaySceneHasHiddenRecipeUnlockScreen()
    {
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(HiddenRecipeUnlockSetup.ScenePath, OpenSceneMode.Single);
            var screen = Find<HiddenRecipeUnlockScreen>(scene);
            Assert.That(screen, Is.Not.Null, "Setup Hidden Recipe Unlock 메뉴 실행 후 Play 씬을 저장해 주세요.");
            Assert.DoesNotThrow(screen.ValidateReferences);
            Assert.That(screen.IsOpen, Is.False);
            Assert.That(screen.name, Is.EqualTo(HiddenRecipeUnlockSetup.CanvasName));
            var canvas = screen.GetComponent<Canvas>();
            var scaler = screen.GetComponent<CanvasScaler>();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(canvas.sortingOrder, Is.EqualTo(HiddenRecipeUnlockSetup.SortOrder));
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(960, 540)));
            Assert.That(scaler.matchWidthOrHeight, Is.Zero);
            var daily = Find<DailyUnlocksScreen>(scene);
            if (daily != null && daily.transform.parent == screen.transform.parent)
                Assert.That(screen.transform.GetSiblingIndex(), Is.GreaterThan(daily.transform.GetSiblingIndex()),
                    "같은 Sort Order이므로 DailyUnlocks 뒤에 두어 위에 그립니다.");
            Assert.That(Field(Find<StoryScriptRunner>(scene), "hiddenRecipeScreen"), Is.SameAs(screen));
            Assert.That(Field(Find<PlayInputHandler>(scene), "hiddenRecipeScreen"), Is.SameAs(screen));
            var root = (GameObject)Field(screen, "root");
            var backdrop = root.transform.Find("BackgroundDimOverlay").GetComponent<Image>();
            Assert.That(backdrop.raycastTarget, Is.True, "뒤의 바 UI 클릭을 막습니다.");
            var button = (Button)Field(screen, "confirmButton");
            Assert.That(screen.GetComponentsInChildren<Image>(true)
                .Where(i => i != backdrop && i.gameObject != button.gameObject).All(i => !i.raycastTarget), Is.True);
            Assert.That(screen.GetComponentsInChildren<TMP_Text>(true).All(t => !t.raycastTarget && !t.enableAutoSizing), Is.True);
            // The overlay canvas root's scale is driven by the Canvas, so only its children are checked.
            Assert.That(screen.GetComponentsInChildren<RectTransform>(true).Where(r => r != screen.transform)
                .All(r => r.localScale == Vector3.one), Is.True);
            Assert.That(screen.GetComponentsInChildren<Transform>(true).All(t => !t.name.Contains(' ')), Is.True);
        }
        finally
        {
            if (previous.Any(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
