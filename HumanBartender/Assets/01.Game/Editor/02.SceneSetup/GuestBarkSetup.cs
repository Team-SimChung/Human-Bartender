using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 영업(1부) 손님 말풍선을 좌석의 월드 캔버스("… Guest Bark")에서 화면 캔버스(Canvas_Dialogue/GuestBarkRoot)로 옮긴다.
/// 사용자가 Play 씬에서 실행하고 화면을 확인한 뒤 저장한다. 두 번 실행해도 안전하다 — 이미 만든 것과 사람이 손본 값·연결은 보존한다.
/// 에셋은 만들지 않는다(말풍선 프리팹과 UI 문구 캐시는 있는 것을 쓴다).
/// </summary>
public static class GuestBarkSetup
{
    public const string ScenePath = "Assets/01.Game/01.Scenes/Play.unity";
    public const string BubblePrefabPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/02.Dialogue/PlayDialogueTextBox.prefab";
    public const string UiStringsPath = "Assets/01.Game/04.Data/02.RuntimeCaches/NewUIStringDataSO.asset";
    public const string DialogueCanvasName = "Canvas_Dialogue";
    public const string RootName = "GuestBarkRoot";
    public const string AnchorName = "BarkAnchor";
    public const string VisualRootName = "Guest Visual Root";
    /// <summary>옛 월드 말풍선 캔버스가 있던 자리(Guest Visual Root 기준). 옛 캔버스가 없을 때만 쓴다.</summary>
    public static readonly Vector3 DefaultAnchorPosition = new(0, .8f, 0);
    public static readonly ESlotType[] SeatOrder = { ESlotType.Left, ESlotType.Middle, ESlotType.Right };
    const string UndoLabel = "Setup Guest Bark Bubbles";
    const string Tag = "[GuestBarkSetup] ";

    public static string BubbleName(ESlotType seat) => seat + "GuestBark";
    public static string OldCanvasName(ESlotType seat) => seat + " Guest Bark";

