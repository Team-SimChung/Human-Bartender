using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// #151 일회성 셋업(박용석 P1·미구현 리스트). 적용 후 삭제한다.
/// 1) ProjectLifeScope에 Canvas_Loading(비둘기 + Loading) 추가·연결
/// 2) OutSide에 갱단 통행 벽(GangPassBoundary) 추가·연결
/// 3) OutSide 밀수 거래·계약 연출 값을 웹 배치(npc-placements.json)에 맞춤
/// 두 번 실행해도 안전하다. 이미 있는 오브젝트는 다시 만들지 않고, 비어 있는 연결만 채운다.
/// </summary>
public static class Issue151SceneSetup
{
    const string ProjectScopePrefab = "Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab";
    const string OutsideScene = "Assets/01.Game/01.Scenes/OutSide.unity";
    const string PigeonSheet = "Assets/01.Game/03.Content/01.ArtResources/01.Characters/02.Outside/05.StreetNPC/01.Idle/Pigeon_idle.png";
    const string DialogueFont = "Assets/01.Game/03.Content/05.Fonts/Regular_PFStardust_3_0_SDF.asset";

    // 웹 data/presentation/npc-placements.json (2026-11-18 a4992ad)
    const float TradePartnerX = 3.72f;
    const float ContractPartnerX = -9.34f;
    // 웹 outside-neighborhood.js clampBoundary: 루나 x >= 경비 x + 0.34
    const float GuardClampOffset = .34f;
    // 웹 outside-events.js 계약: 루나 = max(삼호 + 1, 밀수업자 퇴장 끝 + 0.4)
    const float ContractMinimumGap = 1f, ContractExitMargin = .4f;

    [MenuItem("Tools/Issue151/1. Build Loading Canvas")]
    public static void BuildLoadingCanvas() => BuildLoadingCanvasCore();

