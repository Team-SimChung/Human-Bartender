using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Stage = CraftPrepStageScreen.Stage;

/// <summary>웹 현행 재료의 누락 슬롯만 보충한다. 기존 병의 위치와 그림은 보존한다.</summary>
public static class WebCraftingShelfSetup
{
    public const string PrefabPath = "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/01.Preparation/CraftPrepStage.prefab";
    const string ArtFolder = "Assets/01.Game/03.Content/01.ArtResources/03.Crafting/02.Ingredients";
    const string UndoLabel = "Complete Web Crafting Shelf";

    // 웹 960×540 선반의 기본 배치. 스프라이트 피벗과 클릭 영역의 중심 차이를 반영한다.
    static readonly (string id, string art, Stage stage, Vector2 position, float hitHeight, Vector2 hitOffset)[] Missing =
    {
        ("triple_sec", "02.Shelf/triple_sec.png", Stage.Liquor, new Vector2(-.88f, -.445f), 1.46f, new Vector2(0f, -.115f)),
        ("kahlua", "02.Shelf/kahlua.png", Stage.Liquor, new Vector2(-.29f, -.445f), 1.53f, new Vector2(0f, -.08f)),
        ("amaretto", "02.Shelf/amaretto.png", Stage.Liquor, new Vector2(.30f, -.445f), 1.24f, new Vector2(0f, -.225f)),
        ("wild_dog", "02.Shelf/johnny-wild_dog.png", Stage.Liquor, new Vector2(.89f, 1.705f), 1.50f, new Vector2(0f, -.205f)),
        ("bitters", "02.Shelf/johnny-bitters.png", Stage.Liquor, new Vector2(.89f, -.535f), 1.09f, new Vector2(0f, -.21f)),
        ("sugar_cube", "02.Shelf/johnny-sugar_cube.png", Stage.Liquor, new Vector2(1.48f, -1.01f), .67f, new Vector2(0f, .055f)),
        ("cranberry_juice", "01.Refrigerator/cranberry_juice.png", Stage.Fridge, new Vector2(1.345f, -.215f), 1.66f, new Vector2(.055f, -.275f)),
        ("milk", "01.Refrigerator/milk.png", Stage.Fridge, new Vector2(2.37f, -.225f), 1.62f, new Vector2(-.02f, -.285f)),
    };

    [MenuItem("Tools/Human Bartender/Crafting/Complete Web Crafting Shelf")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[WebCraftingShelfSetup] Play 모드를 끄고 실행해 주세요.");
            return;
        }
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogWarning("[WebCraftingShelfSetup] 편집 중인 프리팹을 저장하고 Prefab 모드를 닫아 주세요.");
            return;
        }
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Complete(root.GetComponent<CraftPrepStageScreen>());
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[WebCraftingShelfSetup] 제조 선반 원본에 누락 재료를 연결했습니다. 기존 병 배치는 보존했습니다.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void Complete(CraftPrepStageScreen screen)
    {
        if (screen == null) throw new InvalidOperationException("[WebCraftingShelfSetup] 제조 준비 화면이 없습니다.");
        var existing = screen.GetComponentsInChildren<CraftPrepShelfSlot>(true).ToList();
        var template = existing.FirstOrDefault(s => s.ItemId == "gin");
        if (template == null || template.Icon == null) throw new InvalidOperationException("[WebCraftingShelfSetup] 복제할 진 슬롯이 없습니다.");
        using var owner = new SerializedObject(screen);
        var anchors = owner.FindProperty("stageAnchors");
        foreach (var entry in Missing)
        {
            if (existing.Any(slot => slot.ItemId == entry.id)) continue;
            var art = AssetDatabase.LoadAllAssetsAtPath(ArtFolder + "/" + entry.art).OfType<Sprite>().SingleOrDefault();
            if (art == null)
            {
                Debug.LogWarning("[WebCraftingShelfSetup] 선반용 스프라이트가 없어 슬롯을 생성하지 않았습니다: " + entry.art);
                continue;
            }
            var anchor = anchors.GetArrayElementAtIndex((int)entry.stage).objectReferenceValue as Transform;
            if (anchor == null) throw new InvalidOperationException("[WebCraftingShelfSetup] 선반 앵커가 없습니다: " + entry.stage);
            var slot = UnityEngine.Object.Instantiate(template, anchor, false);
            slot.name = entry.id + "Slot";
            Undo.RegisterCreatedObjectUndo(slot.gameObject, UndoLabel);
            slot.transform.localPosition = new Vector3(entry.position.x, entry.position.y, template.transform.localPosition.z);
            slot.transform.localScale = Vector3.one;
            slot.gameObject.SetActive(true);
            var renderer = slot.GetComponent<SpriteRenderer>();
            renderer.sprite = art;
            var collider = slot.GetComponent<BoxCollider2D>();
            collider.size = new Vector2(entry.stage == Stage.Fridge ? .93f : .57f, entry.hitHeight);
            collider.offset = entry.hitOffset;
            using var serialized = new SerializedObject(slot);
            serialized.FindProperty("itemId").stringValue = entry.id;
            serialized.FindProperty("stage").enumValueIndex = (int)entry.stage;
            serialized.ApplyModifiedProperties();
            existing.Add(slot);
        }
        // 기존 순서·참조는 유지하고, 저장된 배열에 빠진 슬롯만 추가한다.
        var slots = owner.FindProperty("slots");
        foreach (var slot in existing)
        {
            bool connected = Enumerable.Range(0, slots.arraySize).Any(i => slots.GetArrayElementAtIndex(i).objectReferenceValue == slot);
            if (connected) continue;
            slots.InsertArrayElementAtIndex(slots.arraySize);
            slots.GetArrayElementAtIndex(slots.arraySize - 1).objectReferenceValue = slot;
        }
        owner.ApplyModifiedProperties();
        EditorUtility.SetDirty(screen);
    }
}
