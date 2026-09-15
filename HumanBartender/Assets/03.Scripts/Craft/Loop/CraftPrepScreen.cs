using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 제조 준비 화면(칵테일 제조 준비 시스템 §3).
///
/// 잔 → 도구 → 술 선반 → 냉장고 순서로 한 화면씩 넘어간다. 탭으로 아무 데나 뛰지 않고 이전·다음으로만
/// 움직이는 것은, 바텐더가 잔을 먼저 집고 도구를 들고 술을 따르는 실제 순서를 그대로 밟게 하려는 것이다.
///
/// 마지막 화면(냉장고)에서 다음을 누르면 기믹이 시작된다. 그 전까지 다음은 화면을 넘길 뿐이라
/// 아무것도 확정하지 않는다 — 도구를 안 고르고 지나가도 되고, 되돌아와 다시 고를 수도 있다.
///
/// 무엇을 고를 수 있는지는 CraftShelf가, 골라도 되는지·다 골랐는지는 CraftPreparation이 정한다.
/// 이 화면은 그 둘을 옮겨 그리고 클릭을 돌려보내기만 한다 — 판정을 여기서 다시 하면 화면과 기록이
/// 서로 다른 말을 하기 시작한다.
///
/// 칸은 아직 색 사각형이다. 선반 아트가 나오면 CreateCell만 갈아 끼우면 된다.
/// </summary>
public class CraftPrepScreen : MonoBehaviour
{
    /// <summary>
    /// 화면 한 장. 값의 순서가 곧 넘어가는 순서다 — 사이에 화면을 끼우려면 여기에 끼우면 된다.
    /// </summary>
    public enum EStage
    {
        Glass,
        Tool,
        Liquor,
        Fridge,
    }

    /// <summary>넘어가는 순서. 마지막 화면에서 다음을 누르면 기믹으로 간다.</summary>
    static readonly EStage[] Order =
    {
        EStage.Glass,
        EStage.Tool,
        EStage.Liquor,
        EStage.Fridge,
    };

    [Header("Data")]
    [SerializeField] NewShelfItemDataSO shelfData;
    [SerializeField] NewCocktailDataSO cocktailData;

    [Header("Scene")]
    [Tooltip("제조 흐름. 연결하면 칵테일을 고를 때 이 화면이 저절로 열린다. 단독 테스트 씬에서는 비워 둔다.")]
    [SerializeField] CraftFlowController craftFlow;

    [Header("Layout")]
    [Tooltip("준비 중에만 켜지는 묶음. 이 컴포넌트가 붙은 오브젝트는 계속 켜 둬야 한다 — " +
             "그걸 끄면 OnDisable이 돌아 제조 시작 신호 구독이 끊긴다.")]
    [SerializeField] GameObject content;

    [SerializeField] RectTransform shelfContent;
    [SerializeField] RectTransform trayContent;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI stageText;
    [SerializeField] TextMeshProUGUI noticeText;
    [SerializeField] Button prevButton;
    [SerializeField] Button nextButton;
    [SerializeField] TextMeshProUGUI nextLabel;
    [SerializeField] Button recipeNoteButton;

    [Header("Font")]
    [SerializeField] TMP_FontAsset font;

    [Header("Style")]
    [SerializeField] Vector2 cellSize = new(104f, 116f);
    [SerializeField] Color cellColor = new(1f, 1f, 1f, 0.06f);
    [SerializeField] Color cellSelectedColor = new(0.83f, 0.55f, 0.18f, 0.28f);
    [SerializeField] Color guideColor = new(0.36f, 0.76f, 0.72f, 0.30f);

    [Header("Test")]
    [Tooltip("단독 테스트 씬용. 켜 두면 시작할 때 testCocktailId로 준비 화면을 연다. " +
             "손님이 있는 씬(Play)에서는 켜져 있어도 무시한다 — 진행 일차를 덮어쓰고 주문 없는 제조를 여는 탓이다.")]
    [SerializeField] bool openOnStartForTest;
    [SerializeField] string testCocktailId = "gin_fizz";
    [SerializeField] int testDay = 1;

