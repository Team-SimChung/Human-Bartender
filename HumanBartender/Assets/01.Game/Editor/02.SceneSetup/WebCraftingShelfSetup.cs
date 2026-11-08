using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>웹 현행 재료의 누락 슬롯만 보충한다. 기존 병의 위치와 그림은 보존한다.</summary>
public static class WebCraftingShelfSetup
{
    public const string PrefabPath = "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/01.Preparation/CraftPrepStage.prefab";
    const string FontPath = "Assets/01.Game/03.Content/05.Fonts/Regular_PFStardust_3_0_SDF.asset";
    const string UndoLabel = "Complete Web Crafting Shelf";

    // 최초 에디터 배치 값이다. 생성 후 프리팹 Inspector에서 조절하며 재실행 시 덮어쓰지 않는다.
    static readonly (string id, string name, CraftPrepStageScreen.Stage stage, Vector2 position)[] Missing =
    {
        ("triple_sec", "트리플섹", CraftPrepStageScreen.Stage.Liquor, new Vector2(-.90f, -.5f)),
        ("kahlua", "칼루아", CraftPrepStageScreen.Stage.Liquor, new Vector2(-.32f, -.5f)),
        ("amaretto", "아마레토", CraftPrepStageScreen.Stage.Liquor, new Vector2(.26f, -.5f)),
        ("wild_dog", "와일드 독", CraftPrepStageScreen.Stage.Liquor, new Vector2(.87f, 1.5f)),
        ("bitters", "비터스", CraftPrepStageScreen.Stage.Liquor, new Vector2(.84f, -.5f)),
        ("sugar_cube", "각설탕", CraftPrepStageScreen.Stage.Liquor, new Vector2(1.42f, -.5f)),
        ("cranberry_juice", "크랜베리주스", CraftPrepStageScreen.Stage.Fridge, new Vector2(-1.42f, 1.5f)),
        ("milk", "우유", CraftPrepStageScreen.Stage.Fridge, new Vector2(-.46f, 1.5f)),
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
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (screen == null || font == null) throw new InvalidOperationException("[WebCraftingShelfSetup] 화면 또는 이름표 폰트가 없습니다.");
        var existing = screen.GetComponentsInChildren<CraftPrepShelfSlot>(true).ToList();
        var template = existing.FirstOrDefault(s => s.ItemId == "gin");
        if (template == null || template.Icon == null) throw new InvalidOperationException("[WebCraftingShelfSetup] 공용 임시 병의 원본 진 슬롯이 없습니다.");
        var owner = new SerializedObject(screen);
        var anchors = owner.FindProperty("stageAnchors");
        foreach (var entry in Missing)
        {
            if (existing.Any(slot => slot.ItemId == entry.id)) continue;
            var anchor = anchors.GetArrayElementAtIndex((int)entry.stage).objectReferenceValue as Transform;
            if (anchor == null) throw new InvalidOperationException("[WebCraftingShelfSetup] 선반 앵커가 없습니다: " + entry.stage);
            // 기존 선반 슬롯의 충돌체·윤곽선 설정을 그대로 재사용한다. 전용 아트가 준비되면 Inspector에서 교체한다.
            var slot = UnityEngine.Object.Instantiate(template, anchor, false);
            slot.name = entry.id + "Slot";
            Undo.RegisterCreatedObjectUndo(slot.gameObject, UndoLabel);
            slot.transform.localPosition = new Vector3(entry.position.x, entry.position.y, template.transform.localPosition.z);
            slot.transform.localScale = Vector3.one;
            slot.gameObject.SetActive(true);
            var labelObject = new GameObject("ItemNameText", typeof(RectTransform), typeof(TextMeshPro));
            Undo.RegisterCreatedObjectUndo(labelObject, UndoLabel);
            labelObject.layer = slot.gameObject.layer;
            labelObject.transform.SetParent(slot.transform, false);
            var label = labelObject.GetComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = .8f;
            label.enableAutoSizing = false;
            label.richText = false;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.Center;
            label.text = entry.name;
            label.color = Color.white;
            label.rectTransform.sizeDelta = new Vector2(.76f, .2f);
            label.rectTransform.localPosition = new Vector3(0, template.Icon.bounds.min.y - .14f, -.05f);
            var sprite = slot.GetComponent<SpriteRenderer>();
            label.renderer.sortingLayerID = sprite.sortingLayerID;
            label.renderer.sortingOrder = sprite.sortingOrder + 1;
            var serialized = new SerializedObject(slot);
            serialized.FindProperty("itemId").stringValue = entry.id;
            serialized.FindProperty("stage").enumValueIndex = (int)entry.stage;
            serialized.FindProperty("itemNameLabel").objectReferenceValue = label;
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
