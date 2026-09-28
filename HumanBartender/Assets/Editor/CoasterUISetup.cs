// Editor/CoasterUISetup.cs
// 메뉴: Tools > Tycoon > Setup Coaster UI

using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 코스터 드래그앤드롭 UI(우측 하단 코스터 트레이 + 각 GuestSlot 앞 드롭존)를 현재 열려있는 씬에
/// 자동으로 배치한다. 씬을 새로 열거나 저장하지 않는다 — 현재 활성 씬을 직접 수정하므로, 결과를
/// 확인한 뒤 직접 Ctrl+S로 저장해야 한다. 위치/크기는 자리만 잡아둔 placeholder이므로 에디터에서
/// 실제 비주얼에 맞게 다시 조정해야 한다.
/// </summary>
public static class CoasterUISetup
{
    const string TrayCanvasName = "Coaster Tray Canvas";
    const string DropZoneName = "Coaster Drop Zone";
    const string SupplyTrayPrefabPath = "Assets/04.Prefabs/UI/CoasterSupplyTray.prefab";

    [MenuItem("Tools/Tycoon/Setup Coaster UI")]
    public static void Run()
    {
        var guestManager = Object.FindFirstObjectByType<GuestManager>();
        if (guestManager == null)
        {
            Debug.LogError("[CoasterUISetup] 씬에서 GuestManager를 찾지 못했습니다. Play.unity를 열고 다시 실행하세요.");
            return;
        }

        var slots = Object.FindObjectsByType<GuestSlot>(FindObjectsSortMode.InstanceID);
        if (slots.Length == 0)
        {
            Debug.LogError("[CoasterUISetup] 씬에서 GuestSlot을 찾지 못했습니다.");
            return;
        }

        if (GameObject.Find(TrayCanvasName) != null)
        {
            Debug.LogWarning($"[CoasterUISetup] '{TrayCanvasName}'가 이미 있습니다. 중복 생성을 막기 위해 트레이 생성을 건너뜁니다.");
        }
        else
        {
            CreateTray();
        }

        int created = 0;
        foreach (var slot in slots)
        {
            if (slot.transform.Find(DropZoneName) != null)
            {
                Debug.LogWarning($"[CoasterUISetup] {slot.name}에 이미 '{DropZoneName}'가 있어 건너뜁니다.");
                continue;
            }

            CreateDropZone(slot, guestManager);
            created++;
        }

        WireDialogueClickCatcher();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[CoasterUISetup] 완료. 드롭존 {created}개 생성. Ctrl+S로 씬을 저장하세요. " +
                  "위치/크기는 placeholder이니 Scene 뷰에서 실제 좌석 위치에 맞게 조정해주세요.");
    }

    /// <summary>
    /// TycoonFlow.dialogueClickCatcher를 화면 전체를 덮는 대화(2부)용 클릭 캐처("OnClickimage")에 연결한다.
    /// Screen Space Overlay는 World Space UI(코스터 드롭존)보다 항상 레이캐스트 우선순위가 높아서,
    /// 이 오브젝트가 켜져 있으면 타이쿤 국면 동안 코스터를 포함한 월드 스페이스 UI가 아예 드롭을 받지 못한다.
    /// </summary>
    static void WireDialogueClickCatcher()
    {
        var tycoonFlow = Object.FindFirstObjectByType<TycoonFlow>();
        if (tycoonFlow == null)
        {
            Debug.LogWarning("[CoasterUISetup] 씬에서 TycoonFlow를 찾지 못해 dialogueClickCatcher 연결을 건너뜁니다.");
            return;
        }

        var so = new SerializedObject(tycoonFlow);
        var prop = so.FindProperty("dialogueClickCatcher");
        if (prop.objectReferenceValue != null) return; // 이미 연결되어 있으면 건드리지 않음

        var clickCatcher = GameObject.Find("OnClickimage");
        if (clickCatcher == null)
        {
            Debug.LogWarning("[CoasterUISetup] 씬에서 'OnClickimage'를 찾지 못해 dialogueClickCatcher 연결을 건너뜁니다.");
            return;
        }

        prop.objectReferenceValue = clickCatcher;
        so.ApplyModifiedProperties();
        Debug.Log("[CoasterUISetup] TycoonFlow.dialogueClickCatcher를 'OnClickimage'에 연결했습니다.");
    }

    static void CreateTray()
    {
        var trayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SupplyTrayPrefabPath);
        if (trayPrefab == null)
        {
            Debug.LogError($"[CoasterUISetup] 코스터 트레이 프리팹이 없습니다: {SupplyTrayPrefabPath}");
            return;
        }

        var canvasGO = new GameObject(TrayCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);

        var tray = (GameObject)PrefabUtility.InstantiatePrefab(trayPrefab, canvasGO.transform);
        foreach (CoasterDragItem coaster in tray.GetComponentsInChildren<CoasterDragItem>(true))
        {
            SetSerializedField(coaster, "rootCanvas", canvas);
        }
    }

    static void CreateDropZone(GuestSlot slot, GuestManager guestManager)
    {
        var dropGO = new GameObject(DropZoneName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image), typeof(CoasterDropZone));
        dropGO.transform.SetParent(slot.transform, false);

        var canvas = dropGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = FindSlotBubbleCamera(slot);
        canvas.sortingOrder = 5;

        var rect = dropGO.GetComponent<RectTransform>();
        rect.localPosition = Vector3.zero;
        rect.localScale = new Vector3(0.02f, 0.02f, 0.02f); // 기존 Bubble Canvas와 동일한 world-space UI 스케일
        rect.sizeDelta = new Vector2(150, 150);
        rect.anchoredPosition = Vector2.zero;

        var image = dropGO.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f); // 투명 — 레이캐스트 타겟 용도
        image.raycastTarget = true;

        var dropZone = dropGO.GetComponent<CoasterDropZone>();
        SetSerializedField(dropZone, "targetSlot", slot);
        SetSerializedField(dropZone, "guestManager", guestManager);
    }

    /// <summary>GuestSlot의 private bubbleRoot 필드에서 기존 Bubble Canvas가 쓰는 카메라를 그대로 재사용한다.</summary>
    static Camera FindSlotBubbleCamera(GuestSlot slot)
    {
        var field = typeof(GuestSlot).GetField("bubbleRoot", BindingFlags.NonPublic | BindingFlags.Instance);
        var bubbleRoot = field?.GetValue(slot) as GameObject;
        return bubbleRoot != null ? bubbleRoot.GetComponentInChildren<Canvas>()?.worldCamera : null;
    }

    static void SetSerializedField(Object target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(fieldName).objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }
}
