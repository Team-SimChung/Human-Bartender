using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class CutSceneLayoutTool : EditorWindow
{
    // ── 참조 ──────────────────────────────────────────────────────────
    private CutSceneTimelineManager manager;

    // Preview state belongs to this window. Restore the original scene objects on close/target change.
    sealed class PreviewState
    {
        public Image Image;
        public Sprite Sprite;
        public Color Color;
        public bool Active;
        public Vector3 Position, Scale;
        public Vector2 Size, Pivot, Min, Max;
        public Quaternion Rotation;
        public PreviewState(Image image)
        {
            Image=image;Sprite=image.sprite;Color=image.color;Active=image.gameObject.activeSelf;
            var r=image.rectTransform;Position=r.anchoredPosition3D;Scale=r.localScale;Rotation=r.localRotation;
            Size=r.sizeDelta;Pivot=r.pivot;Min=r.anchorMin;Max=r.anchorMax;
        }
        public void Restore()
        {
            if (Image==null) return;
            Image.sprite=Sprite;Image.color=Color;
            var r=Image.rectTransform;r.anchorMin=Min;r.anchorMax=Max;r.pivot=Pivot;r.sizeDelta=Size;
            r.anchoredPosition3D=Position;r.localScale=Scale;r.localRotation=Rotation;
            Image.gameObject.SetActive(Active);
        }
    }
    readonly Dictionary<Image,PreviewState> previews = new();
    void Capture(Image image)
    {
        if (image!=null && !previews.ContainsKey(image)) previews.Add(image,new PreviewState(image));
    }
    void Restore(Image image)
    {
        if (image!=null && previews.TryGetValue(image,out var state)) { state.Restore();previews.Remove(image); }
    }
    void RestoreAll()
    {
        foreach(var state in previews.Values) state.Restore();
        previews.Clear();slots.Clear();usedIndices.Clear();
    }
    void SetManager(CutSceneTimelineManager next)
    {
        if (manager==next) return;
        RestoreAll();manager=next;
    }

    // ── Anchor 매핑 ───────────────────────────────────────────────────
    static readonly Dictionary<AnchorType, Vector2> AnchorMap = new()
    {
        { AnchorType.Center,      new Vector2(0.5f, 0.5f) },
        { AnchorType.Left,        new Vector2(0.0f, 0.5f) },
        { AnchorType.Right,       new Vector2(1.0f, 0.5f) },
        { AnchorType.TopLeft,     new Vector2(0.0f, 1.0f) },
        { AnchorType.TopRight,    new Vector2(1.0f, 1.0f) },
        { AnchorType.BottomLeft,  new Vector2(0.0f, 0.0f) },
        { AnchorType.BottomRight, new Vector2(1.0f, 0.0f) },
    };

    // ── 슬롯 ──────────────────────────────────────────────────────────
    [System.Serializable]
    private class LayoutSlot
    {
        public string label = "이미지";
        public string path  = "";
        public bool   isBG;

        // 사용 중인 images 리스트 인덱스 (-1 = 미할당)
        public int imageIndex = -1;

        // 설정
        public AnchorType anchor      = AnchorType.Center;
        public AnchorType prevAnchor   = AnchorType.Center;
        public float      bgScale     = 1.0f;
        public Vector2    bgPivot     = new Vector2(0.5f, 0.5f);

        // 계산 결과
        public Vector2 offset;

        // 참조 (인덱스로 매번 가져옴)
        public Image GetImage(CutSceneTimelineManager mgr)
        {
            if (isBG) return mgr.BgImage;
            return mgr.GetImageByIndex(imageIndex);
        }

        public bool IsLoaded(CutSceneTimelineManager mgr)
        {
            var img = GetImage(mgr);
            return img != null && img.gameObject.activeSelf && img.sprite != null;
        }
    }

    private List<LayoutSlot> slots = new();
    private HashSet<int> usedIndices = new();
    private Vector2 scrollPos;

    // ── 스타일 ────────────────────────────────────────────────────────
    private bool stylesInit;
    private GUIStyle styleHeader;
    private GUIStyle styleSlotBox;
    private GUIStyle styleCopied;

    static readonly Color ColDivider = new(0.15f, 0.15f, 0.15f);
    static readonly Color ColCopied  = new(0.3f, 0.7f, 0.3f);

    private string copiedMessage = "";
    private double copiedTime;

    [MenuItem("Tools/CutScene Layout Tool")]
    public static void Open()
    {
        var w = GetWindow<CutSceneLayoutTool>("CutScene Layout");
        w.minSize = new Vector2(420, 500);
    }

    void InitStyles()
    {
        if (stylesInit) return;

        styleHeader = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };

        styleSlotBox = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 8, 8),
            margin  = new RectOffset(0, 0, 4, 4)
        };

        styleCopied = new GUIStyle(EditorStyles.helpBox)
        {
            normal    = { background = MakeTex(ColCopied), textColor = Color.white },
            alignment = TextAnchor.MiddleCenter,
            fontSize  = 11
        };

        stylesInit = true;
    }

    Texture2D MakeTex(Color col)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, col);
        t.Apply();
        return t;
    }

    // ══════════════════════════════════════════════════════════════════
    //  GUI
    // ══════════════════════════════════════════════════════════════════

    void OnGUI()
    {
        InitStyles();
        DrawToolbar();
        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("레이아웃 미리보기는 Edit Mode에서 사용합니다.", MessageType.Info);
            return;
        }
        EditorGUILayout.HelpBox("미리보기 변경은 창을 닫거나 대상을 바꾸면 복원됩니다. 필요한 Offset을 복사하세요.", MessageType.Info);

        if (manager == null)
        {
            EditorGUILayout.HelpBox("CutSceneTimelineManager를 할당해주세요.", MessageType.Info);
            return;
        }

        // 복사 메시지
        if (!string.IsNullOrEmpty(copiedMessage) && EditorApplication.timeSinceStartup - copiedTime < 2.0)
            GUILayout.Label(copiedMessage, styleCopied, GUILayout.Height(24));

        // 사용 가능 이미지 수 표시
        int available = manager.ImageCount - usedIndices.Count;
        EditorGUILayout.HelpBox($"이미지 슬롯: {usedIndices.Count} / {manager.ImageCount} 사용 중", MessageType.None);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        for (int i = 0; i < slots.Count; i++)
            DrawSlot(i);

        EditorGUILayout.EndScrollView();

        DrawBottomButtons(available);
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Manager", EditorStyles.toolbarButton, GUILayout.Width(60));
            SetManager((CutSceneTimelineManager)EditorGUILayout.ObjectField(
                manager, typeof(CutSceneTimelineManager), true, GUILayout.Width(200)));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("씬에서 찾기", EditorStyles.toolbarButton, GUILayout.Width(80)))
                SetManager(FindFirstObjectByType<CutSceneTimelineManager>());
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  슬롯 그리기
    // ══════════════════════════════════════════════════════════════════

    void DrawSlot(int index)
    {
        var slot = slots[index];
        var bgColor = GUI.backgroundColor;
        GUI.backgroundColor = slot.isBG ? new Color(0.4f, 0.5f, 0.7f) : Color.white;

        using (new EditorGUILayout.VerticalScope(styleSlotBox))
        {
            // 헤더
            using (new EditorGUILayout.HorizontalScope())
            {
                slot.label = EditorGUILayout.TextField(slot.label, GUILayout.Width(100));
                bool nextBG = GUILayout.Toggle(slot.isBG, "BG", EditorStyles.toolbarButton, GUILayout.Width(35));
                if (nextBG != slot.isBG) { CleanupSlot(slot); slot.isBG = nextBG; }
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                {
                    CleanupSlot(slot);
                    slots.RemoveAt(index);
                    return;
                }
            }

            // 경로 + 로드/제거
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("경로", GUILayout.Width(30));
                slot.path = EditorGUILayout.TextField(slot.path);

                if (GUILayout.Button("로드", EditorStyles.miniButton, GUILayout.Width(40)))
                    LoadSlotImage(slot);

                if (GUILayout.Button("제거", EditorStyles.miniButton, GUILayout.Width(40)))
                    CleanupSlot(slot);
            }

            // 로드된 상태
            if (slot.IsLoaded(manager))
            {
                Image img = slot.GetImage(manager);
                RectTransform rect = img.GetComponent<RectTransform>();

                DrawHorizontalLine();

                // ── Anchor (BG가 아닐 때) ─────────────────────────────
                if (!slot.isBG)
                {
                    AnchorType newAnchor = (AnchorType)EditorGUILayout.EnumPopup("Anchor", slot.anchor);

                    if (newAnchor != slot.anchor)
                    {
                        ApplyAnchorChange(rect, slot.anchor, newAnchor);
                        slot.prevAnchor = slot.anchor;
                        slot.anchor = newAnchor;
                    }
                }

                // 현재 위치
                Vector2 newPos = EditorGUILayout.Vector2Field("Position (px)", rect.anchoredPosition);
                if (newPos != rect.anchoredPosition)
                {
                    Undo.RecordObject(rect, "CutScene Layout Position");
                    rect.anchoredPosition = newPos;
                }

                // Offset 계산
                CalculateOffset(slot, rect);

                GUI.enabled = false;
                EditorGUILayout.Vector2Field("Offset (비율)", slot.offset);
                GUI.enabled = true;

                // BG 전용
                if (slot.isBG)
                {
                    DrawHorizontalLine();

                    float newScale = EditorGUILayout.FloatField("BG Scale", slot.bgScale);
                    if (newScale != slot.bgScale)
                    {
                        Undo.RecordObject(rect, "CutScene Layout BG Scale");
                        slot.bgScale = newScale;
                        rect.localScale = Vector3.one * slot.bgScale;
                    }

                    Vector2 newPivot = EditorGUILayout.Vector2Field("BG Pivot", slot.bgPivot);
                    if (newPivot != slot.bgPivot)
                    {
                        Undo.RecordObject(rect, "CutScene Layout BG Pivot");
                        SetPivotWithCompensation(rect, newPivot);
                        slot.bgPivot = newPivot;
                    }
                }

                // 버튼
                DrawHorizontalLine();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Offset 복사", EditorStyles.miniButton))
                        CopySlotOffset(slot);

                    if (GUILayout.Button("씬 포커스", EditorStyles.miniButton, GUILayout.Width(70)))
                    {
                        Selection.activeGameObject = img.gameObject;
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }

                    if (GUILayout.Button("위치 초기화", EditorStyles.miniButton, GUILayout.Width(70)))
                    {
                        Undo.RecordObject(rect, "CutScene Layout Reset Pos");
                        rect.anchoredPosition = Vector2.zero;
                    }
                }
            }
        }

        GUI.backgroundColor = bgColor;
    }

    // ══════════════════════════════════════════════════════════════════
    //  Anchor 변경 — 위치 유지하면서 앵커/피벗 교체
    // ══════════════════════════════════════════════════════════════════

    void ApplyAnchorChange(RectTransform rect, AnchorType oldAnchor, AnchorType newAnchor)
    {
        Undo.RecordObject(rect, "CutScene Layout Anchor Change");

        Vector2 oldAnchorVec = AnchorMap[oldAnchor];
        Vector2 newAnchorVec = AnchorMap[newAnchor];

        RectTransform parentRect = manager.CanvasRect;
        float parentW = parentRect.rect.width;
        float parentH = parentRect.rect.height;

        // 현재 위치를 부모 기준 절대 좌표로 변환
        Vector2 absolutePos = new Vector2(
            oldAnchorVec.x * parentW + rect.anchoredPosition.x,
            oldAnchorVec.y * parentH + rect.anchoredPosition.y
        );

        // 새 앵커 기준으로 anchoredPosition 재계산
        Vector2 newAnchoredPos = new Vector2(
            absolutePos.x - newAnchorVec.x * parentW,
            absolutePos.y - newAnchorVec.y * parentH
        );

        rect.anchorMin = newAnchorVec;
        rect.anchorMax = newAnchorVec;
        rect.pivot     = newAnchorVec;
        rect.anchoredPosition = newAnchoredPos;
    }

    void SetPivotWithCompensation(RectTransform rect, Vector2 newPivot)
    {
        Vector2 pivotDelta = newPivot - rect.pivot;
        Vector2 size = rect.rect.size;
        rect.pivot = newPivot;
        rect.anchoredPosition += new Vector2(pivotDelta.x * size.x, pivotDelta.y * size.y);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Offset 계산 (앵커 기준)
    // ══════════════════════════════════════════════════════════════════

    void CalculateOffset(LayoutSlot slot, RectTransform rect)
    {
        RectTransform canvasRect = manager.CanvasRect;
        float w = canvasRect.rect.width;
        float h = canvasRect.rect.height;

        if (w == 0 || h == 0) return;

        slot.offset = new Vector2(
            rect.anchoredPosition.x / w,
            rect.anchoredPosition.y / h
        );
    }


    void LoadSlotImage(LayoutSlot slot)
    {
        if (manager == null) return;

        CleanupSlot(slot);

        Sprite sprite = Resources.Load<Sprite>($"Cutscenes/{slot.path}");
        if (sprite == null)
        {
            Debug.LogWarning($"[LayoutTool] 스프라이트를 찾을 수 없음: Cutscenes/{slot.path}");
            return;
        }

        Image img;

        if (slot.isBG)
        {
            img = manager.BgImage;
            if (img == null) { Debug.LogWarning("[LayoutTool] BgImage가 할당되지 않았습니다."); return; }
        }
        else
        {
            // 사용 가능한 인덱스 찾기
            int idx = FindAvailableIndex();
            if (idx < 0) { Debug.LogWarning("[LayoutTool] 사용 가능한 이미지 슬롯이 없습니다."); return; }

            slot.imageIndex = idx;
            usedIndices.Add(idx);
            img = manager.GetImageByIndex(idx);
        }

        if (img == null) return;
        Capture(img);
        Undo.RecordObject(img.gameObject, "CutScene Layout Load");
        Undo.RecordObject(img, "CutScene Layout Load");

        img.sprite = sprite;
        img.SetNativeSize();
        img.color  = Color.white;
        img.gameObject.SetActive(true);

        RectTransform rect = img.GetComponent<RectTransform>();
        Undo.RecordObject(rect, "CutScene Layout Load");
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;

        // 기본 앵커 적용
        if (!slot.isBG)
        {
            Vector2 anchorVec = AnchorMap[slot.anchor];
            rect.anchorMin = anchorVec;
            rect.anchorMax = anchorVec;
            rect.pivot     = anchorVec;
        }

        Selection.activeGameObject = img.gameObject;
    }

    int FindAvailableIndex()
    {
        for (int i = 0; i < manager.ImageCount; i++)
        {
            if (!usedIndices.Contains(i))
                return i;
        }
        return -1;
    }

    // ══════════════════════════════════════════════════════════════════
    //  복사
    // ══════════════════════════════════════════════════════════════════

    void CopySlotOffset(LayoutSlot slot)
    {
        string text;

        if (slot.isBG)
        {
            text = $"bgOffset: ({slot.offset.x:F3}, {slot.offset.y:F3})\n" +
                   $"bgScale: {slot.bgScale:F2}\n" +
                   $"bgPivot: ({slot.bgPivot.x:F2}, {slot.bgPivot.y:F2})";
        }
        else
        {
            text = $"anchor: {slot.anchor}\n" +
                   $"offsetX: {slot.offset.x:F3}\n" +
                   $"offsetY: {slot.offset.y:F3}";
        }

        EditorGUIUtility.systemCopyBuffer = text;
        copiedMessage = $"'{slot.label}' offset 복사됨";
        copiedTime = EditorApplication.timeSinceStartup;
    }

    void CopyAllOffsets()
    {
        var sb = new System.Text.StringBuilder();

        foreach (var slot in slots)
        {
            if (!slot.IsLoaded(manager)) continue;

            Image img = slot.GetImage(manager);
            CalculateOffset(slot, img.GetComponent<RectTransform>());

            sb.AppendLine($"── {slot.label} ──");

            if (slot.isBG)
            {
                sb.AppendLine($"  bgPath: {slot.path}");
                sb.AppendLine($"  bgOffset: ({slot.offset.x:F3}, {slot.offset.y:F3})");
                sb.AppendLine($"  bgScale: {slot.bgScale:F2}");
                sb.AppendLine($"  bgPivot: ({slot.bgPivot.x:F2}, {slot.bgPivot.y:F2})");
            }
            else
            {
                sb.AppendLine($"  imagePath: {slot.path}");
                sb.AppendLine($"  anchor: {slot.anchor}");
                sb.AppendLine($"  offsetX: {slot.offset.x:F3}");
                sb.AppendLine($"  offsetY: {slot.offset.y:F3}");
            }
            sb.AppendLine();
        }

        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        int loadedCount = slots.FindAll(s => s.IsLoaded(manager)).Count;
        copiedMessage = $"전체 {loadedCount}개 슬롯 offset 복사됨";
        copiedTime = EditorApplication.timeSinceStartup;
    }

    // ══════════════════════════════════════════════════════════════════
    //  정리
    // ══════════════════════════════════════════════════════════════════

    void CleanupSlot(LayoutSlot slot)
    {
        if (manager != null) Restore(slot.GetImage(manager));
        if (slot.imageIndex >= 0) usedIndices.Remove(slot.imageIndex);
        slot.imageIndex = -1;
    }

    void ClearAll() => RestoreAll();

    void OnDestroy()
    {
        if (styleCopied?.normal.background != null) DestroyImmediate(styleCopied.normal.background);
    }

    // ══════════════════════════════════════════════════════════════════
    //  하단 버튼
    // ══════════════════════════════════════════════════════════════════

    void DrawBottomButtons(int available)
    {
        EditorGUILayout.Space(8);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = available > 0;
            if (GUILayout.Button($"+ 이미지 슬롯 ({available}개 남음)", GUILayout.Height(30)))
                slots.Add(new LayoutSlot { label = $"이미지_{slots.Count}" });
            GUI.enabled = true;

            if (GUILayout.Button("+ BG 슬롯", GUILayout.Height(30)))
                slots.Add(new LayoutSlot { label = $"BG_{slots.Count}", isBG = true });
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("전체 Offset 복사", GUILayout.Height(28)))
                CopyAllOffsets();

            GUI.color = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("전체 정리", GUILayout.Height(28), GUILayout.Width(80)))
                ClearAll();
            GUI.color = Color.white;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  씬 뷰 가이드
    // ══════════════════════════════════════════════════════════════════

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        RestoreAll();
    }

    void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) RestoreAll();
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (manager == null) return;

        foreach (var slot in slots)
        {
            if (!slot.IsLoaded(manager)) continue;

            Image img = slot.GetImage(manager);
            RectTransform rect = img.GetComponent<RectTransform>();
            Vector3 worldPos = rect.position;

            CalculateOffset(slot, rect);

            // 라벨 + 십자선
            Color guideColor = slot.isBG ? Color.cyan : Color.green;
            Handles.color = guideColor;

            string info = slot.isBG
                ? $"{slot.label}\noffset: ({slot.offset.x:F2}, {slot.offset.y:F2})\nscale: {slot.bgScale:F1}"
                : $"{slot.label} [{slot.anchor}]\noffset: ({slot.offset.x:F2}, {slot.offset.y:F2})";

            Handles.Label(worldPos + Vector3.up * 30f, info,
                new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = guideColor }
                });

            float crossSize = 20f;
            Handles.DrawLine(worldPos + Vector3.left * crossSize, worldPos + Vector3.right * crossSize);
            Handles.DrawLine(worldPos + Vector3.up * crossSize,   worldPos + Vector3.down * crossSize);
        }

        Repaint();
    }

    // ── 유틸 ──────────────────────────────────────────────────────────
    void DrawHorizontalLine()
    {
        GUILayout.Space(3);
        var r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, ColDivider);
        GUILayout.Space(3);
    }
}
