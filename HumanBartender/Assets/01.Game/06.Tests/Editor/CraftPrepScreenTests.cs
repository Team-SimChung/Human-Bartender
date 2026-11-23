using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 엔진 수정 목록 02·03·04(제조 준비 화면). 웹 bar-playtest의 preparation-resources.css·bar-views.css·
/// bar-views.js prepRecipeBrief가 기준이다.
/// </summary>
public sealed class CraftPrepScreenTests
{
    const string StagePath = "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/01.Preparation/CraftPrepStage.prefab";
    const string TagChipPath = "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/04.Recipes/RecipeTagChip.prefab";
    const string RowPath = "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/01.Preparation/CraftPrepRecipeBriefRow.prefab";
    const string ArtPath = "Assets/01.Game/03.Content/01.ArtResources/05.UI/03.Crafting/01.Preparation/";
    const string TrayBackgroundPath = ArtPath + "IMG_igd_backround.png";
    static readonly string[] BriefArt =
        { "BTN_recipe_brief_A", "BTN_recipe_brief_B", "IMG_brief_check_box_A", "IMG_brief_check_box_B", "IMG_brief_check", "IMG_brief_arrow" };

    static Sprite Art(string name) => AssetDatabase.LoadAllAssetsAtPath(ArtPath + name + ".png").OfType<Sprite>().Single();

    static readonly Color WebToolTag = new Color32(0xDC, 0xB0, 0x76, 0xFF);
    static readonly Color WebDefaultTag = new Color32(0x62, 0x81, 0xA0, 0xFF);
    static readonly Color WebPackedRow = new Color32(0xAD, 0xF2, 0xE4, 0xFF);
    static readonly Color WebRow = new Color32(0xD9, 0xE7, 0xE9, 0xFF);

