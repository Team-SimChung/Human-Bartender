using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>제조 준비 프리팹의 표시와 입력만 담당한다. 선택 상태는 CraftPreparation이 소유한다.</summary>
public sealed class CraftPrepStageScreen : MonoBehaviour
{
    ISoundEffectPlayer sound;
    public void ConfigureAudio(ISoundEffectPlayer player) => sound = player;
    public enum Stage { Glass, Tool, Liquor, Fridge }

    [Header("Data")]
    [SerializeField] private NewShelfItemDataSO shelfData;
    [SerializeField] private CocktailRecipeVisualCatalog recipeVisuals;

    [Header("World stage")]
    [SerializeField] private Camera stageCamera;
    [Tooltip("선반을 이동하는 데 걸리는 시간(초).")]
    [SerializeField, Min(0f)] private float cameraMoveDuration = 0.45f;
    [SerializeField] private AnimationCurve cameraEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Transform[] stageAnchors;
    [SerializeField] private CraftPrepShelfSlot[] slots;
    [SerializeField] private CraftPrepItemTooltip itemTooltip;

    [Header("Fixed HUD")]
    [SerializeField] private CocktailRecipePreview recipePreview;
    [Tooltip("하단 선택 슬롯 묶음. 화면 가운데에 고정되고 레시피를 열어도 움직이지 않는다(웹 기준).")]
    [SerializeField] private RectTransform trayContent;
    [SerializeField] private CraftPrepTrayItem trayItemPrefab;
    [SerializeField] private GameObject trayPlaceholder;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [Tooltip("왼쪽 위 레시피 버튼. 칵테일 이름과 준비물 목록을 보여 주고, 누르면 상세 레시피를 연다.")]
    [SerializeField] private CraftPrepRecipeBrief recipeBrief;
    [SerializeField] private Button recipeCloseButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button backButton;
    /// <summary>선반 데이터에 없는 병따개의 기본 표시 이름. 레시피 상세(CocktailRecipePreview)도 이 값을 기본값으로 쓴다.</summary>
    public const string DefaultBottleOpenerName = "병따개";
    [Tooltip("선반 데이터에 없는 병따개의 표시 이름. 레시피 버튼 목록에서 쓴다.")]
    [SerializeField] private string bottleOpenerName = DefaultBottleOpenerName;
    [Header("Notice Popup")]
    [SerializeField] private TMP_FontAsset noticeFont;
    [SerializeField] private float noticeFontSize = 22f;
    private GameObject openerPopup;
    [Header("Shelf Indicators")]
    [SerializeField] private Image[] stageIndicators;
    [SerializeField] private Button[] stageButtons;
    [SerializeField] private Sprite inactiveIndicator;
    [SerializeField] private Sprite activeIndicator;
    [Tooltip("선택된 스프라이트의 표시 너비. 두 스프라이트의 원본 픽셀을 같은 배율로 표시합니다.")]
    [SerializeField, Min(1)] private int indicatorSize = 30;

    private readonly List<CraftPrepTrayItem> trayItems = new List<CraftPrepTrayItem>();
    private readonly List<CraftPrepChecklistEntry> checklist = new List<CraftPrepChecklistEntry>();
    private System.Func<string, string> itemNameLookup;
    private CraftPreparation preparation;
    private CraftFlowController craftFlow;
    private CraftPrepShelfSlot hoveredSlot;
    private int stageIndex;
    private bool recipeOpen;
    private bool cameraMoving;
    private CancellationTokenSource cameraMoveCancellation;

    public bool IsOpen { get { return preparation != null; } }
    public bool IsBottleOpenerSelected => preparation != null && preparation.IsBottleOpenerSelected;
    public Stage CurrentStage => (Stage)stageIndex;
    public bool IsCameraMoving => cameraMoving;
    public string HoveredItemId => hoveredSlot != null ? hoveredSlot.ItemId : null;
    public System.Func<bool> TutorialCanStart { get; set; }
    /// <summary>왼쪽 위 레시피 상세가 열려 있는지. 튜토리얼이 열기·확인·닫기 단계를 나눌 때 읽는다.</summary>
    public bool IsRecipeOpen => recipeOpen;
    public Camera GuideCamera => stageCamera;

