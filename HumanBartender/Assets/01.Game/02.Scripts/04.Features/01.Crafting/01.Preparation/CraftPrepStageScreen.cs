using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>제조 준비 프리팹의 표시와 입력만 담당한다. 선택 상태는 CraftPreparation이 소유한다.</summary>
public sealed class CraftPrepStageScreen : MonoBehaviour
{
    public enum Stage { Glass, Tool, Liquor, Fridge }

    [Header("Data")]
    [SerializeField] private NewShelfItemDataSO shelfData;
    [SerializeField] private CocktailRecipeVisualCatalog recipeVisuals;

    [Header("World stage")]
    [SerializeField] private Camera stageCamera;
    [SerializeField] private float cameraMoveDuration = 0.5f;
    [SerializeField] private AnimationCurve cameraEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Transform[] stageAnchors;
    [SerializeField] private CraftPrepShelfSlot[] slots;
    [SerializeField] private CraftPrepItemTooltip itemTooltip;

    [Header("Fixed HUD")]
    [SerializeField] private CocktailRecipePreview recipePreview;
    [SerializeField] private RectTransform trayContent;
    [SerializeField] private RectTransform trayPosition;
    [SerializeField] private float trayShiftWhenRecipeOpen = 238f;
    [SerializeField] private CraftPrepTrayItem trayItemPrefab;
    [SerializeField] private GameObject trayPlaceholder;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button recipeButton;
    [SerializeField] private Button recipeCloseButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button backButton;
    [Header("Notice Popup")]
    [SerializeField] private TMP_FontAsset noticeFont;
    [SerializeField] private float noticeFontSize = 22f;
    private GameObject openerPopup;
    [SerializeField] private Image[] stageIndicators;
    [SerializeField] private Sprite inactiveIndicator;
    [SerializeField] private Sprite activeIndicator;

    private readonly List<CraftPrepTrayItem> trayItems = new List<CraftPrepTrayItem>();
    private CraftPreparation preparation;
    private CraftFlowController craftFlow;
    private CraftPrepShelfSlot hoveredSlot;
    private Vector2 originalTrayPosition;
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
    public Camera GuideCamera => stageCamera;

    public Transform GetTutorialTarget(string step, bool beer)
    {
        if (step == "readRecipe") return recipeOpen ? recipeCloseButton.transform : recipeButton.transform;
        if (step == "navigateGin" || step == "navigateSoda" || step == "opener" && CurrentStage != Stage.Tool)
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
            "hoverGin" or "addGin" or "ginAgain" => "gin",
            "hoverSoda" or "addSoda" => beer ? "beer" : "soda_water",
            _ => null
        };
        foreach (var slot in slots) if (slot != null && slot.ItemId == id) return slot.transform;
        return null;
    }

    private void Awake()
    {
        if (trayPosition != null) originalTrayPosition = trayPosition.anchoredPosition;
        if (previousButton != null) previousButton.onClick.AddListener(Previous);
        if (nextButton != null) nextButton.onClick.AddListener(Next);
        if (recipeButton != null) recipeButton.onClick.AddListener(ToggleRecipe);
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
        if (recipeButton != null) recipeButton.onClick.RemoveListener(ToggleRecipe);
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
                    item.UnlockDay > GameStateManager.Instance.CurrentDay)
                {
                    slot.SetUnavailable();
                    continue;
                }
                slot.Configure(this, item, preparation.IsSelected(item.Id), preparation.IsGuideTarget(item.Id));
            }
        }
        if (itemTooltip != null) itemTooltip.Hide();
        hoveredSlot = null;
        RefreshTray();
        if (stageIndicators != null)
        {
            for (int index = 0; index < stageIndicators.Length; index++)
                if (stageIndicators[index] != null)
                    stageIndicators[index].sprite = index == stageIndex ? activeIndicator : inactiveIndicator;
        }
        if (previousButton != null) previousButton.interactable = stageIndex > 0;
        if (nextButton != null) nextButton.interactable = stageIndex < stageAnchors.Length - 1;
        RefreshStartButton();
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
        if (trayPlaceholder != null) trayPlaceholder.transform.SetAsLastSibling();
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
            openerEntry.Bind(this, id, Stage.Tool, "병따개", openerIcon);
            trayItems.Add(openerEntry);
            return;
        }
        if (shelfData == null || !shelfData.TryGet(id, out NewShelfItemData data)) return;
        Sprite icon = null;
        if (slots != null)
            foreach (CraftPrepShelfSlot slot in slots)
                if (slot != null && slot.ItemId == id) { icon = slot.Icon; break; }
        if (icon == null && recipeVisuals != null) icon = recipeVisuals.GetIngredientSprite(id);
        CraftPrepTrayItem entry = Instantiate(trayItemPrefab, trayContent);
        entry.Bind(this, id, stage, string.IsNullOrEmpty(data.Name.Ko) ? id : data.Name.Ko, icon);
        trayItems.Add(entry);
    }

    public void ToggleItem(string id, Stage stage)
    {
        if (preparation == null) return;
        if (stage == Stage.Glass) preparation.ToggleGlass(id);
        else if (stage == Stage.Tool) preparation.ToggleTool(id);
        else preparation.ToggleIngredient(id);
    }

    public bool CanInteract(Stage stage)
    {
        return preparation != null && !cameraMoving && stageIndex == (int)stage;
    }

    public void ShowItemInfo(CraftPrepShelfSlot slot, NewShelfItemData item)
    {
        hoveredSlot = slot;
        if (itemTooltip != null) itemTooltip.Show(item, slot.InfoAnchor, slot.InfoDirection);
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
        if (preparation == null || cameraMoving || stageAnchors == null ||
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
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
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

    private void OpenRecipe()
    {
        if (preparation == null || recipePreview == null) return;
        recipePreview.Show(preparation.Cocktail, recipeVisuals);
        recipeOpen = true;
        if (trayPosition != null) trayPosition.anchoredPosition = originalTrayPosition + Vector2.right * trayShiftWhenRecipeOpen;
    }

    private void CloseRecipe()
    {
        if (recipePreview != null) recipePreview.Hide();
        bool wasOpen = recipeOpen;
        if (wasOpen && preparation != null) preparation.MarkRecipeNoteRead();
        recipeOpen = false;
        if (trayPosition != null) trayPosition.anchoredPosition = originalTrayPosition;
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
        TMP_Text message = CreateLabel(openerPopup.transform, "병따기가 필요한 재료가 있습니다.\n오프너를 선택한 후 제조를 시작해 주세요.");
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