    CraftPreparation preparation;
    int stageIndex;

    /// <summary>다시 그릴 때 지워야 하는 칸들. 매번 자식을 통째로 훑지 않으려고 들고 있는다.</summary>
    readonly List<GameObject> spawned = new();

    /// <summary>지금 열려 있는 준비. 열려 있지 않으면 null이다.</summary>
    public CraftPreparation Preparation => preparation;

    /// <summary>지금 보고 있는 화면.</summary>
    public EStage Stage => Order[stageIndex];

    bool IsLastStage => stageIndex == Order.Length - 1;

    void Awake()
    {
        if (prevButton != null) prevButton.onClick.AddListener(OnPrev);
        if (nextButton != null) nextButton.onClick.AddListener(OnNext);
        if (recipeNoteButton != null) recipeNoteButton.onClick.AddListener(OnRecipeNoteClosed);

        SetVisible(false);
    }

    /// <summary>
    /// 화면을 보이거나 감춘다.
    ///
    /// 컴포넌트가 붙은 오브젝트가 아니라 안쪽 묶음만 끈다. 바깥을 끄면 OnDisable이 돌면서
    /// CraftBegan 구독이 끊겨, 다음에 칵테일을 골라도 이 화면이 열리지 않는다.
    /// </summary>
    void SetVisible(bool visible)
    {
        if (content != null) content.SetActive(visible);
    }

    void OnEnable()
    {
        if (craftFlow == null) return;

        craftFlow.CraftBegan += OnCraftBegan;
        craftFlow.CraftCompleted += OnCraftCompleted;
    }

    void OnDisable()
    {
        if (craftFlow == null) return;

        craftFlow.CraftBegan -= OnCraftBegan;
        craftFlow.CraftCompleted -= OnCraftCompleted;
    }

    void Start()
    {
        if (!openOnStartForTest) return;

        // 손님을 들이는 씬에서는 테스트 자동 열기를 쓰지 않는다.
        //
        // OpenForTest는 진행 일차를 testDay로 덮어쓰고 주문 없이 제조를 연다. 단독 테스트 씬에서는
        // 그게 목적이지만, 1부가 도는 씬에서는 일차가 바뀌어 엉뚱한 손님이 오고 "받을 주문이 없다"는
        // 경고만 남는다. 프리팹에 켜진 채로 저장돼 있어 얹을 때마다 되풀이되므로 여기서 막는다.
        if (FindAnyObjectByType<GuestManager>() != null)
        {
            Debug.LogWarning("[CraftPrep] 손님이 있는 씬이라 테스트 자동 열기를 건너뜁니다. " +
                             "Open On Start For Test를 꺼 두세요.");
            return;
        }

        OpenAfterDataLoadAsync().Forget();
    }

    /// <summary>
    /// json 로딩이 끝나기를 기다렸다가 연다. 기다리지 않으면 선반이 빈 채로 뜨는데, 예외도 나지 않아
    /// 데이터가 없는 것인지 로딩이 덜 된 것인지 구분할 수 없다.
    /// </summary>
    async UniTaskVoid OpenAfterDataLoadAsync()
    {
        await NewDataLoadManager.WaitUntilLoadedAsync();

        if (this == null) return;

        OpenForTest();
    }

    // ── 열고 닫기 ───────────────────────────────────────────────────────

    /// <summary>준비를 화면에 건다. 이전에 걸려 있던 것은 놓는다.</summary>
    public void Open(CraftPreparation prep)
    {
        Close();

        preparation = prep;
        if (preparation == null) return;

        preparation.Changed += Refresh;

        stageIndex = 0;

        SetVisible(true);
        Refresh();
    }

