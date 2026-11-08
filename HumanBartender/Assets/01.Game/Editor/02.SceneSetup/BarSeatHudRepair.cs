using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>예전 HUD가 남은 씬의 연결만 복구한다. 씬을 다시 만들거나 자동 저장하지 않는다.</summary>
public static class BarSeatHudRepair
{
    const string ArtPath = "Assets/01.Game/03.Content/01.ArtResources/05.UI/03.Bar/SeatHud/";
    const string UndoLabel = "Repair Bar Seat HUD";
    static readonly string[] Seats = { "Left", "Middle", "Right" };
    static readonly ESlotType[] SeatTypes = { ESlotType.Left, ESlotType.Middle, ESlotType.Right };

    [MenuItem("Tools/Human Bartender/UI/Repair Bar Seat HUD")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[BarSeatHudRepair] Play 모드를 끄고 실행해 주세요.");
            return;
        }
        var scene = SceneManager.GetActiveScene();
        var hud = Find<BarSeatStatusView>(scene);
        if (hud == null)
        {
            Debug.LogWarning("[BarSeatHudRepair] 활성 씬에 BarSeatStatusView가 없습니다. Play 씬을 열어 주세요.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoLabel);
        try
        {
            Repair(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[BarSeatHudRepair] 좌석 HUD 연결을 복구했습니다. Inspector와 화면을 확인한 뒤 씬을 저장해 주세요.");
        }
        catch (Exception error)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(error);
        }
        finally { Undo.CollapseUndoOperations(group); }
    }

    public static void Repair(BarSeatStatusView hud)
    {
        if (hud == null) throw new ArgumentNullException(nameof(hud));
        // 필요한 원본을 먼저 확인한다. 누락된 아트를 임의로 생성하지 않는다.
        var fill = Sprite("SeatHud_FrameFill");
        var border = Sprite("SeatHud_FrameBorder");
        var dot = Sprite("SeatHud_Dot");
        var ring = Sprite("SeatHud_Ring");
        var indicators = hud.Indicators;
        if (indicators == null) throw new InvalidOperationException("[BarSeatHudRepair] Indicators 참조가 없습니다.");
        var serialized = new SerializedObject(hud);
        var dots = new Image[3];
        var rings = new Image[3];
        // 예전 필드의 타입은 StoryTutorialShape였다. Image[]로 바뀌면서 유효하지 않은 참조가 남았다.
        // 현재 유효한 연결은 우선하고, 이전 이름은 마이그레이션할 때만 사용한다.
        for (int i = 0; i < Seats.Length; i++)
        {
            var oldDot = ReferencedOrNamed(serialized, "dots", i, indicators, "Seat " + Seats[i]);
            var oldRing = ReferencedOrNamed(serialized, "rings", i, indicators, "Selected Seat " + Seats[i]);
            if (oldDot == null || oldRing == null)
                throw new InvalidOperationException("[BarSeatHudRepair] " + Seats[i] + " 좌석 점 또는 링을 찾지 못했습니다.");
            dots[i] = ConvertGraphic(oldDot, dot, false);
            rings[i] = ConvertGraphic(oldRing, ring, false);
        }
        ConvertGraphic(indicators, fill, true);
        var frame = indicators.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == "HUD Border" || t.name == "SeatFrameBorder");
        if (frame != null)
        {
            ConvertGraphic(frame, border, true);
            RenameLegacy(frame.gameObject, "HUD Border", "SeatFrameBorder");
        }
        RenameLegacy(hud.gameObject, "Bar Seat HUD", "BarSeatHudRoot");
        RenameLegacy(indicators.gameObject, "Seat Indicators", "SeatIndicators");
        // 새 줄을 만드는 경우에만 표준 배치를 적용한다. 이미 편집된 Seats의 간격/크기는 보존한다.
        var row = indicators.Find("Seats") as RectTransform;
        if (row == null)
        {
            row = Rect("Seats", indicators);
            row.anchorMin = Vector2.zero;
            row.anchorMax = Vector2.one;
            row.sizeDelta = Vector2.zero;
            var layout = Undo.AddComponent<HorizontalLayoutGroup>(row.gameObject);
            layout.spacing = 2;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childScaleWidth = layout.childScaleHeight = false;
            for (int i = 0; i < Seats.Length; i++)
            {
                var cell = Rect("Seat" + Seats[i], row);
                cell.sizeDelta = new Vector2(22, 22);
                MoveToCell(rings[i].rectTransform, cell, "Seat" + Seats[i] + "Ring");
                MoveToCell(dots[i].rectTransform, cell, "Seat" + Seats[i] + "Dot");
            }
        }
        var presenter = hud.GetComponent<BarSeatHudPresenter>() ?? Undo.AddComponent<BarSeatHudPresenter>(hud.gameObject);
        SetImages(hud, "dots", dots);
        SetImages(hud, "rings", rings);
        Connect(hud, "leftKeyLabel", hud.LeftKey != null ? hud.LeftKey.GetComponentInChildren<TMP_Text>(true) : null);
        Connect(hud, "rightKeyLabel", hud.RightKey != null ? hud.RightKey.GetComponentInChildren<TMP_Text>(true) : null);
        var scene = hud.gameObject.scene;
        Connect(presenter, "view", hud);
        Connect(presenter, "phases", Find<PlayPhaseController>(scene));
        Connect(presenter, "characters", Find<DialogueCharacterManager>(scene));
        Connect(presenter, "guests", Find<GuestManager>(scene));
        Connect(presenter, "craft", Find<CraftFlowController>(scene));
        Connect(presenter, "preparation", Find<CraftPrepStageHost>(scene));
        Connect(presenter, "recipes", Find<RecipeBrowserScreen>(scene));
        Connect(presenter, "playCamera", Find<PlayCamera>(scene));
        Connect(presenter, "servicePanel", Find<ServicePanelController>(scene));
        var owner = new SerializedObject(presenter);
        var slots = owner.FindProperty("guestSlots");
        if (slots.arraySize < 3) slots.arraySize = 3;
        var guests = All<GuestSlot>(scene);
        for (int i = 0; i < 3; i++)
            if (slots.GetArrayElementAtIndex(i).objectReferenceValue == null)
                slots.GetArrayElementAtIndex(i).objectReferenceValue = guests.FirstOrDefault(s => s.SlotType == SeatTypes[i]);
        owner.ApplyModifiedProperties();
        EditorUtility.SetDirty(hud);
        EditorUtility.SetDirty(presenter);
    }

