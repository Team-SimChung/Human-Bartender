using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// StoryServeZone 프리팹에 코스터·완성 잔·빈 잔 자리를 만든다. 수치는 바 화면 픽셀(서빙 캔버스 960×540 기준과 1:1) 단위이며,
/// 이미 있는 자식은 옮기지 않으므로 이후 조정은 프리팹에서 직접 한다.
/// </summary>
public static class StoryServeZoneSetup
{
    const string PrefabPath = "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/03.Serving/StoryServeZone.prefab";

    // 자리 전체는 120×140이고, 위에서 124px 아래가 책상 표면 선(코스터 바닥)이다. 피벗을 그 선에 둔다.
    static readonly Vector2 ZoneSize = new(120, 140);
    const float TabletopFromTop = 124;

    // (이름, 자리 왼쪽 위 기준 x·y, 크기)
    static readonly (string name, Vector2 position, Vector2 size)[] Slots =
    {
        ("Coaster", new Vector2(18.5f, 97), new Vector2(83, 27)),
        ("Drink", new Vector2(25.5f, 0), new Vector2(69, 112)),
        ("EmptyGlass", new Vector2(30, 19), new Vector2(60, 93)),
    };

    [MenuItem("Tools/Story/Set Up Serve Zone Layout")]
    public static void SetUp()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = ZoneSize;
            rect.pivot = new Vector2(.5f, (ZoneSize.y - TabletopFromTop) / ZoneSize.y);

            var images = new Image[Slots.Length];
            for (int i = 0; i < Slots.Length; i++)
                images[i] = FindOrCreateSlot(rect, Slots[i].name, Slots[i].position, Slots[i].size);

            var layout = root.GetComponent<StoryServeZoneLayout>() ?? root.AddComponent<StoryServeZoneLayout>();
            using (var serialized = new SerializedObject(layout))
            {
                serialized.FindProperty("coaster").objectReferenceValue = images[0];
                serialized.FindProperty("drink").objectReferenceValue = images[1];
                serialized.FindProperty("emptyGlass").objectReferenceValue = images[2];
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Image FindOrCreateSlot(RectTransform parent, string name, Vector2 topLeft, Vector2 size)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.GetComponent<Image>() ?? throw new InvalidOperationException(name + "에 Image가 없습니다.");
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        go.SetActive(false);
        return image;
    }

    /// <summary>batchmode용: 프리팹 배치를 실행하고 종료 코드로 결과를 알린다.</summary>
    public static void SetUpBatch()
    {
        try { SetUp(); Debug.Log("SERVE_ZONE_SETUP_OK"); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