    static CsvContentCatalog Csv() => CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
    static NewCocktailData Cocktail(CsvContentCatalog csv, string id) => csv.Read<NewCocktailData[]>("cocktails").Single(c => c.Id == id);
    static FieldInfo FieldInfo(System.Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    static T Field<T>(object owner, string name) => (T)FieldInfo(owner.GetType(), name).GetValue(owner);
    static void Inject(object owner, string name, object value) => FieldInfo(owner.GetType(), name).SetValue(owner, value);
    static void Call(object owner, string name) =>
        owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
    static void AssertColor(Color actual, Color expected, string message) =>
        Assert.That(Vector4.Distance(actual, expected), Is.LessThan(0.005f), $"{message}: {actual} != {expected}");

    // ── 02. 레시피 태그 색 ───────────────────────────────────────────────

    [Test]
    public void TagChipUsesAccentOnlyForToolTag()
    {
        var chip = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TagChipPath)).GetComponent<RecipeTagChip>();
        try
        {
            AssertColor(chip.ToolColor, WebToolTag, "도구명 태그 색(웹 #dcb076)");
            AssertColor(chip.DefaultColor, WebDefaultTag, "기본 태그 색(웹 #6281a0)");
            var label = chip.GetComponentInChildren<TextMeshProUGUI>(true);
            var outline = Field<Image>(chip, "background");

            chip.Bind("SHAKE", true);
            Assert.That(label.text, Is.EqualTo("SHAKE"), "태그 문구는 바꾸지 않는다");
            AssertColor(label.color, WebToolTag, "도구 태그 글자");
            AssertColor(outline.color, WebToolTag, "도구 태그 외곽선은 글자와 같은 색");

            chip.Bind("상큼한", false);
            AssertColor(label.color, WebDefaultTag, "맛 태그 글자");
            AssertColor(outline.color, WebDefaultTag, "맛 태그 외곽선");
        }
        finally { Object.DestroyImmediate(chip.gameObject); }
    }

    [Test]
    public void RecipePreviewHighlightsOnlyTheFirstToolTag()
    {
        var csv = Csv();
        var tonic = Cocktail(csv, "gin_tonic");
        var root = PrefabUtility.LoadPrefabContents(StagePath);
        try
        {
            var screen = root.GetComponent<CraftPrepStageScreen>();
            var preview = Field<CocktailRecipePreview>(screen, "recipePreview");
            Assert.That(FieldInfo(typeof(CocktailRecipePreview), "categoryColors"), Is.Null, "태그마다 다른 색을 고르지 않는다");

            preview.Show(tonic, Field<CocktailRecipeVisualCatalog>(screen, "recipeVisuals"));
            var chips = Field<RectTransform>(preview, "tagList").GetComponentsInChildren<RecipeTagChip>(true);
            var labels = chips.Select(c => c.GetComponentInChildren<TextMeshProUGUI>(true)).ToArray();
            int tagCount = tonic.Tags.Count(t => !string.IsNullOrWhiteSpace(t.Ko));
            Assert.That(chips.Length, Is.EqualTo(1 + tagCount), "태그를 지우거나 더하지 않는다");
            Assert.That(labels[0].text, Is.EqualTo(tonic.Mix.ToString().ToUpperInvariant()));
            AssertColor(labels[0].color, WebToolTag, "첫 태그(도구명)");
            for (int index = 1; index < labels.Length; index++)
                AssertColor(labels[index].color, WebDefaultTag, labels[index].text);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ── 03. 하단 선택 슬롯 가운데 정렬 ────────────────────────────────────

    [Test]
    public void SelectionTrayIsCenteredAndStaysWhenRecipeOpens()
    {
        Assert.That(FieldInfo(typeof(CraftPrepStageScreen), "trayPosition"), Is.Null);
        Assert.That(FieldInfo(typeof(CraftPrepStageScreen), "trayShiftWhenRecipeOpen"), Is.Null);
        var root = PrefabUtility.LoadPrefabContents(StagePath);
        try
        {
            var screen = root.GetComponent<CraftPrepStageScreen>();
            var tray = Field<RectTransform>(screen, "trayContent");
            Assert.That(tray.anchorMin.x, Is.EqualTo(0.5f));
            Assert.That(tray.anchorMax.x, Is.EqualTo(0.5f));
            Assert.That(tray.pivot.x, Is.EqualTo(0.5f));
            Assert.That(tray.anchoredPosition.x, Is.Zero, "화면(부모) 가운데");
            var layout = tray.GetComponent<HorizontalLayoutGroup>();
            Assert.That(layout.childAlignment, Is.EqualTo(TextAnchor.MiddleCenter));
            Assert.That(layout.spacing, Is.EqualTo(9));
            Assert.That(layout.childControlWidth || layout.childControlHeight || layout.childForceExpandWidth ||
                        layout.childForceExpandHeight || layout.childScaleWidth || layout.childScaleHeight, Is.False,
                "AGENTS.md Layout Group 기본값");

            Inject(screen, "preparation", new CraftPreparation(Cocktail(Csv(), "gin_tonic"), new ActualCraft()));
            Vector2 before = tray.anchoredPosition;
            Call(screen, "OpenRecipe");
            Assert.That(screen.IsRecipeOpen, Is.True);
            Assert.That(tray.anchoredPosition, Is.EqualTo(before), "레시피를 열어도 슬롯 위치는 그대로");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void TrayBackgroundCropsTheWhiteExportMargin()
    {
        var sprite = AssetDatabase.LoadAllAssetsAtPath(TrayBackgroundPath).OfType<Sprite>().Single();
        // 원본 위쪽 11px과 오른쪽 끝 1px 열이 흰색이다(웹 css "Crop the source's 11px white export margin").
        Assert.That(sprite.rect.yMax, Is.LessThanOrEqualTo(sprite.texture.height - 11), "위쪽 흰 여백");
        Assert.That(sprite.rect.xMax, Is.LessThanOrEqualTo(sprite.texture.width - 1), "오른쪽 흰 열");
        var root = PrefabUtility.LoadPrefabContents(StagePath);
        try
        {
            var tray = Field<RectTransform>(root.GetComponent<CraftPrepStageScreen>(), "trayContent");
            var background = tray.parent.GetComponent<Image>();
            Assert.That(background.sprite, Is.EqualTo(sprite));
            Assert.That(background.color.a, Is.EqualTo(0.92f).Within(0.001f));
            Assert.That(background.rectTransform.sizeDelta.y, Is.EqualTo(110));
            Assert.That(background.raycastTarget, Is.False);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void ShelfNavigationAndItemTooltipUseWebLayout()
    {
        var root = PrefabUtility.LoadPrefabContents(StagePath);
        try
        {
            var screen = root.GetComponent<CraftPrepStageScreen>();
            Assert.That(Field<float>(screen, "cameraMoveDuration"), Is.EqualTo(0.45f));
            foreach (string field in new[] { "previousButton", "nextButton" })
            {
                var arrow = (RectTransform)Field<Button>(screen, field).transform;
                Assert.That(arrow.sizeDelta, Is.EqualTo(new Vector2(20, 35)));
            }

            var buttons = Field<Button[]>(screen, "stageButtons");
            var dots = Field<Image[]>(screen, "stageIndicators");
            Assert.That(buttons.Length, Is.EqualTo(Field<Transform[]>(screen, "stageAnchors").Length));
            Assert.That(dots.Length, Is.EqualTo(buttons.Length));
            for (int i = 0; i < buttons.Length; i++)
            {
                Assert.That(buttons[i].targetGraphic, Is.EqualTo(dots[i]));
                Assert.That(dots[i].transform.parent, Is.EqualTo(buttons[i].transform));
                Assert.That(dots[i].rectTransform.sizeDelta, Is.EqualTo(Vector2.one * (i == 0 ? 30 : 13)));
                Assert.That(((RectTransform)buttons[i].transform).sizeDelta, Is.EqualTo(new Vector2(18, 30)));
                Assert.That(buttons[i].GetComponent<Image>().raycastTarget, Is.True);
                Assert.That(dots[i].raycastTarget, Is.False);
            }
            Inject(screen, "stageIndex", 2);
            Call(screen, "RefreshStageIndicators");
            for (int i = 0; i < dots.Length; i++)
                Assert.That(dots[i].rectTransform.sizeDelta, Is.EqualTo(Vector2.one * (i == 2 ? 30 : 13)));

            var tooltip = Field<CraftPrepItemTooltip>(screen, "itemTooltip");
            var frame = Field<SpriteRenderer>(tooltip, "frame");
            Assert.That(frame.drawMode, Is.EqualTo(SpriteDrawMode.Sliced));
            Assert.That(frame.sprite.border, Is.EqualTo(Vector4.one * 8));
            Assert.That(Field<Vector2>(tooltip, "contentPadding"), Is.EqualTo(new Vector2(11, 8)));
            Assert.That(Field<TextMeshPro>(tooltip, "nameText").horizontalAlignment, Is.EqualTo(HorizontalAlignmentOptions.Left));
            Assert.That(Field<TextMeshPro>(tooltip, "descriptionText").horizontalAlignment, Is.EqualTo(HorizontalAlignmentOptions.Left));

            var item = Field<CraftPrepTrayItem>(screen, "trayItemPrefab");
            Assert.That(item.GetComponentInChildren<TMP_Text>(true), Is.Null);
            Assert.That(item.SelectionControl, Is.EqualTo(item.transform));
            Assert.That(((RectTransform)item.transform).sizeDelta, Is.EqualTo(new Vector2(70, 70)));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ── 04. 레시피 버튼과 호버 목록 ───────────────────────────────────────

    [Test]
    public void ChecklistFollowsWebOrderAndPackedState()
    {
        var csv = Csv();
        var shelf = ScriptableObject.CreateInstance<NewShelfItemDataSO>();
        try
        {
            shelf.shelfItemData = csv.Read<NewShelfItemData[]>("shelf_items");
            var list = new List<CraftPrepChecklistEntry>();

            var sour = Cocktail(csv, "whiskey_sour");
            var prep = new CraftPreparation(sour, new ActualCraft());
            CraftPrepRecipeChecklist.Build(prep, shelf, list);
            Assert.That(list.Select(e => e.Id),
                Is.EqualTo(new[] { sour.Glass, NewToolIds.Shaker }.Concat(sour.GetSelectableIngredientIds())), "잔 → 도구 → 재료");
            Assert.That(list.Take(2).Select(e => e.Kind), Is.EqualTo(new[] { ECraftPrepChecklistKind.Glass, ECraftPrepChecklistKind.Tool }));
            Assert.That(list.Skip(2).All(e => e.Kind == ECraftPrepChecklistKind.Ingredient), Is.True);
            Assert.That(list.Any(e => e.Packed), Is.False);

            prep.ToggleGlass(sour.Glass);
            prep.ToggleTool(NewToolIds.Shaker);
            prep.ToggleIngredient("whiskey");
            CraftPrepRecipeChecklist.Build(prep, shelf, list);
            Assert.That(list.Where(e => e.Packed).Select(e => e.Id), Is.EquivalentTo(new[] { sour.Glass, NewToolIds.Shaker, "whiskey" }));

            prep.ToggleTool(NewToolIds.MixingGlass);
            CraftPrepRecipeChecklist.Build(prep, shelf, list);
            Assert.That(list.Single(e => e.Id == NewToolIds.Shaker).Packed, Is.False, "다른 도구를 고르면 담음이 아니다");

            var beer = Cocktail(csv, "bottle_beer");
            var beerPrep = new CraftPreparation(beer, new ActualCraft());
            CraftPrepRecipeChecklist.Build(beerPrep, shelf, list);
            Assert.That(list.Select(e => e.Id), Is.EqualTo(new[] { beer.Glass, NewToolIds.BottleOpener, "beer" }), "병을 따는 재료가 있으면 병따개");
            Assert.That(list[1].Kind, Is.EqualTo(ECraftPrepChecklistKind.Tool));
            Assert.That(list[1].Packed, Is.False);
            beerPrep.ToggleBottleOpener();
            CraftPrepRecipeChecklist.Build(beerPrep, shelf, list);
            Assert.That(list[1].Packed, Is.True);

            var tonic = Cocktail(csv, "gin_tonic");
            CraftPrepRecipeChecklist.Build(new CraftPreparation(tonic, new ActualCraft()), shelf, list);
            Assert.That(list.Any(e => e.Kind == ECraftPrepChecklistKind.Tool), Is.False, "빌드는 도구 줄이 없다");
        }
        finally { Object.DestroyImmediate(shelf); }
    }

    [Test]
    public void RecipeBriefShowsCocktailNameAndPackedRowsOnHover()
    {
        var csv = Csv();
        var tonic = Cocktail(csv, "gin_tonic");
        var root = PrefabUtility.LoadPrefabContents(StagePath);
        try
        {
            var screen = root.GetComponent<CraftPrepStageScreen>();
            var brief = Field<CraftPrepRecipeBrief>(screen, "recipeBrief");
            var reveal = Field<CanvasGroup>(brief, "revealGroup");
            var rowList = Field<RectTransform>(brief, "rowList");
            var prep = new CraftPreparation(tonic, new ActualCraft());
            prep.ToggleGlass(tonic.Glass);
            prep.ToggleIngredient("gin");
            Inject(screen, "preparation", prep);

            Call(screen, "RefreshRecipeBrief");
            Assert.That(brief.CocktailName, Is.EqualTo(tonic.Name.Ko));
            Assert.That(brief.RowCount, Is.EqualTo(3));
            Assert.That(brief.IsExpanded, Is.False);
            Assert.That(brief.CurrentHeight, Is.EqualTo(brief.CollapsedHeight));
            Assert.That(reveal.alpha, Is.Zero, "마우스를 올리기 전에는 목록을 숨긴다");
            Assert.That(brief.BackgroundSprite, Is.EqualTo(Art("BTN_recipe_brief_A")), "기본 패널");

            brief.OnPointerEnter(new PointerEventData(null));
            Assert.That(brief.IsExpanded, Is.True);
            Assert.That(reveal.alpha, Is.EqualTo(1f));
            Assert.That(brief.BackgroundSprite, Is.EqualTo(Art("BTN_recipe_brief_B")), "호버 패널");
            var rows = Enumerable.Range(0, brief.RowCount).Select(index => brief.GetRow(index)).ToArray();
            var layout = rowList.GetComponent<VerticalLayoutGroup>();
            float listHeight = layout.padding.vertical + rows.Sum(r => ((RectTransform)r.transform).rect.height) +
                               layout.spacing * (rows.Length - 1);
            Assert.That(brief.CurrentHeight, Is.EqualTo(brief.CollapsedHeight + Mathf.Ceil(listHeight)).Within(0.01f), "목록만큼 아래로 펼친다");

            Assert.That(rows.Select(r => r.ItemId), Is.EqualTo(new[] { tonic.Glass, "gin", "soda_water" }));
            Assert.That(rows.Select(r => r.IsPacked), Is.EqualTo(new[] { true, true, false }));
            Assert.That(rows.Select(r => r.IsCheckMarkVisible), Is.EqualTo(new[] { true, true, false }), "담은 줄만 체크 표시");
            Assert.That(rows.Select(r => r.CheckBoxSprite), Is.EqualTo(new[]
                { Art("IMG_brief_check_box_B"), Art("IMG_brief_check_box_B"), Art("IMG_brief_check_box_A") }), "체크 칸 그림");
            Assert.That(rows[1].DisplayName, Is.EqualTo("진"));
            AssertColor(rows[1].NameColor, WebPackedRow, "담은 줄 색(웹 #adf2e4)");
            AssertColor(rows[2].NameColor, WebRow, "담지 않은 줄 색");

            brief.OnPointerExit(new PointerEventData(null));
            Assert.That(brief.IsExpanded, Is.False);
            Assert.That(brief.CurrentHeight, Is.EqualTo(brief.CollapsedHeight));
            Assert.That(reveal.alpha, Is.Zero);
            Assert.That(brief.BackgroundSprite, Is.EqualTo(Art("BTN_recipe_brief_A")));

            int instances = rowList.childCount;
            prep.ToggleIngredient("gin");
            Call(screen, "RefreshRecipeBrief");
            Assert.That(rowList.childCount, Is.EqualTo(instances), "선택이 바뀌어도 줄을 새로 만들지 않는다");
            Assert.That(brief.GetRow(1).IsPacked, Is.False, "뺀 재료는 미선택");

            brief.Bind(tonic.Name.Ko, new[] { new CraftPrepChecklistEntry(tonic.Glass, ECraftPrepChecklistKind.Glass, false) }, null);
            Assert.That(rowList.childCount, Is.EqualTo(instances));
            Assert.That(rowList.Cast<Transform>().Count(t => t.gameObject.activeSelf), Is.EqualTo(1), "남는 줄은 숨긴다");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void RecipeButtonIsTopLeftAndBackButtonIsBottomLeft()
    {
        var root = PrefabUtility.LoadPrefabContents(StagePath);
        try
        {
            var screen = root.GetComponent<CraftPrepStageScreen>();
            var brief = Field<CraftPrepRecipeBrief>(screen, "recipeBrief");
            var rect = (RectTransform)brief.transform;
            // 웹 left 24 · top 18 · width 280(1280 기준)을 960×540 캔버스로 0.75배.
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0, 1)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0, 1)));
            Assert.That(rect.pivot, Is.EqualTo(new Vector2(0, 1)), "위쪽 피벗이라 아래로 펼쳐진다");
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(18, -14)));
            Assert.That(rect.sizeDelta.x, Is.EqualTo(210));

            var back = (RectTransform)Field<Button>(screen, "backButton").transform;
            Assert.That(back.anchorMin, Is.EqualTo(Vector2.zero), "뒤로가기는 왼쪽 아래");
            Assert.That(back.anchorMax, Is.EqualTo(Vector2.zero));

            Assert.That(brief.Button, Is.SameAs(brief.GetComponent<Button>()));
            Assert.That(brief.Button.onClick.GetPersistentEventCount(), Is.Zero, "버튼은 코드에서 AddListener로 연결한다");
            Assert.That(screen.GetTutorialTarget("readRecipe", false), Is.SameAs(brief.transform), "튜토리얼 강조가 새 버튼을 가리킨다");

            foreach (var child in brief.GetComponentsInChildren<RectTransform>(true))
            {
                Assert.That(child.localScale, Is.EqualTo(Vector3.one), child.name + " scale");
                Assert.That(child.anchoredPosition.x % 1f == 0f && child.anchoredPosition.y % 1f == 0f &&
                            child.sizeDelta.x % 1f == 0f && child.sizeDelta.y % 1f == 0f, Is.True, child.name + " 정수 값");
                Assert.That(child.name, Does.Not.Contain(" "), "이름에 띄어쓰기 없음");
            }
            foreach (var graphic in brief.GetComponentsInChildren<Graphic>(true))
            {
                Assert.That(graphic.raycastTarget, Is.EqualTo(graphic == brief.Button.targetGraphic), graphic.name + " Raycast Target");
                Assert.That(graphic is Image || graphic is TMP_Text, Is.True, graphic.name + "은 리소스 그림이나 글자여야 한다(코드 도형 금지)");
                if (graphic is Image image) Assert.That(image.color, Is.EqualTo(Color.white), image.name + " 색은 그림에 들어 있다");
            }
            foreach (var text in brief.GetComponentsInChildren<TMP_Text>(true))
                Assert.That(text.richText || text.enableAutoSizing, Is.False, text.name);

            var panel = (Image)brief.Button.targetGraphic;
            Assert.That(panel.type, Is.EqualTo(Image.Type.Sliced), "호버 때 아래로 늘어나므로 9-slice");
            Assert.That(panel.sprite, Is.EqualTo(Art("BTN_recipe_brief_A")));
            // 1680×320 그림을 210×40에 그대로 맞춘다(모서리·테두리 비율 유지).
            Assert.That(panel.pixelsPerUnitMultiplier, Is.EqualTo(panel.sprite.rect.width / rect.sizeDelta.x));
            Assert.That(brief.GetComponentsInChildren<Image>(true).Any(i => i.sprite == Art("IMG_brief_arrow")), Is.True, "화살표 그림");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void BriefArtIsImportedUncompressedAndPanelsAreSliced()
    {
        foreach (string name in BriefArt)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath + name + ".png");
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), name + " 압축 없음");
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f), name + " PPU");
            Assert.That(Art(name), Is.Not.Null, name + " 스프라이트");
        }
        foreach (string name in new[] { "BTN_recipe_brief_A", "BTN_recipe_brief_B" })
        {
            Vector4 border = Art(name).border;
            Assert.That(Mathf.Min(border.x, border.y, border.z, border.w), Is.GreaterThan(0f), name + " 9-slice");
        }
        Assert.That(Art("IMG_brief_check").rect.size, Is.EqualTo(Art("IMG_brief_check_box_A").rect.size),
            "체크 표시는 체크 칸과 같은 캔버스라 그대로 겹친다");
    }

    [Test]
    public void BriefRowPrefabIsDecorativeOnly()
    {
        var row = AssetDatabase.LoadAssetAtPath<GameObject>(RowPath);
        Assert.That(row.GetComponent<CraftPrepRecipeBriefRow>(), Is.Not.Null);
        foreach (var graphic in row.GetComponentsInChildren<Graphic>(true))
        {
            Assert.That(graphic.raycastTarget, Is.False, graphic.name + " Raycast Target");
            Assert.That(graphic is Image || graphic is TMP_Text, Is.True, graphic.name + "은 리소스 그림이나 글자여야 한다(코드 도형 금지)");
            if (graphic is Image image) Assert.That(image.color, Is.EqualTo(Color.white), image.name + " 색은 그림에 들어 있다");
        }
        var sprites = row.GetComponentsInChildren<Image>(true).Select(i => i.sprite).ToArray();
        Assert.That(sprites, Is.EquivalentTo(new[] { Art("IMG_brief_check_box_A"), Art("IMG_brief_check") }), "체크 칸 하나 + 체크 표시 하나");
        foreach (var text in row.GetComponentsInChildren<TMP_Text>(true))
        {
            Assert.That(text.richText || text.enableAutoSizing, Is.False, text.name);
            Assert.That(text.fontSize, Is.InRange(9f, 12f), text.name + " 글자 크기(AGENTS.md 9~12)");
        }
    }
}