    [MenuItem("Tools/Issue151/2. Gang Pass Boundary + Contract Staging (OutSide)")]
    public static void ApplyOutside()
    {
        var scene = EditorSceneManager.OpenScene(OutsideScene, OpenSceneMode.Single);
        ApplyGangPassBoundary();
        ApplyContractStaging();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>배치 모드 진입점(-executeMethod Issue151SceneSetup.RunAll).</summary>
    public static void RunAll()
    {
        try
        {
            BuildLoadingCanvasCore();
            ApplyOutside();
            AssetDatabase.SaveAssets();
            Debug.Log("[Issue151] 셋업 완료");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }

    // ── 1. 로딩 화면 ────────────────────────────────────────────────

    static void BuildLoadingCanvasCore()
    {
        var root = PrefabUtility.LoadPrefabContents(ProjectScopePrefab);
        try
        {
            var manager = root.GetComponentInChildren<SceneTransitionManager>(true);
            if (manager == null) throw new InvalidOperationException("[Issue151] ProjectLifeScope에 SceneTransitionManager가 없습니다.");
            var existing = manager.transform.Find("Canvas_Loading");
            SceneLoadingView view = existing != null ? existing.GetComponent<SceneLoadingView>() : CreateLoadingCanvas(manager.transform);
            if (view == null) throw new InvalidOperationException("[Issue151] Canvas_Loading에 SceneLoadingView가 없습니다.");

            var managerObject = new SerializedObject(manager);
            var overlay = managerObject.FindProperty("loadingOverlay");
            if (overlay.objectReferenceValue == null)
            {
                overlay.objectReferenceValue = view;
                managerObject.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, ProjectScopePrefab);
            Debug.Log("[Issue151] Canvas_Loading " + (existing != null ? "연결 확인" : "생성"));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static SceneLoadingView CreateLoadingCanvas(Transform parent)
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        var canvasObject = NewUi("Canvas_Loading", parent, uiLayer);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 바 문 전환(32000) 바로 아래. 둘은 동시에 뜨지 않고, 씬 캔버스(Tutorial 4000 등)보다 위에 있어야 한다.
        canvas.sortingOrder = 31999;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);
        scaler.matchWidthOrHeight = 0;
        canvasObject.AddComponent<GraphicRaycaster>();
        var view = canvasObject.AddComponent<SceneLoadingView>();
        Stretch((RectTransform)canvasObject.transform);

        var loadingRoot = NewUi("LoadingRoot", canvasObject.transform, uiLayer);
        Stretch((RectTransform)loadingRoot.transform);
        var group = loadingRoot.AddComponent<CanvasGroup>();

        var backdrop = NewUi("LoadingBackdrop", loadingRoot.transform, uiLayer);
        Stretch((RectTransform)backdrop.transform);
        var backdropImage = backdrop.AddComponent<Image>();
        backdropImage.color = Color.black;
        backdropImage.raycastTarget = true; // 로딩 중 아래 화면 입력을 막는다.

        var content = NewUi("LoadingGroup", loadingRoot.transform, uiLayer);
        Place((RectTransform)content.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(120, 84));

        // 웹: 각 칸(129×138)에서 (55,101,17,14)만 잘라 4.2배. Unity 960 기준으로 3.15배(4.2 × 0.75).
        var frame = NewUi("PigeonFrame", content.transform, uiLayer);
        Place((RectTransform)frame.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(54, 44));
        frame.AddComponent<RectMask2D>();
        var pigeon = NewUi("PigeonImage", frame.transform, uiLayer);
        Place((RectTransform)pigeon.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(3, 123), new Vector2(406, 435));
        var pigeonImage = pigeon.AddComponent<Image>();
        pigeonImage.raycastTarget = false;

        var row = NewUi("LoadingTextRow", content.transform, uiLayer);
        Place((RectTransform)row.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(120, 28));
        var label = Text("LoadingText", row.transform, uiLayer, TextAlignmentOptions.Right, new Vector2(0, 0), new Vector2(92, 28));
        var dots = Text("LoadingDotsText", row.transform, uiLayer, TextAlignmentOptions.Left, new Vector2(92, 0), new Vector2(28, 28));
        label.text = "Loading";

        var frames = AssetDatabase.LoadAllAssetsAtPath(PigeonSheet).OfType<Sprite>()
            .OrderBy(s => int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out int i) ? i : 999).ToArray();
        if (frames.Length != 12) Debug.LogWarning($"[Issue151] Pigeon_idle 프레임이 12개가 아닙니다: {frames.Length}");
        if (frames.Length > 0) pigeonImage.sprite = frames[0];

        var so = new SerializedObject(view);
        so.FindProperty("loadingRoot").objectReferenceValue = loadingRoot;
        so.FindProperty("rootGroup").objectReferenceValue = group;
        so.FindProperty("pigeonImage").objectReferenceValue = pigeonImage;
        so.FindProperty("loadingLabel").objectReferenceValue = label;
        so.FindProperty("dotsLabel").objectReferenceValue = dots;
        var framesProperty = so.FindProperty("pigeonFrames");
        framesProperty.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        loadingRoot.SetActive(false);
        return view;
    }

    static GameObject NewUi(string name, Transform parent, int layer)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = layer };
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = pivot;
        rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
    }

    static TextMeshProUGUI Text(string name, Transform parent, int layer, TextAlignmentOptions alignment, Vector2 position, Vector2 size)
    {
        var go = NewUi(name, parent, layer);
        Place((RectTransform)go.transform, Vector2.zero, Vector2.zero, position, size);
        var text = go.AddComponent<TextMeshProUGUI>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DialogueFont);
        if (font != null) text.font = font;
        else Debug.LogWarning("[Issue151] PFStardust 폰트를 찾지 못했습니다: " + DialogueFont);
        text.fontSize = 20; // AGENTS.md 제목 크기(20~22). 웹 22px(1280 기준)에 가깝다.
        text.enableAutoSizing = false;
        text.richText = false;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.alignment = alignment;
        text.color = new Color32(0xD5, 0xE6, 0xEB, 0xFF);
        text.text = string.Empty;
        return text;
    }

    // ── 2. 갱단 통행 벽 ──────────────────────────────────────────────

    static void ApplyGangPassBoundary()
    {
        var context = UnityEngine.Object.FindFirstObjectByType<WorldInteractionSceneContext>(FindObjectsInactive.Include);
        if (context == null) throw new InvalidOperationException("[Issue151] OutSide에 WorldInteractionSceneContext가 없습니다.");
        var guard = FindByName("neighborhood-guardBlocked");
        if (guard == null) throw new InvalidOperationException("[Issue151] neighborhood-guardBlocked 대상을 찾지 못했습니다.");
        var parent = guard.transform.parent;

        var existing = parent.Find("GangPassBoundary");
        GangPassBoundary boundary;
        if (existing != null) boundary = existing.GetComponent<GangPassBoundary>();
        else
        {
            var leftWall = FindByPath("03.World/Collision/Left")?.GetComponent<BoxCollider2D>();
            if (leftWall == null) throw new InvalidOperationException("[Issue151] 03.World/Collision/Left 벽을 찾지 못했습니다.");
            var player = context.Player;
            var playerCollider = player != null ? player.GetComponent<BoxCollider2D>() : null;
            if (playerCollider == null) throw new InvalidOperationException("[Issue151] 루나의 BoxCollider2D를 찾지 못했습니다.");
            float halfWidth = playerCollider.size.x * Mathf.Abs(playerCollider.transform.lossyScale.x) * .5f;
            float rightFace = guard.transform.position.x + GuardClampOffset - halfWidth;
            Vector3 leftCenter = leftWall.transform.TransformPoint(leftWall.offset);
            float height = leftWall.size.y * Mathf.Abs(leftWall.transform.lossyScale.y);
            const float width = .2f;

            var holder = new GameObject("GangPassBoundary");
            Undo.RegisterCreatedObjectUndo(holder, "Gang Pass Boundary");
            holder.transform.SetParent(parent, false);
            holder.transform.position = Vector3.zero;
            boundary = holder.AddComponent<GangPassBoundary>();

            var wallObject = new GameObject("PassBlockWall") { layer = leftWall.gameObject.layer };
            wallObject.transform.SetParent(holder.transform, false);
            wallObject.transform.position = new Vector3(rightFace - width * .5f, leftCenter.y, 0f);
            var wall = wallObject.AddComponent<BoxCollider2D>();
            wall.size = new Vector2(width, height);

            var so = new SerializedObject(boundary);
            so.FindProperty("bounds").objectReferenceValue = wallObject;
            so.FindProperty("wall").objectReferenceValue = wall;
            so.ApplyModifiedPropertiesWithoutUndo();
            wallObject.SetActive(false);
            Debug.Log($"[Issue151] 갱단 통행 벽 생성: 오른쪽 면 x={rightFace:F3}, 경비 x={guard.transform.position.x:F3}");
        }

        var contextObject = new SerializedObject(context);
        var field = contextObject.FindProperty("gangPassBoundary");
        if (field.objectReferenceValue == null)
        {
            field.objectReferenceValue = boundary;
            contextObject.ApplyModifiedProperties();
        }
    }

    // ── 3. 밀수 거래·계약 연출 ───────────────────────────────────────

    static void ApplyContractStaging()
    {
        // 1일차 퇴근 밀수 거래: 밀수업자 x 3.72, 카메라는 (두 사람 가운데 + 루나) / 2.
        var trade = Npc("outside_day1_samho_smuggler", out var tradeSamho, out var tradeDealer);
        MoveX(tradeDealer, TradePartnerX);
        float tradeCenter = (tradeSamho.position.x + TradePartnerX) * .5f;
        SetStep(trade, "web-dialogue-start", "CameraFocus", "offset", v => new Vector2(tradeCenter, v.y));

        // 3일차 퇴근 계약: 밀수업자 x -9.34, 밀수업자는 오른쪽(루나 쪽)으로 퇴장, 루나는 그 끝 + 0.4 이상.
        var contract = Npc("day3-samho-contract", out var contractSamho, out var contractDealer);
        MoveX(contractDealer, ContractPartnerX);
        float samhoX = contractSamho.position.x;
        // 웹 dealerDirection: 밀수업자가 삼호보다 오른쪽이면 오른쪽으로, 아니면 왼쪽으로 걸어 나간다.
        float direction = ContractPartnerX > samhoX ? 1f : -1f;
        float exitDistance = 0f;
        SetStep(contract, "dealer-exit", "DepartWhileMoving", "offset", v =>
        {
            exitDistance = Mathf.Abs(v.x);
            return new Vector2(direction * exitDistance, v.y);
        });
        float lunaX = Mathf.Max(samhoX + ContractMinimumGap, ContractPartnerX + direction * exitDistance + ContractExitMargin);
        SetStep(contract, "web-dialogue-start", "MovePlayer", "offset", v => new Vector2(lunaX - samhoX, v.y));
        float cameraX = (Mathf.Min(samhoX, ContractPartnerX) + lunaX) * .5f;
        SetStep(contract, "web-dialogue-start", "MovePlayer", "cameraOffset", v => new Vector2(cameraX - lunaX, v.y));
        Debug.Log($"[Issue151] 계약 연출: 삼호 {samhoX:F3}, 밀수업자 {ContractPartnerX}, 루나 {lunaX:F3}, 카메라 {cameraX:F3}; 거래 카메라 중심 {tradeCenter:F3}");
    }

    static NpcInteraction Npc(string targetName, out Transform samho, out Transform dealer)
    {
        var go = FindByName(targetName);
        var interaction = go != null ? go.GetComponent<NpcInteraction>() : null;
        if (interaction == null) throw new InvalidOperationException("[Issue151] NPC 대상을 찾지 못했습니다: " + targetName);
        var so = new SerializedObject(interaction);
        samho = ((Component)so.FindProperty("actor").objectReferenceValue)?.transform;
        dealer = null;
        var extra = so.FindProperty("additionalActors");
        for (int i = 0; i < extra.arraySize; i++)
        {
            var actor = extra.GetArrayElementAtIndex(i).FindPropertyRelative("actor").objectReferenceValue as Component;
            if (actor != null && actor.name.Contains("dealer")) dealer = actor.transform;
        }
        if (samho == null || dealer == null) throw new InvalidOperationException("[Issue151] 삼호·밀수업자 배우 연결이 없습니다: " + targetName);
        return interaction;
    }

    static void MoveX(Transform target, float x)
    {
        if (Mathf.Abs(target.position.x - x) < .0001f) return;
        Undo.RecordObject(target, "Move NPC");
        target.position = new Vector3(x, target.position.y, target.position.z);
        EditorUtility.SetDirty(target);
    }

    static void SetStep(NpcInteraction interaction, string sequenceId, string kind, string field, Func<Vector2, Vector2> change)
    {
        var so = new SerializedObject(interaction);
        var sequences = so.FindProperty("sequences");
        for (int i = 0; i < sequences.arraySize; i++)
        {
            var sequence = sequences.GetArrayElementAtIndex(i);
            if (sequence.FindPropertyRelative("id").stringValue != sequenceId) continue;
            var actions = sequence.FindPropertyRelative("actions");
            for (int j = 0; j < actions.arraySize; j++)
            {
                var action = actions.GetArrayElementAtIndex(j);
                var kindProperty = action.FindPropertyRelative("kind");
                if (kindProperty.enumNames[kindProperty.enumValueIndex] != kind) continue;
                var value = action.FindPropertyRelative(field);
                var next = change(value.vector2Value);
                if ((next - value.vector2Value).sqrMagnitude < 1e-8f) return;
                value.vector2Value = next;
                so.ApplyModifiedProperties();
                return;
            }
        }
        throw new InvalidOperationException($"[Issue151] {interaction.name}의 {sequenceId}/{kind} 동작을 찾지 못했습니다.");
    }

    static GameObject FindByName(string name) =>
        Resources.FindObjectsOfTypeAll<Transform>()
            .Where(t => t.gameObject.scene.IsValid() && t.name == name).Select(t => t.gameObject).FirstOrDefault();

    static GameObject FindByPath(string path)
    {
        string[] parts = path.Split('/');
        var root = EditorSceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == parts[0]);
        if (root == null) return null;
        var found = root.transform.Find(string.Join("/", parts.Skip(1)));
        return found != null ? found.gameObject : null;
    }
}