    [MenuItem("Tools/Human Bartender/UI/Setup Guest Bark Bubbles")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(Tag + "Play 모드를 끄고 실행해 주세요.");
            return;
        }
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogWarning(Tag + "Play.unity를 활성 씬으로 열어 주세요.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoLabel);
        try
        {
            Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(Tag + "손님 말풍선을 Canvas_Dialogue/GuestBarkRoot로 옮겼습니다. Hierarchy와 Inspector를 확인한 뒤 씬을 저장해 주세요.");
        }
        catch (Exception error)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(error);
        }
        finally { Undo.CollapseUndoOperations(group); }
    }

    /// <summary>메뉴와 테스트가 함께 쓰는 본체. 씬을 저장하거나 dirty로 표시하지 않는다.</summary>
    public static void Apply(Scene scene)
    {
        var dialogueCanvas = All<Canvas>(scene).FirstOrDefault(c => c.name == DialogueCanvasName && c.isRootCanvas);
        if (dialogueCanvas == null) throw new InvalidOperationException(Tag + "05.UI/Canvas_Dialogue를 찾지 못했습니다.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BubblePrefabPath);
        if (prefab == null) throw new InvalidOperationException(Tag + "말풍선 프리팹이 없습니다: " + BubblePrefabPath);
        var slots = All<GuestSlot>(scene).GroupBy(s => s.SlotType).ToDictionary(g => g.Key, g => g.First());
        foreach (var seat in SeatOrder)
            if (!slots.ContainsKey(seat)) throw new InvalidOperationException(Tag + seat + " 좌석의 GuestSlot이 없습니다.");

        var root = EnsureRoot(dialogueCanvas.transform, scene);
        var oldCanvases = new List<Canvas>();
        var pairs = new List<(Transform anchor, RectTransform bubble)>();
        foreach (var seat in SeatOrder)
        {
            var slot = slots[seat];
            var visualRoot = slot.transform.Find(VisualRootName);
            if (visualRoot == null)
            {
                Debug.LogWarning(Tag + slot.name + "에 " + VisualRootName + "가 없어 좌석 오브젝트 아래에 기준점을 둡니다.", slot);
                visualRoot = slot.transform;
            }
            var oldCanvas = visualRoot.Find(OldCanvasName(seat))?.GetComponent<Canvas>();
            if (oldCanvas != null && oldCanvas.renderMode != RenderMode.WorldSpace) oldCanvas = null;
            var anchor = EnsureAnchor(visualRoot, oldCanvas, scene);
            var bubble = EnsureBubble(root, prefab, seat);
            Rewire(slot, bubble, oldCanvas);
            if (oldCanvas != null) oldCanvases.Add(oldCanvas);
            pairs.Add((anchor, (RectTransform)bubble.transform));
        }

        ConnectFollower(dialogueCanvas.gameObject, root, pairs);
        foreach (var host in All<GimmickRunner>(scene).Cast<Object>().Concat(All<CraftPrepStageHost>(scene)))
            CleanBarCanvases(host, dialogueCanvas, oldCanvases);

        var guests = All<GuestManager>(scene).FirstOrDefault();
        var uiStrings = AssetDatabase.LoadAssetAtPath<NewUIStringDataSO>(UiStringsPath);
        if (uiStrings == null) Debug.LogWarning(Tag + "UI 문구 캐시가 없어 손님 이름 줄을 연결하지 못했습니다: " + UiStringsPath);
        if (guests != null) Connect(guests, "uiStringData", uiStrings);
        else Debug.LogWarning(Tag + "GuestManager가 없어 손님 이름 줄의 UI 문구를 연결하지 못했습니다.");

        foreach (var oldCanvas in oldCanvases)
        {
            if (slots.Values.Any(slot => References(slot, oldCanvas.transform)))
            {
                Debug.LogWarning(Tag + oldCanvas.name + "을 아직 GuestSlot이 가리켜 지우지 않았습니다. Inspector에서 연결을 확인해 주세요.", oldCanvas);
                continue;
            }
            Undo.DestroyObjectImmediate(oldCanvas.gameObject);
        }
    }

    /// <summary>
    /// Canvas_Dialogue의 첫 자식으로 둔다. Hierarchy에서 아래일수록 위에 그려지므로, 손님 말풍선은 루나 말풍선(Dialogue Boxes)·
    /// 클릭 받이(OnClickimage)·선택지(ChoicePanel)보다 먼저(아래에) 그려진다. 입력은 CanvasGroup으로 받지 않는다.
    /// </summary>
    static RectTransform EnsureRoot(Transform canvas, Scene scene)
    {
        if (canvas.Find(RootName) is RectTransform existing)
        {
            Debug.Log(Tag + "기존 " + RootName + "의 배치와 설정을 보존합니다.");
            return existing;
        }
        var go = Create(RootName, scene, typeof(RectTransform), typeof(CanvasGroup));
        go.layer = LayerMask.NameToLayer("UI");
        var rect = (RectTransform)go.transform;
        rect.SetParent(canvas, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.SetSiblingIndex(0);
        var group = go.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        return rect;
    }

    static Transform EnsureAnchor(Transform visualRoot, Canvas oldCanvas, Scene scene)
    {
        var existing = visualRoot.Find(AnchorName);
        if (existing != null) return existing;
        var anchor = Create(AnchorName, scene).transform;
        anchor.SetParent(visualRoot, false);
        anchor.localPosition = oldCanvas != null ? (Vector3)((RectTransform)oldCanvas.transform).anchoredPosition : DefaultAnchorPosition;
        return anchor;
    }

    /// <summary>
    /// 프리팹 원본 값(루나 말풍선과 같은 크기·테두리·글자·이름 줄)을 그대로 쓴다. 덮어쓰는 것은 이름, 시작 시 꺼짐,
    /// 가운데 꼬리를 위로(말풍선 위의 손님 쪽으로) 향하게 하는 것뿐이다. 위치는 GuestBarkFollower가 실행 중에 정한다.
    /// </summary>
    static GameObject EnsureBubble(RectTransform root, GameObject prefab, ESlotType seat)
    {
        var existing = root.Find(BubbleName(seat));
        if (existing != null) return existing.gameObject;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);
        go.name = BubbleName(seat);
        var bubble = go.GetComponent<DynamicSpeechBubble>();
        if (bubble != null && bubble.centerArrow != null && bubble.centerArrow.TryGetComponent<SpeechBubbleTail>(out var tail))
        {
            var data = new SerializedObject(tail);
            data.FindProperty("pointUp").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        else Debug.LogWarning(Tag + go.name + "의 가운데 꼬리를 찾지 못해 방향을 프리팹 값으로 둡니다.", go);
        go.SetActive(false);
        return go;
    }

    /// <summary>좌석의 말풍선 연결을 새 말풍선으로 바꾼다. 비어 있거나 옛 월드 말풍선을 가리킬 때만 바꾸고, 다른 것을 가리키면 보존한다.</summary>
    static void Rewire(GuestSlot slot, GameObject bubbleObject, Canvas oldCanvas)
    {
        var data = new SerializedObject(slot);
        var root = data.FindProperty("bubbleRoot");
        var speech = data.FindProperty("speechBubble");
        var bubble = bubbleObject.GetComponent<DynamicSpeechBubble>();
        bool changed = false;
        if (root.objectReferenceValue != bubbleObject)
        {
            if (IsReplaceable(root.objectReferenceValue, oldCanvas)) { root.objectReferenceValue = bubbleObject; changed = true; }
            else Debug.LogWarning(Tag + slot.name + "의 Bubble Root가 다른 대상을 가리켜 보존합니다.", slot);
        }
        if (speech.objectReferenceValue != bubble)
        {
            if (IsReplaceable(speech.objectReferenceValue, oldCanvas)) { speech.objectReferenceValue = bubble; changed = true; }
            else Debug.LogWarning(Tag + slot.name + "의 Speech Bubble이 다른 대상을 가리켜 보존합니다.", slot);
        }
        if (!changed) return;
        Undo.RecordObject(slot, UndoLabel);
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(slot);
    }

    static bool IsReplaceable(Object current, Canvas oldCanvas) =>
        current == null || oldCanvas != null && TransformOf(current) is Transform t && t.IsChildOf(oldCanvas.transform);

    static bool References(GuestSlot slot, Transform oldCanvas)
    {
        var data = new SerializedObject(slot);
        return new[] { "bubbleRoot", "speechBubble" }.Any(field =>
            TransformOf(data.FindProperty(field).objectReferenceValue) is Transform t && t.IsChildOf(oldCanvas));
    }

    static Transform TransformOf(Object value) => value switch
    {
        GameObject go => go.transform,
        Component component => component.transform,
        _ => null,
    };

    /// <summary>따라가기 연결. 비어 있는 칸만 채우고, 보정 위치(offset) 등 사람이 손본 값은 건드리지 않는다.</summary>
    static void ConnectFollower(GameObject canvas, RectTransform root, List<(Transform anchor, RectTransform bubble)> pairs)
    {
        var follower = canvas.GetComponent<GuestBarkFollower>();
        if (follower == null) follower = Undo.AddComponent<GuestBarkFollower>(canvas);
        var data = new SerializedObject(follower);
        var layer = data.FindProperty("layer");
        var seats = data.FindProperty("seats");
        bool changed = false;
        if (layer.objectReferenceValue == null) { layer.objectReferenceValue = root; changed = true; }
        if (seats.arraySize < pairs.Count) { seats.arraySize = pairs.Count; changed = true; }
        for (int i = 0; i < pairs.Count; i++)
        {
            var element = seats.GetArrayElementAtIndex(i);
            var anchor = element.FindPropertyRelative("anchor");
            var bubble = element.FindPropertyRelative("bubble");
            if (anchor.objectReferenceValue == null) { anchor.objectReferenceValue = pairs[i].anchor; changed = true; }
            if (bubble.objectReferenceValue == null) { bubble.objectReferenceValue = pairs[i].bubble; changed = true; }
        }
        if (!changed) return;
        Undo.RecordObject(follower, UndoLabel);
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(follower);
    }

    /// <summary>제조 중 숨기는 바 UI 목록에서 옛 월드 말풍선과 빈 칸을 뺀다. 새 말풍선은 Canvas_Dialogue와 함께 숨는다.</summary>
    static void CleanBarCanvases(Object host, Canvas dialogueCanvas, List<Canvas> oldCanvases)
    {
        var data = new SerializedObject(host);
        var list = data.FindProperty("barCanvases");
        var current = Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue as Canvas).ToList();
        var kept = current.Where(c => c != null && !oldCanvases.Contains(c) && !IsOldWorldBark(c)).Distinct().ToList();
        if (!kept.Contains(dialogueCanvas)) kept.Insert(0, dialogueCanvas);
        if (kept.SequenceEqual(current)) return;
        Undo.RecordObject(host, UndoLabel);
        list.arraySize = kept.Count;
        for (int i = 0; i < kept.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = kept[i];
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(host);
    }

    static bool IsOldWorldBark(Canvas canvas) =>
        canvas.renderMode == RenderMode.WorldSpace && SeatOrder.Any(seat => canvas.name == OldCanvasName(seat));

    static void Connect(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        var property = data.FindProperty(field);
        if (property == null || property.objectReferenceValue != null || value == null) return;
        Undo.RecordObject(target, UndoLabel);
        property.objectReferenceValue = value;
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    static GameObject Create(string name, Scene scene, params Type[] components)
    {
        var go = new GameObject(name, components);
        if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);
        return go;
    }

    static IEnumerable<T> All<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
}