    /// <summary>화면을 비운다. 신호를 놓지 않으면 다음 제조에서 두 번 갱신된다.</summary>
    public void Close()
    {
        if (preparation != null) preparation.Changed -= Refresh;

        preparation = null;
        ClearSpawned();
        SetVisible(false);
    }

    void OnDestroy()
    {
        Close();
    }

    void OnCraftBegan(CraftSession session)
    {
        Open(craftFlow.Preparation);
    }

    /// <summary>
    /// 테스트 씬에서 쓰는 진입점.
    ///
    /// 제조 흐름이 연결돼 있으면 그쪽에 시도를 열게 한다 — 그래야 준비를 마친 뒤 기믹과 판정까지
    /// 실제로 이어진다. 연결돼 있지 않으면 준비 화면만 혼자 띄워 배치와 선택을 확인한다.
    /// </summary>
    public void OpenForTest()
    {
        // 일차를 바꾸는 것은 단독 테스트 씬에서만 뜻이 있다. 0 이하면 지금 일차를 그대로 쓴다.
        if (testDay > 0) GameStateManager.Instance.CurrentDay = testDay;

        if (craftFlow != null)
        {
            craftFlow.BeginCraft(testCocktailId); // CraftBegan을 타고 Open으로 돌아온다.
            return;
        }

        if (cocktailData == null || !cocktailData.TryGet(testCocktailId, out NewCocktailData cocktail))
        {
            Debug.LogError($"[CraftPrep] 테스트용 칵테일 '{testCocktailId}'을 찾지 못했습니다. " +
                           "cocktails.json이 로드된 뒤인지 확인하세요.");
            return;
        }

        Open(new CraftPreparation(cocktail, new ActualCraft()));
    }

    /// <summary>
    /// 기믹까지 끝나 판정이 나온 뒤. 등급을 한 줄로 남기고, 테스트 중이면 같은 칵테일로 다시 연다.
    /// 자세한 채점 내역은 CraftFlowController가 이미 로그로 남긴다.
    /// </summary>
    void OnCraftCompleted(CraftSession session, CraftJudgement judgement)
    {
        // 등급·점수는 둘 다 null일 수 있다(데이터 오류로 채점하지 못한 경우). 없는 값을 0으로 적으면
        // 최악의 제조와 채점 실패가 로그에서 구분되지 않는다.
        string grade = judgement?.CraftGrade is ENewGrade value ? value.ToString() : "채점 불가";
        string score = judgement?.CraftScore is float points ? points.ToString("0.0") : "-";

        Debug.Log($"[CraftPrep] 제조 끝 — {session.SelectedCocktailId} / 등급 {grade} / 점수 {score}");

        Close();

        if (openOnStartForTest) ReopenAfterCraftAsync().Forget();
    }

    /// <summary>
    /// 한 판이 끝나면 다시 준비 화면을 연다. 테스트 씬을 껐다 켜지 않고 여러 번 돌려 보기 위한 것이다.
    /// 한 프레임 쉬는 것은 기믹이 치워지고 바 UI가 되돌아온 뒤에 열기 위해서다.
    /// </summary>
    async UniTaskVoid ReopenAfterCraftAsync()
    {
        await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());