    public Transform GetTutorialTarget(string step, bool beer)
    {
        if (step == "readRecipe") return recipeOpen ? recipeCloseButton.transform : recipeBrief.transform;
        if ((step is "opener" or "shaker") && CurrentStage != Stage.Tool)
            return CurrentStage < Stage.Tool ? nextButton.transform : previousButton.transform;
        if (step == "navigateGin" || step == "navigateSoda")
            return nextButton.transform;
        if (step == "start") return startButton.transform;
        if (step == "opener")
            foreach (var hotspot in GetComponentsInChildren<CraftPrepInfoHotspot>(true))
                if (hotspot.IsBottleOpener) return hotspot.transform;
        if (step == "removeGin")
        {
            foreach (var item in trayItems) if (item != null && item.ItemId == "gin") return item.SelectionControl;
            return trayContent;
        }
        string id = step switch
        {
            "glass" => preparation?.Cocktail.Glass,
            "shaker" => preparation?.Cocktail.TargetToolId,
            "hoverGin" or "addGin" or "ginAgain" => "gin",
            "hoverSoda" or "addSoda" => beer ? "beer" : "soda_water",
            _ => null
        };
        foreach (var slot in slots) if (slot != null && slot.ItemId == id) return slot.transform;
        return null;
    }

    private void Awake()
    {
        if (previousButton != null) previousButton.onClick.AddListener(Previous);
        if (nextButton != null) nextButton.onClick.AddListener(Next);
        if (stageButtons != null)
        {
            for (int index = 0; index < stageButtons.Length; index++)
            {
                if (stageButtons[index] == null) continue;
                int target = index;
                stageButtons[index].onClick.AddListener(() => MoveToStage(target));
            }
        }
        if (recipeBrief != null && recipeBrief.Button != null) recipeBrief.Button.onClick.AddListener(ToggleRecipe);
        if (recipeCloseButton != null) recipeCloseButton.onClick.AddListener(CloseRecipe);
        if (startButton != null) startButton.onClick.AddListener(StartCraft);
        EnsureOpenerPopup();
        if (backButton != null) backButton.onClick.AddListener(Back);
        if (startButton != null) startButton.interactable = false;
        if (previousButton != null) previousButton.interactable = false;
        if (nextButton != null) nextButton.interactable = false;
        if (itemTooltip != null) itemTooltip.Hide();
        if (recipePreview != null) recipePreview.Hide();
    }

    public void Open(CraftPreparation current, CraftFlowController flow)
    {
        if (stageCamera == null || stageAnchors == null || stageAnchors.Length == 0 || stageAnchors[0] == null)
            throw new System.InvalidOperationException("[CraftPrep] 전용 카메라와 선반 앵커를 연결해 주세요.");
        if (preparation != null) preparation.Changed -= Refresh;
        if (craftFlow != null) craftFlow.PreparationBlocked -= ShowOpenerPopup;
        preparation = current;
        craftFlow = flow;
        if (craftFlow != null) craftFlow.PreparationBlocked += ShowOpenerPopup;
        stageIndex = 0;
        recipeOpen = false;
        cameraMoving = false;
        if (preparation != null) preparation.Changed += Refresh;
        CloseRecipe();
        Vector3 cameraPosition = stageCamera.transform.position;
        cameraPosition.x = stageAnchors[0].position.x;
        stageCamera.transform.position = cameraPosition;
        Refresh();
    }

    public void Close()
    {
        if (craftFlow != null) craftFlow.PreparationBlocked -= ShowOpenerPopup;
        if (openerPopup != null) openerPopup.SetActive(false);
        if (cameraMoveCancellation != null) cameraMoveCancellation.Cancel();
        if (preparation != null) preparation.Changed -= Refresh;
        preparation = null;
        cameraMoving = false;
        if (itemTooltip != null) itemTooltip.Hide();
    }