    static Transform ReferencedOrNamed(SerializedObject owner, string field, int index, Transform root, string legacyName)
    {
        var array = owner.FindProperty(field);
        if (index < array.arraySize && array.GetArrayElementAtIndex(index).objectReferenceValue is Component component)
            return component.transform;
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == legacyName);
    }

    static Image ConvertGraphic(Transform target, Sprite sprite, bool sliced)
    {
        var image = target.GetComponent<Image>();
        var legacy = target.GetComponent<StoryTutorialShape>();
        if (image == null)
        {
            var tint = legacy != null ? legacy.color : Color.white;
            if (legacy != null) Undo.DestroyObjectImmediate(legacy);
            image = Undo.AddComponent<Image>(target.gameObject);
            image.color = tint;
            image.raycastTarget = false;
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        }
        else if (image.sprite == null)
        {
            Undo.RecordObject(image, UndoLabel);
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        }
        return image;
    }

    static void SetImages(Object target, string name, Image[] images)
    {
        var owner = new SerializedObject(target);
        var field = owner.FindProperty(name);
        field.arraySize = images.Length;
        for (int i = 0; i < images.Length; i++) field.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
        owner.ApplyModifiedProperties();
    }

    static void Connect(Object target, string name, Object value)
    {
        var owner = new SerializedObject(target);
        var field = owner.FindProperty(name);
        if (field.objectReferenceValue == null && value != null)
        {
            field.objectReferenceValue = value;
            owner.ApplyModifiedProperties();
        }
        if (field.objectReferenceValue == null) Debug.LogWarning("[BarSeatHudRepair] 연결할 수 없는 참조: " + name, target);
    }

    static Sprite Sprite(string name) => AssetDatabase.LoadAllAssetsAtPath(ArtPath + name + ".png").OfType<Sprite>().FirstOrDefault()
        ?? throw new InvalidOperationException("[BarSeatHudRepair] 스프라이트가 없습니다: " + name);

    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);
        go.layer = parent.gameObject.layer;
        Undo.SetTransformParent(go.transform, parent, UndoLabel);
        var rect = (RectTransform)go.transform;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition3D = Vector3.zero;
        return rect;
    }

    static void MoveToCell(RectTransform rect, RectTransform cell, string name)
    {
        Undo.SetTransformParent(rect, cell, UndoLabel);
        Undo.RecordObject(rect, UndoLabel);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition3D = Vector3.zero;
        Undo.RecordObject(rect.gameObject, UndoLabel);
        rect.name = name;
    }

    static void RenameLegacy(GameObject go, string oldName, string newName)
    {
        if (go.name != oldName) return;
        Undo.RecordObject(go, UndoLabel);
        go.name = newName;
    }

    static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).ToArray();
    static T Find<T>(Scene scene) where T : Component => All<T>(scene).FirstOrDefault();
}
