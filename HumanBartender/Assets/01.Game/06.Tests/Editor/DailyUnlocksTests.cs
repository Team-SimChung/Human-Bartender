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
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class DailyUnlocksTests
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Field(object owner, string name) => owner.GetType().GetField(name, Fields).GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Fields).SetValue(owner, value);
    static T Find<T>(UnityEngine.SceneManagement.Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).FirstOrDefault();

    static NewShelfItemData Ingredient(string id, int day = 1, string when = null) =>
        new() { Id = id, Kind = ENewShelfKind.Ingredient, UnlockDay = day, UnlockWhen = when,
            Name = new LocalizedText { Ko = id }, Desc = new LocalizedText { Ko = id + " 설명" } };
    static NewCocktailData Cocktail(string id, int day = 1, string when = null) =>
        new() { Id = id, Status = ENewDataStatus.Confirmed, UnlockDay = day, UnlockWhen = when };

    [Test]
    public void QueryKeepsOnlyUnconditionalUnlocksOfThatDay()
    {
        var notIngredient = Ingredient("tool");
        notIngredient.Kind = ENewShelfKind.Tool;
        var unconfirmed = Cocktail("unconfirmed");
        unconfirmed.Status = (ENewDataStatus)999;
        var result = DailyUnlockQuery.Collect(1,
            new[] { Ingredient("z"), Ingredient("old", 0), Ingredient("future", 2), Ingredient("conditional", 1, "flag.test"),
                notIngredient, Ingredient("a", 1, "\\N"), Ingredient("b", 1, "  ") },
            new[] { Cocktail("z"), unconfirmed, Cocktail("old", 0), Cocktail("future", 2),
                Cocktail("conditional", 1, "flag.test"), Cocktail("a", 1, "\\N"), Cocktail("b", 1, " ") });
        Assert.That(result.Ingredients.Select(i => i.Id), Is.EqualTo(new[] { "z", "a", "b" }));
        Assert.That(result.Cocktails.Select(i => i.Id), Is.EqualTo(new[] { "z", "a", "b" }));
        Assert.That(result.IsEmpty, Is.False);
    }

    [Test]
    public void QueryHandlesNullInputsDefaultResultAndDayZero()
    {
        Assert.That(default(DailyUnlocks).IsEmpty, Is.True);
        Assert.That(new DailyUnlocks(null, null).IsEmpty, Is.True);
        Assert.That(DailyUnlockQuery.Collect(1, null, null).IsEmpty, Is.True);
        Assert.That(DailyUnlockQuery.Collect(0, new[] { Ingredient("day0", 0) }, new[] { Cocktail("day0", 0) }).IsEmpty, Is.True);
        Assert.That(DailyUnlockQuery.Collect(-1, new[] { Ingredient("negative", -1) }, null).IsEmpty, Is.True);
    }

    [Test]
    public void DayOneMatchesCurrentWebCsv()
    {
        var csv = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        var result = DailyUnlockQuery.Collect(1, csv.Read<NewShelfItemData[]>("shelf_items"), csv.Read<NewCocktailData[]>("cocktails"));
        Assert.That(result.Ingredients.Select(i => i.Id), Is.EqualTo(new[] { "beer", "tequila", "vodka", "rum", "orange_juice", "cola", "dry_vermouth" }));
        Assert.That(result.Cocktails.Select(i => i.Id), Is.EqualTo(new[] { "bottle_beer", "screwdriver", "tequila_sunrise", "dry_martini" }));
    }

    [TestCase(2, 10, 6)]
    [TestCase(3, 0, 0)]
    public void LaterDaysMatchWebAndExcludeConditionalUnlocks(int day, int ingredientCount, int cocktailCount)
    {
        var csv = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        var result = DailyUnlockQuery.Collect(day, csv.Read<NewShelfItemData[]>("shelf_items"), csv.Read<NewCocktailData[]>("cocktails"));
        Assert.That(result.Ingredients.Count, Is.EqualTo(ingredientCount));
        Assert.That(result.Cocktails.Count, Is.EqualTo(cocktailCount));
    }

    [Test]
    public void PlaySceneHasDailyUnlocksScreen()
    {
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(DailyUnlocksSetup.ScenePath, OpenSceneMode.Single);
            var screen = Find<DailyUnlocksScreen>(scene);
            Assert.That(screen, Is.Not.Null, "Setup Daily Unlocks 메뉴 실행 후 Play 씬을 저장해 주세요.");
            Assert.DoesNotThrow(screen.ValidateReferences);
            Assert.That(screen.IsOpen, Is.False);
            var canvas = screen.GetComponent<Canvas>();
            var scaler = screen.GetComponent<CanvasScaler>();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(canvas.sortingOrder, Is.EqualTo(12));
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(960, 540)));
            Assert.That(scaler.matchWidthOrHeight, Is.Zero);
            Assert.That(Field(Find<PlayPhaseController>(scene), "dailyUnlocksScreen"), Is.SameAs(screen));
            Assert.That((Canvas[])Field(Find<GimmickRunner>(scene), "barCanvases"), Has.No.Member(canvas));
            Assert.That((Canvas[])Field(Find<CraftPrepStageHost>(scene), "barCanvases"), Has.No.Member(canvas));
            Assert.That(screen.GetComponentsInChildren<RectTransform>(true).All(r => r.localScale == Vector3.one), Is.True);
        }
        finally
        {
            if (previous.Any(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    [Test]
    public void CardPrefabFollowsUiRules()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DailyUnlocksSetup.CardPath);
        Assert.That(prefab, Is.Not.Null, "Setup Daily Unlocks 메뉴로 카드 프리팹을 만들어 주세요.");
        var card = prefab.GetComponent<DailyUnlockCard>();
        Assert.That(card, Is.Not.Null);
        Assert.That(prefab.GetComponent<Selectable>(), Is.Not.Null);
        var icon = (Image)Field(card, "icon");
        Assert.That(icon.preserveAspect, Is.True);
        Assert.That(icon.raycastTarget, Is.False);
        Assert.That(((TMP_Text)Field(card, "nameText")).raycastTarget, Is.False);
        Assert.That(prefab.GetComponentsInChildren<Image>().Where(i => i.gameObject != prefab).All(i => !i.raycastTarget), Is.True);
        Assert.That(prefab.GetComponentsInChildren<RectTransform>().All(r => r.localScale == Vector3.one), Is.True);
        Assert.That(prefab.GetComponentsInChildren<TMP_Text>().All(t => !t.richText && !t.enableAutoSizing), Is.True);
    }

    // Pure screen fixtures do not run the user's setup menu or save authored assets.
    sealed class ScreenFixture : IDisposable
    {
        public readonly GameObject host;
        public readonly DailyUnlocksScreen screen;
        public readonly Button confirm;
        public readonly RectTransform ingredients;
        public readonly TMP_Text detail;
        public readonly Sprite sprite;
        readonly CocktailRecipeVisualCatalog catalog;
        readonly Texture2D texture;
        public ScreenFixture()
        {
            host = new GameObject("DailyUnlocksTest", typeof(RectTransform));
            host.SetActive(false);
            screen = host.AddComponent<DailyUnlocksScreen>();
            var root = Child("Root", host.transform);
            Set(screen, "root", root);
            Set(screen, "canvasGroup", root.AddComponent<CanvasGroup>());
            Set(screen, "dayText", Text("Day", root.transform));
            detail = Text("Detail", root.transform);
            detail.text = "안내 문구";
            Set(screen, "detailText", detail);
            confirm = Child("Confirm", root.transform).AddComponent<Button>();
            Set(screen, "confirmButton", confirm);
            foreach (string prefix in new[] { "ingredient", "cocktail" })
            {
                Set(screen, prefix + "CountText", Text(prefix + "Count", root.transform));
                Set(screen, prefix + "Empty", Child(prefix + "Empty", root.transform));
                var scroll = Child(prefix + "Scroll", root.transform).AddComponent<ScrollRect>();
                scroll.horizontal = false;
                scroll.viewport = (RectTransform)Child("Viewport", scroll.transform).transform;
                scroll.viewport.sizeDelta = new Vector2(373, 157);
                scroll.content = (RectTransform)Child("Content", scroll.viewport).transform;
                scroll.content.pivot = new Vector2(.5f, 1);
                var grid = scroll.content.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(86, 75);
                grid.spacing = new Vector2(7, 7);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 4;
                Set(screen, prefix + "Content", scroll.content);
                Set(screen, prefix + "Scroll", scroll);
            }
            ingredients = (RectTransform)Field(screen, "ingredientContent");
            var template = Child("CardTemplate", host.transform);
            var card = template.AddComponent<DailyUnlockCard>();
            var icon = Child("Icon", template.transform).AddComponent<Image>();
            icon.preserveAspect = true;
            Set(card, "icon", icon);
            Set(card, "nameText", Text("Name", template.transform));
            template.SetActive(false);
            Set(screen, "cardPrefab", card);
            texture = new Texture2D(1, 1);
            sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            catalog = ScriptableObject.CreateInstance<CocktailRecipeVisualCatalog>();
            var serialized = new SerializedObject(catalog);
            foreach (string field in new[] { "ingredientSprites", "cocktailSprites" })
            {
                var entries = serialized.FindProperty(field);
                entries.arraySize = 12;
                for (int i = 0; i < 12; i++)
                {
                    entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue = "item" + i;
                    entries.GetArrayElementAtIndex(i).FindPropertyRelative("sprite").objectReferenceValue = sprite;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Set(screen, "visuals", catalog);
            Set(screen, "fallbackSprite", sprite);
            typeof(DailyUnlocksScreen).GetMethod("Awake", Fields).Invoke(screen, null);
            host.SetActive(true);
        }

        public UniTask Show(int count) => screen.ShowAsync(1,
            new DailyUnlocks(Enumerable.Range(0, count).Select(i => Ingredient("item" + i)).ToArray(), null), CancellationToken.None);

        public void Dispose()
        {
            screen.Hide();
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(sprite);
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
    public void ShowAsyncRendersReusesAndCompletesOnConfirm()
    {
        using var f = new ScreenFixture();
        var first = f.Show(3);
        Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Pending));
        Assert.That(f.screen.IsOpen, Is.True);
        Assert.That(f.ingredients.childCount, Is.EqualTo(3));
        var card = f.ingredients.GetChild(0).GetComponent<DailyUnlockCard>();
        card.OnPointerEnter(null);
        Assert.That(f.detail.text, Is.EqualTo("item0 — item0 설명"));
        Assert.That(((GameObject)Field(f.screen, "cocktailEmpty")).activeSelf, Is.True);
        f.confirm.onClick.Invoke();
        first.GetAwaiter().GetResult();
        Assert.That(f.screen.IsOpen, Is.False);
        var second = f.Show(1);
        Assert.That(f.detail.text, Is.EqualTo("안내 문구"));
        Assert.That(f.ingredients.childCount, Is.EqualTo(3));
        Assert.That(f.ingredients.GetChild(0).GetComponent<DailyUnlockCard>(), Is.SameAs(card));
        Assert.That(f.ingredients.GetChild(1).gameObject.activeSelf, Is.False);
        Assert.That(f.ingredients.GetChild(2).gameObject.activeSelf, Is.False);
        f.confirm.onClick.Invoke();
        second.GetAwaiter().GetResult();
    }

    [Test]
    public void CancellationAndDisableReleaseThePendingScreen()
    {
        using var f = new ScreenFixture();
        using var ct = new CancellationTokenSource();
        var task = f.screen.ShowAsync(1, default, ct.Token);
        ct.Cancel();
        Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
        Assert.That(f.screen.IsOpen, Is.False);
        var retry = f.Show(1);
        typeof(DailyUnlocksScreen).GetMethod("OnDisable", Fields).Invoke(f.screen, null);
        Assert.Throws<OperationCanceledException>(() => retry.GetAwaiter().GetResult());
        Assert.That(f.screen.IsOpen, Is.False);
        var final = f.Show(1);
        f.confirm.onClick.Invoke();
        final.GetAwaiter().GetResult();
    }

    [Test]
    public void ConcurrentShowIsRejectedWithoutReplacingTheOriginalWait()
    {
        using var f = new ScreenFixture();
        var first = f.Show(3);
        var duplicate = f.Show(1);
        Assert.Throws<InvalidOperationException>(() => duplicate.GetAwaiter().GetResult());
        Assert.That(f.ingredients.childCount, Is.EqualTo(3));
        f.confirm.onClick.Invoke();
        first.GetAwaiter().GetResult();
    }

    [Test]
    public void RenderFailureDoesNotLeavePendingWaitOrVisibleRoot()
    {
        using var f = new ScreenFixture();
        Set(f.screen, "dayFormat", "{invalid}");
        var failed = f.Show(1);
        Assert.Throws<FormatException>(() => failed.GetAwaiter().GetResult());
        Assert.That(f.screen.IsOpen, Is.False);
        Set(f.screen, "dayFormat", "{0}일차");
        var retry = f.Show(1);
        f.confirm.onClick.Invoke();
        retry.GetAwaiter().GetResult();
    }

    [Test]
    public void MissingCatalogImageUsesFallbackAndWarnsWithId()
    {
        using var f = new ScreenFixture();
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "[DailyUnlocks] 'missing'의 이미지가 없어 대체 이미지를 사용합니다.");
        var task = f.screen.ShowAsync(1, new DailyUnlocks(new[] { Ingredient("missing") }, null), CancellationToken.None);
        var card = f.ingredients.GetChild(0).GetComponent<DailyUnlockCard>();
        Assert.That(((Image)Field(card, "icon")).sprite, Is.SameAs(f.sprite));
        f.confirm.onClick.Invoke();
        task.GetAwaiter().GetResult();
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
            var task = f.Show(1);
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
    public void CardSelectionUsesDescriptionAndNavigationStaysInsideScreen()
    {
        using var f = new ScreenFixture();
        var task = f.Show(10);
        var card = f.ingredients.GetChild(9).GetComponent<DailyUnlockCard>();
        card.OnSelect(null);
        Assert.That(f.detail.text, Is.EqualTo("item9 — item9 설명"));
        foreach (var selectable in f.ingredients.GetComponentsInChildren<Selectable>())
        {
            Assert.That(selectable.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            foreach (var target in new[] { selectable.navigation.selectOnUp, selectable.navigation.selectOnDown,
                selectable.navigation.selectOnLeft, selectable.navigation.selectOnRight })
                Assert.That(target.transform.IsChildOf(f.host.transform), Is.True);
        }
        f.confirm.onClick.Invoke();
        task.GetAwaiter().GetResult();
    }
}