    private void OnDestroy()
    {
        Close();
        if (previousButton != null) previousButton.onClick.RemoveListener(Previous);
        if (nextButton != null) nextButton.onClick.RemoveListener(Next);
        if (recipeBrief != null && recipeBrief.Button != null) recipeBrief.Button.onClick.RemoveListener(ToggleRecipe);
        if (recipeCloseButton != null) recipeCloseButton.onClick.RemoveListener(CloseRecipe);
        if (startButton != null) startButton.onClick.RemoveListener(StartCraft);
        if (backButton != null) backButton.onClick.RemoveListener(Back);
    }

    private void Refresh()
    {
        if (preparation == null) return;
        if (slots != null)
        {
            foreach (CraftPrepShelfSlot slot in slots)
            {
                if (slot == null) continue;
                if (shelfData == null || !shelfData.TryGet(slot.ItemId, out NewShelfItemData item) ||
                    !(craftFlow != null ? craftFlow.IsShelfItemAvailable(item) :
                        CraftShelf.IsAvailable(item, GameStateManager.Instance.CurrentDay)))
                {
                    slot.SetUnavailable();
                    continue;
                }
                slot.Configure(this, item, preparation.IsSelected(item.Id));
            }
        }
        if (hoveredSlot != null && (!hoveredSlot.isActiveAndEnabled || !CanInteract(hoveredSlot.ItemStage)))
            HideItemInfo(hoveredSlot);
        RefreshTray();
        RefreshRecipeBrief();
        RefreshStageIndicators();
        if (previousButton != null) previousButton.interactable = stageIndex > 0;
        if (nextButton != null) nextButton.interactable = stageIndex < stageAnchors.Length - 1;
        RefreshStartButton();
    }