        OpenForTest();
    }

    // ── 화면 넘기기 ─────────────────────────────────────────────────────

    void OnPrev()
    {
        if (stageIndex == 0) return;

        stageIndex--;
        Refresh();
    }

    /// <summary>
    /// 마지막 화면이 아니면 다음 선반으로 넘어가기만 한다. 마지막 화면에서는 준비를 끝내고 기믹으로 간다.
    /// </summary>
    void OnNext()
    {
        if (preparation == null) return;

        if (!IsLastStage)
        {
            stageIndex++;
            Refresh();
            return;
        }

        if (!preparation.CanProceed) return;

        if (craftFlow != null)
        {
            craftFlow.StartGimmicks();
            return;
        }

        Debug.Log($"[CraftPrep] 준비 완료 — 잔 {preparation.GlassId} / 도구 {preparation.ToolId ?? "없음"} / " +
                  $"재료 {string.Join(", ", preparation.IngredientIds)}");
    }

    // ── 갱신 ────────────────────────────────────────────────────────────

    void Refresh()
    {
        if (preparation == null) return;

        RefreshHeader();
        RefreshShelf();
        RefreshTray();
        RefreshFooter();
    }

    void RefreshHeader()
    {
        if (titleText != null)
        {
            string name = preparation.Note.Name.Ko;
            titleText.text = string.IsNullOrEmpty(name) ? preparation.Note.CocktailId : name;
        }

        if (stageText != null)
            stageText.text = $"{StageLabel(Stage)}  {stageIndex + 1}/{Order.Length}";
    }

    void RefreshFooter()
    {
        if (prevButton != null) prevButton.interactable = stageIndex > 0;

        // 넘기는 것은 언제든 되고, 마지막에서 기믹으로 넘어갈 때만 조건을 본다(§3.8.2).
        if (nextButton != null) nextButton.interactable = !IsLastStage || preparation.CanProceed;
        if (nextLabel != null) nextLabel.text = IsLastStage ? "제조 시작" : "다음";

        if (noticeText == null) return;

        // 정답 구성을 다 갖춘 순간 한 번만 알린다. 넘어가는 것은 플레이어가 정한다(§3.8.1).
        if (preparation.ConsumeReadyAnnouncement())
            noticeText.text = "모든 재료를 선택하셨습니다. 다음 단계로 진행하십시오.";
        else if (IsLastStage && !preparation.CanProceed)
            noticeText.text = "잔과 재료를 최소한 하나씩 골라야 제조를 시작할 수 있습니다.";
        else
            noticeText.text = "";
    }

    static string StageLabel(EStage stage) => stage switch
    {
        EStage.Glass => "잔",
        EStage.Tool => "도구",
        EStage.Liquor => "술 선반",
        _ => "냉장고",
    };

    // ── 선반 ────────────────────────────────────────────────────────────

    List<NewShelfItemData> CollectShelfItems()
    {
        int day = GameStateManager.Instance.CurrentDay;

        return Stage switch
        {
            EStage.Glass => CraftShelf.GetGlasses(shelfData, day),
            EStage.Tool => CraftShelf.GetTools(shelfData, day),
            EStage.Liquor => CraftShelf.GetIngredients(shelfData, ENewShelfGroup.Liquor, day),
            _ => CraftShelf.GetIngredients(shelfData, ENewShelfGroup.Fridge, day),
        };
    }

    void RefreshShelf()
    {
        ClearSpawned();

        if (shelfContent == null) return;

        foreach (var item in CollectShelfItems())
            spawned.Add(CreateCell(item));
    }

    /// <summary>
    /// 선반 칸 하나. 색은 재료의 액체 색을 쓰고, 색이 없는 잔·도구는 회색으로 둔다.
    ///
    /// 고른 칸과 가이드 칸을 서로 다른 색으로 칠한다. 가이드는 노트를 한 번 열어 본 뒤에만 켜지고,
    /// 켜져 있어도 다른 것을 고르는 것을 막지 않는다(§3.7.8).
    /// </summary>
    GameObject CreateCell(NewShelfItemData item)
    {
        var go = new GameObject($"Cell {item.Id}",
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(shelfContent, false);

        var layout = go.GetComponent<LayoutElement>();
        layout.preferredWidth = cellSize.x;
        layout.preferredHeight = cellSize.y;

        bool selected = preparation.IsSelected(item.Id);
        bool guided = preparation.IsGuideTarget(item.Id);

        go.GetComponent<Image>().color = selected ? cellSelectedColor : guided ? guideColor : cellColor;

        CreateSwatch(go.transform, item);
        CreateLabel(go.transform, ResolveName(item), 12f, new Vector2(4f, 6f), new Vector2(-4f, 30f));

        string id = item.Id;
        go.GetComponent<Button>().onClick.AddListener(() => Toggle(id, Stage));

        return go;
    }

    /// <summary>칸 위쪽의 색 조각. 아트가 나오기 전까지 무엇인지 구분하는 유일한 단서다.</summary>
    void CreateSwatch(Transform parent, NewShelfItemData item)
    {
        var go = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(cellSize.x * 0.5f, cellSize.y * 0.44f);
        rect.anchoredPosition = new Vector2(0f, -12f);

        var image = go.GetComponent<Image>();
        image.color = item.TryGetLiquidColor(out Color32 color) ? color : new Color(1f, 1f, 1f, 0.35f);
        image.raycastTarget = false;
    }

    /// <summary>어느 선반에서 눌렀는지에 따라 잔·도구·재료 중 하나를 토글한다.</summary>
    void Toggle(string id, EStage from)
    {
        switch (from)
        {
            case EStage.Glass: preparation.ToggleGlass(id); break;
            case EStage.Tool: preparation.ToggleTool(id); break;
            default: preparation.ToggleIngredient(id); break;
        }
    }

    // ── 하단 트레이 ─────────────────────────────────────────────────────

    /// <summary>
    /// 지금까지 고른 것. 잔·도구·재료를 한 줄에 늘어놓고, 재료는 고른 순서를 그대로 지킨다.
    /// 어느 화면에 있든 전부 보인다 — 앞 화면에서 무엇을 골랐는지 되돌아가지 않고도 알아야 한다.
    /// 여기서 누르면 선택이 풀린다(§3.4.2).
    /// </summary>
    void RefreshTray()
    {
        if (trayContent == null) return;

        for (int i = trayContent.childCount - 1; i >= 0; i--)
            Destroy(trayContent.GetChild(i).gameObject);

        if (preparation.GlassId != null) CreateTrayChip(preparation.GlassId, EStage.Glass);
        if (preparation.ToolId != null) CreateTrayChip(preparation.ToolId, EStage.Tool);

        foreach (string ingredientId in preparation.IngredientIds)
            CreateTrayChip(ingredientId, EStage.Liquor); // 재료는 어느 선반에서 왔든 같은 토글을 쓴다.
    }

    void CreateTrayChip(string id, EStage from)
    {
        var go = new GameObject($"Chip {id}",
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(trayContent, false);

        go.GetComponent<Image>().color = cellSelectedColor;

        var layout = go.GetComponent<LayoutElement>();
        layout.preferredWidth = 96f;
        layout.preferredHeight = 28f;

        string label = shelfData != null && shelfData.TryGet(id, out var item) ? ResolveName(item) : id;
        CreateLabel(go.transform, label, 11.5f, new Vector2(6f, 2f), new Vector2(-6f, -2f));

        go.GetComponent<Button>().onClick.AddListener(() => Toggle(id, from));
    }

    // ── 그 밖 ───────────────────────────────────────────────────────────

    /// <summary>레시피 노트를 닫았을 때. 이때부터 정답 칸에 가이드가 켜진다(§3.7.5).</summary>
    void OnRecipeNoteClosed()
    {
        if (preparation == null) return;

        preparation.MarkRecipeNoteRead();
        Refresh();
    }

    void ClearSpawned()
    {
        foreach (var go in spawned)
        {
            if (go != null) Destroy(go);
        }

        spawned.Clear();
    }

    /// <summary>화면에 적을 이름. 한국어 이름이 없으면 id를 그대로 보여 준다 — 빈 칸보다 낫다.</summary>
    static string ResolveName(NewShelfItemData item)
    {
        return string.IsNullOrEmpty(item.Name.Ko) ? item.Id : item.Name.Ko;
    }

    void CreateLabel(Transform parent, string text, float size, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;

        if (font != null) label.font = font;
    }
}