    private void RefreshStageIndicators()
    {
        if (stageIndicators == null) return;
        Sprite reference = activeIndicator != null ? activeIndicator : inactiveIndicator;
        if (reference == null) return;
        float scale = indicatorSize / reference.rect.width;
        for (int index = 0; index < stageIndicators.Length; index++)
        {
            Image indicator = stageIndicators[index];
            if (indicator == null) continue;
            indicator.sprite = index == stageIndex ? activeIndicator : inactiveIndicator;
            if (indicator.sprite == null) continue;
            // 회색 점은 원 안쪽만 잘려 있고 선택된 점은 바깥 빛까지 포함한다.
            Vector2 size = indicator.sprite.rect.size * scale;
            indicator.rectTransform.sizeDelta = new Vector2(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));
        }
    }

    void Update() => RefreshStartButton();

    void RefreshStartButton()
    {
        if (startButton != null) startButton.interactable = preparation != null && preparation.CanProceed &&
            (TutorialCanStart == null || TutorialCanStart());
    }

    private void RefreshTray()
    {
        foreach (CraftPrepTrayItem entry in trayItems)
            if (entry != null) Destroy(entry.gameObject);
        trayItems.Clear();
        if (preparation.GlassId != null) AddTrayItem(preparation.GlassId, Stage.Glass);
        if (preparation.ToolId != null) AddTrayItem(preparation.ToolId, Stage.Tool);
        foreach (string id in preparation.IngredientIds)
            AddTrayItem(id, Stage.Liquor);
        if (trayPlaceholder != null)
        {
            trayPlaceholder.SetActive(preparation.CanAddIngredient);
            trayPlaceholder.transform.SetAsLastSibling();
        }
    }

    private void RefreshRecipeBrief()
    {
        if (recipeBrief == null) return;
        if (itemNameLookup == null) itemNameLookup = ItemName;
        CraftPrepRecipeChecklist.Build(preparation, shelfData, checklist);
        recipeBrief.Bind(preparation.Cocktail.Name.Ko, checklist, itemNameLookup);
    }

    private string ItemName(string id)
    {
        if (id == NewToolIds.BottleOpener) return bottleOpenerName;
        return shelfData != null && shelfData.TryGet(id, out NewShelfItemData item) && !string.IsNullOrEmpty(item.Name.Ko)
            ? item.Name.Ko : id;
    }

    private void AddTrayItem(string id, Stage stage)
    {
        if (trayItemPrefab == null || trayContent == null) return;
        if (id == NewToolIds.BottleOpener)
        {
            Sprite openerIcon = null;
            foreach (CraftPrepInfoHotspot hotspot in GetComponentsInChildren<CraftPrepInfoHotspot>(true))
                if (hotspot.IsBottleOpener) { openerIcon = hotspot.Icon; break; }
            CraftPrepTrayItem openerEntry = Instantiate(trayItemPrefab, trayContent);
            openerEntry.Bind(this, id, Stage.Tool, openerIcon);
            trayItems.Add(openerEntry);
            return;
        }
        if (shelfData == null || !shelfData.TryGet(id, out _)) return;
        Sprite icon = null;
        if (slots != null)
            foreach (CraftPrepShelfSlot slot in slots)
                if (slot != null && slot.ItemId == id) { icon = slot.Icon; break; }
        if (icon == null && recipeVisuals != null) icon = recipeVisuals.GetIngredientSprite(id);
        CraftPrepTrayItem entry = Instantiate(trayItemPrefab, trayContent);
        entry.Bind(this, id, stage, icon);
        trayItems.Add(entry);
    }

    public void ToggleItem(string id, Stage stage)
    {
        if (preparation == null) return;
        // 병따개는 선반 데이터에 없는 도구라 아래 선반 확인을 통과하지 못한다. 트레이 X로도 바로 뺄 수 있게 따로 처리한다.
        if (stage == Stage.Tool && id == NewToolIds.BottleOpener)
        {
            ToggleBottleOpener();
            return;
        }
        if (shelfData == null || !shelfData.TryGet(id, out var item) ||
            !(craftFlow != null ? craftFlow.IsShelfItemAvailable(item) :
                CraftShelf.IsAvailable(item, GameStateManager.Instance.CurrentDay))) return;
        if (stage == Stage.Glass) preparation.ToggleGlass(id);
        else if (stage == Stage.Tool) preparation.ToggleTool(id);
        else preparation.ToggleIngredient(id);
    }

    public bool CanInteract(Stage stage)
    {
        return preparation != null && !cameraMoving && stageIndex == (int)stage;
    }

    public void ShowItemInfo(CraftPrepShelfSlot slot, NewShelfItemData item, Vector2 pointerPosition)
    {
        if (hoveredSlot != slot && item.Kind == ENewShelfKind.Ingredient)
            sound.PlayCue("SFX_ui_hover", exclusive: true);
        hoveredSlot = slot;
        if (itemTooltip != null) itemTooltip.Show(item, stageCamera, pointerPosition);
    }

    public void HideItemInfo(CraftPrepShelfSlot slot)
    {
        if (hoveredSlot != slot) return;
        hoveredSlot = null;
        if (itemTooltip != null) itemTooltip.Hide();
    }

    public void Previous() { MoveToStage(stageIndex - 1); }
    public void Next() { MoveToStage(stageIndex + 1); }

    private void MoveToStage(int index)
    {
        if (preparation == null || cameraMoving || index == stageIndex || stageAnchors == null ||
            index < 0 || index >= stageAnchors.Length || stageAnchors[index] == null) return;

        stageIndex = index;
        cameraMoving = true;
        if (itemTooltip != null) itemTooltip.Hide();
        hoveredSlot = null;
        Refresh();
        cameraMoveCancellation = new CancellationTokenSource();
        MoveCameraAsync(stageAnchors[index].position.x, cameraMoveCancellation).Forget(ReportCameraError);
    }

    private async UniTask MoveCameraAsync(float worldX, CancellationTokenSource cancellation)
    {
        try
        {
            Transform cameraTransform = stageCamera.transform;
            Vector3 start = cameraTransform.position;
            Vector3 target = new Vector3(worldX, start.y, start.z);
            for (float elapsed = 0f; elapsed < cameraMoveDuration;)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / cameraMoveDuration);
                float eased = cameraEase != null ? cameraEase.Evaluate(progress) : progress;
                cameraTransform.position = Vector3.Lerp(start, target, eased);
                await UniTask.Yield(cancellation.Token);
            }
            cancellation.Token.ThrowIfCancellationRequested();
            cameraTransform.position = target;
        }
        finally
        {
            if (ReferenceEquals(cameraMoveCancellation, cancellation)) cameraMoveCancellation = null;
            cancellation.Dispose();
            cameraMoving = false;
        }
    }

    private static void ReportCameraError(System.Exception error)
    {
        if (error is not System.OperationCanceledException) Debug.LogException(error);
    }
    private void ToggleRecipe() { if (recipeOpen) CloseRecipe(); else OpenRecipe(); }

    // 하단 슬롯은 레시피 표시와 상관없이 화면 가운데에 둔다(웹 preparation-resources.css 주석).
    // 레시피 버튼은 상세가 열린 동안 숨긴다(웹 .prep-open .recipe-toggle{visibility:hidden}).
    private void OpenRecipe()
    {
        if (preparation == null || recipePreview == null) return;
        recipePreview.Show(preparation.Cocktail, recipeVisuals);
        recipeOpen = true;
        if (recipeBrief != null) recipeBrief.gameObject.SetActive(false);
    }

    private void CloseRecipe()
    {
        if (recipePreview != null) recipePreview.Hide();
        bool wasOpen = recipeOpen;
        if (wasOpen && preparation != null) preparation.MarkRecipeNoteRead();
        recipeOpen = false;
        if (recipeBrief != null) recipeBrief.gameObject.SetActive(true);
        if (wasOpen) Refresh();
    }

    private void StartCraft()
    {
        if (preparation == null || !preparation.CanProceed || (TutorialCanStart != null && !TutorialCanStart())) return;
        if (craftFlow != null) craftFlow.StartGimmicks();
    }

    public void ToggleBottleOpener()
    {
        if (preparation == null) return;
        preparation.ToggleBottleOpener();
        if (openerPopup != null) openerPopup.SetActive(false);
    }

    private void EnsureOpenerPopup()
    {
        Canvas canvas = startButton != null ? startButton.GetComponentInParent<Canvas>() : GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;
        openerPopup = new GameObject("Opener Required Popup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        openerPopup.transform.SetParent(canvas.transform, false);
        RectTransform popupRect = (RectTransform)openerPopup.transform;
        popupRect.anchorMin = Vector2.zero;
        popupRect.anchorMax = Vector2.one;
        popupRect.offsetMin = popupRect.offsetMax = Vector2.zero;
        openerPopup.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
        // 웹 본편 openerWarning 팝업의 제목·본문과 같다.
        TMP_Text message = CreateLabel(openerPopup.transform,
            "<size=125%>병따개가 필요합니다</size>\n\n병맥주를 따르려면 먼저 뚜껑을 열어야 합니다.\n도구 선반에서 병따개를 선택해 주세요.");
        ((RectTransform)message.transform).offsetMin = new Vector2(40f, 100f);
        ((RectTransform)message.transform).offsetMax = new Vector2(-40f, -40f);
        Button close = CreateButton(openerPopup.transform, "Confirm", new Vector2(0f, -100f), new Vector2(140f, 44f), "확인");
        close.onClick.AddListener(() => openerPopup.SetActive(false));
        openerPopup.SetActive(false);
    }

    private void ShowOpenerPopup(string message)
    {
        if (openerPopup == null) return;
        openerPopup.transform.SetAsLastSibling();
        openerPopup.SetActive(true);
    }

    private TMP_Text CreateLabel(Transform parent, string text)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var label = go.GetComponent<TextMeshProUGUI>();
        TMP_Text source = startButton != null ? startButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (noticeFont != null) label.font = noticeFont;
        else if (source != null) label.font = source.font;
        label.text = text;
        label.fontSize = noticeFontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private Button CreateButton(Transform parent, string name, Vector2 position, Vector2 size, string text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(0.1f, 0.25f, 0.35f, 1f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        CreateLabel(go.transform, text);
        return button;
    }

    private void Back()
    {
        if (craftFlow != null) craftFlow.CancelCurrentCraft();
        else gameObject.SetActive(false);
    }
}
