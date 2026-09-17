using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 2부 대본이 부르는 제조·서빙 창구(2부 운영 명세 §8.2).
///
/// 만드는 일도 내는 일도 새로 만들지 않는다. 1부가 쓰는 것과 같은 제조 흐름(CraftFlowController)과
/// 같은 완성 잔 트레이(ServeTrayUI)를 그대로 쓰고, 여기서는 그 화면을 언제 열고 닫을지와
/// 완성 잔을 누구에게 내야 하는지만 정한다. 판정과 정산도 1부와 같은 것을 쓴다 —
/// 2부만의 계산을 따로 두면 같은 잔이 국면에 따라 다른 등급을 받는다.
///
/// 서빙 자리는 씬에 미리 두지 않고 주문이 생길 때 만든다. 2부의 좌석은 대본이 그때그때 채우는
/// 자리라, 어느 자리에 코스터가 필요한지가 실행 중에야 정해진다.
/// </summary>
public class StoryCraftGate : MonoBehaviour, IStoryCraftGate
{
    [Header("Craft")]
    [Tooltip("1부와 같은 제조 흐름. 여기서 잔이 완성된다.")]
    [SerializeField] CraftFlowController craftFlow;

    [Tooltip("칵테일 메뉴가 들어 있는 좌측 슬라이드 패널. 제조 스텝에서 열어 준다.")]
    [SerializeField] LeftSlidePanel craftPanel;

    [Tooltip("제조·서빙 동안에만 켜는 화면. 좌측 슬라이드 패널 캔버스와 완성 잔 트레이 캔버스를 꽂는다. " +
             "2부 대화 중에는 꺼 두어야 대사 위에 얹히지 않는다.")]
    [SerializeField] GameObject[] craftUiRoots;

    [Header("Serve")]
    [Tooltip("좌석에 앉은 인물의 위치를 묻는 곳. 서빙 자리를 그 앞에 놓는다.")]
    [SerializeField] DialogueCharacterManager characterManager;

    [Tooltip("서빙 자리를 올려놓을 캔버스. 완성 잔 트레이가 쓰는 것과 같아야 잔을 끌어다 놓을 수 있다.")]
    [SerializeField] Canvas serveCanvas;

    [SerializeField] Vector2 serveZoneSize = new(220f, 180f);

    [Tooltip("좌석 월드 좌표에서 서빙 자리까지의 어긋남. 인물의 손 앞에 오도록 맞춘다.")]
    [SerializeField] Vector3 serveZoneWorldOffset = new(0f, -0.6f, 0f);

    [Tooltip("서빙 자리를 눈에 보이게 할지. 코스터 그림이 붙기 전까지 자리를 확인하는 용도다.")]
    [SerializeField] bool showServeZoneGuide = true;

    [SerializeField] Color serveZoneColor = new(1f, 1f, 1f, 0.12f);

    [Header("Settlement")]
    [SerializeField] NewCocktailDataSO cocktailData;
    [SerializeField] NewBalanceDataSO balanceData;

    [Tooltip("당일 매출 장부를 들고 있는 곳. 2부 매출도 1부와 같은 장부에 쌓여야 하루 합계가 맞는다. " +
             "비워 두면 정산 없이 진행한다.")]
    [SerializeField] GuestManager guestManager;

    // 주문과 제조 세션은 원본 참조를 공유한다.
    sealed class OrderPresentation
    {
        public StoryOrder Order;
        public GameObject Zone;
        public readonly CancellationTokenSource Lifetime = new();
        public bool WaitingForServe;
    }
    sealed class CraftRequest
    {
        public CancellationTokenSource Lifetime;
        public CraftSession Session;
    }
    OrderPresentation orderView;
    CraftRequest craftRequest;

    public void OpenOrder(StoryOrder order)
    {
        if (order == null || order.IsServed) throw new ArgumentException("An unserved order is required.");
        if (orderView != null) throw new InvalidOperationException("Close the current story order first.");
        orderView = new OrderPresentation { Order = order };
        BuildServeZone(order);
    }

    public void CloseOrder(StoryOrder order)
    {
        var view = orderView;
        if (view == null || !ReferenceEquals(view.Order, order)) return;
        orderView = null;
        view.Lifetime.Cancel();
        view.Lifetime.Dispose();
        RemoveServeZone(view);
        craftRequest?.Lifetime.Cancel();
        SetCraftUiActive(false);
    }

    static void RemoveServeZone(OrderPresentation view)
    {
        if (view.Zone == null) return;
        view.Zone.GetComponent<StoryServeDropTarget>().Bind(null);
        view.Zone.SetActive(false);
        Destroy(view.Zone);
        view.Zone = null;
    }

    void OnDisable()
    {
        craftRequest?.Lifetime.Cancel();
        if (orderView != null) CloseOrder(orderView.Order);
    }

    static bool IsBusy(CraftSession session) => session != null &&
        (session.Phase == ECraftPhase.Preparing || session.Phase == ECraftPhase.Playing);

    public async UniTask<bool> RunCraftAsync(StoryOrder order, string tutorialCocktailId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (craftFlow == null || craftRequest != null || IsBusy(craftFlow.Current))
            throw new InvalidOperationException("Craft flow is missing or a previous craft is still running.");
        if (order != null && (orderView == null || !ReferenceEquals(orderView.Order, order) || order.IsServed))
            throw new InvalidOperationException("Craft order is no longer active.");
        if (order == null && string.IsNullOrEmpty(tutorialCocktailId))
            throw new InvalidOperationException("Craft needs an order or tutorial cocktail.");

        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        var request = new CraftRequest { Lifetime = source };
        craftRequest = request;
        var completion = new UniTaskCompletionSource<bool>();
        bool completed = false;
        var flow = craftFlow;
        void OnBegan(CraftSession session)
        {
            if (!ReferenceEquals(craftRequest, request) || source.IsCancellationRequested || request.Session != null) return;
            if (session == null || (!string.IsNullOrEmpty(tutorialCocktailId) && session.SelectedCocktailId != tutorialCocktailId)) return;
            request.Session = session;
        }
        void OnCompleted(CraftSession session, CraftJudgement judgement)
        {
            if (ReferenceEquals(craftRequest, request) && !source.IsCancellationRequested &&
                request.Session != null && ReferenceEquals(request.Session, session))
                completion.TrySetResult(session.Phase == ECraftPhase.Completed && judgement != null);
        }
        flow.CraftBegan += OnBegan;
        flow.CraftCompleted += OnCompleted;
        try
        {
            SetCraftUiActive(true);
            if (!string.IsNullOrEmpty(tutorialCocktailId))
            {
                flow.BeginCraft(tutorialCocktailId);
                if (request.Session == null) throw new InvalidOperationException("CraftFlow did not start the requested tutorial craft.");
            }
            else
            {
                if (craftPanel == null || !craftPanel.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("The story craft menu cannot be opened; check craftPanel and craftUiRoots.");
                craftPanel.Open();
            }
            completed = await completion.Task.AttachExternalCancellation(source.Token);
            source.Token.ThrowIfCancellationRequested();
            return completed;
        }
        finally
        {
            flow.CraftBegan -= OnBegan;
            flow.CraftCompleted -= OnCompleted;
            if (ReferenceEquals(craftRequest, request))
            {
                craftRequest = null;
                if (!completed) SetCraftUiActive(false);
            }
            // B의 CancelAsync가 없어 현재는 C의 완료 대기만 취소한다.
            if (source.IsCancellationRequested && IsBusy(request.Session))
                Debug.LogWarning("[StoryCraft] Story wait cancelled, but the B craft is still active. CraftFlowController needs a session-specific CancelAsync API.");
        }
    }

    public async UniTask<CraftedDrink> WaitForServeAsync(StoryOrder order, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var view = orderView;
        if (view == null || !ReferenceEquals(view.Order, order) || order.IsServed || view.WaitingForServe)
            throw new InvalidOperationException("Serve order is missing, stale or already being served.");
        if (view.Zone == null) BuildServeZone(order);
        if (view.Zone == null) throw new InvalidOperationException("Story serve canvas or character manager is missing.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, view.Lifetime.Token, this.GetCancellationTokenOnDestroy());
        var completion = new UniTaskCompletionSource<CraftedDrink>();
        view.WaitingForServe = true;
        bool received = false;
        view.Zone.GetComponent<StoryServeDropTarget>().Bind(drink =>
        {
            if (ReferenceEquals(orderView, view) && !source.IsCancellationRequested && drink != null)
                completion.TrySetResult(drink);
        });
        try
        {
            var drink = await completion.Task.AttachExternalCancellation(source.Token);
            // 드래그 종료 처리가 끝난 뒤 트레이를 숨긴다.
            await UniTask.NextFrame(cancellationToken: source.Token);
            source.Token.ThrowIfCancellationRequested();
            received = true;
            return drink;
        }
        finally
        {
            view.WaitingForServe = false;
            RemoveServeZone(view);
            if (ReferenceEquals(orderView, view))
            {
                if (!received) CloseOrder(order);
                else SetCraftUiActive(false);
            }
        }
    }

    /// <summary>
    /// 주문자 앞에 서빙 자리를 놓는다.
    ///
    /// 좌석의 월드 좌표를 화면 좌표로 옮겨 캔버스 위에 놓는다 — 말풍선을 화자 머리 위에 맞추는 것과
    /// 같은 방식이다(UIDialogueTextView.SetBubblePosition). 좌석이 화면 어디에 잡히는지는
    /// 카메라가 정하므로 캔버스 좌표를 고정값으로 둘 수 없다.
    /// </summary>
    void BuildServeZone(StoryOrder order)
    {
        if (serveCanvas == null || characterManager == null)
        {
            Debug.LogError("[StoryCraft] serveCanvas 또는 characterManager가 비어 있어 서빙 자리를 만들지 못했습니다.");
            return;
        }

        if (orderView == null || !ReferenceEquals(orderView.Order, order)) return;
        RemoveServeZone(orderView);

        var go = new GameObject($"Story Serve Zone ({order.GuestActorId})",
            typeof(RectTransform), typeof(Image), typeof(StoryServeDropTarget));
        go.transform.SetParent(serveCanvas.transform, false);

        var rect = (RectTransform)go.transform;
        rect.sizeDelta = serveZoneSize;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = ResolveSeatCanvasPoint(order.GuestActorId);

        var image = go.GetComponent<Image>();
        image.color = showServeZoneGuide ? serveZoneColor : new Color(0f, 0f, 0f, 0f);
        // 평소에는 잔 클릭을 가리지 않고, 잔을 집은 동안에만 StoryServeDropTarget이 켠다.
        image.raycastTarget = false;

        orderView.Zone = go;
    }

    Vector2 ResolveSeatCanvasPoint(string actorId)
    {
        Vector3 world = characterManager.GetCharacterPosition(actorId) + serveZoneWorldOffset;

        Camera camera = Camera.main;
        if (camera == null) return Vector2.zero;

        Vector2 screenPoint = camera.WorldToScreenPoint(world);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)serveCanvas.transform, screenPoint, null, out Vector2 localPoint);

        return localPoint;
    }

    // ── 정산 ────────────────────────────────────────────────────────────

    public StoryResultContext CommitServe(StoryOrder order, CraftedDrink drink)
    {
        if (drink == null || order == null || order.IsServed || orderView == null ||
            !ReferenceEquals(orderView.Order, order)) return null;

        if (balanceData?.balanceData == null)
        {
            Debug.LogError("[StoryCraft] balanceData가 없어 서빙을 확정하지 못했습니다.");
            return null;
        }

        NewBalanceConfig config = balanceData.balanceData.Config;

        bool orderMatch = ServeJudge.IsOrderMatch(drink, order.OrderedCocktailId);
        ENewGrade? finalGrade = ServeJudge.ResolveFinalGrade(drink, order.OrderedCocktailId, config);

        Debug.Log($"[StoryCraft] 서빙 {order.GuestActorId} ← {drink.CocktailId} (주문 {order.OrderedCocktailId}) / " +
                  $"제조등급 {drink.CraftGrade?.ToString() ?? "채점 불가"} → 최종등급 {finalGrade?.ToString() ?? "채점 불가"}");

        if (drink.CraftGrade == null || finalGrade == null)
        {
            // 등급을 내지 못한 잔이다. 결과 문맥을 만들지 않으면 뒤의 grade 조건이 터지는데,
            // 임의의 등급으로 메우는 것보다 낫다 — 메우면 대본이 엉뚱한 가지로 조용히 흘러간다.
            Debug.LogError($"[StoryCraft] 채점하지 못한 잔이라 결과 문맥을 만들지 못했습니다: {drink.CocktailId}");
            return null;
        }

        CommitSettlement(order, finalGrade);
        order.MarkServed();
        CloseOrder(order);
        return new StoryResultContext(drink.CraftGrade.Value, finalGrade.Value, orderMatch,
                                      order.OrderedCocktailId, drink.CocktailId);
    }

    void CommitSettlement(StoryOrder order, ENewGrade? finalGrade)
    {
        if (guestManager == null || balanceData?.balanceData == null) return;

        int price = 0;
        if (cocktailData != null && cocktailData.TryGet(order.OrderedCocktailId, out NewCocktailData cocktail))
            price = cocktail.Price;

        // 팁 배수는 성격에서 오는 값인데 2부 단골에는 성격이 붙어 있지 않다. 없는 값을 지어내지 않고
        // 배수 없음(1배)으로 둔다. 단골 성격이 데이터에 생기면 그때 여기서 읽는다.
        OrderSettlement settlement = OrderSettlement.Calculate(
            $"{order.Id}_settle", $"{order.Id}_serve", finalGrade, price, 1f, balanceData.balanceData);

        if (settlement == null) return;

        guestManager.Sales.Apply(settlement);

        Debug.Log($"[StoryCraft] 정산 {order.GuestActorId} {order.OrderedCocktailId}(가격 {price}) " +
                  $"{settlement.FinalGrade} / {settlement} / 당일 누계 {guestManager.Sales.Total}");
    }

    // ── 화면 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 제조·서빙 화면을 켜고 끈다.
    ///
    /// 닫는 일을 끄기 <b>전에</b> 한다. 슬라이드 패널은 코루틴으로 움직이는데 꺼진 오브젝트에서는
    /// 코루틴이 시작조차 되지 않아서, 순서를 뒤집으면 "Coroutine couldn't be started because the
    /// game object is inactive"가 난다.
    ///
    /// 패널 자신은 craftUiRoots와 별개로 켠다. 인스펙터에서 그 목록에 빠뜨리면 제조가 통째로 막히는데,
    /// 화면에는 아무것도 나지 않아 원인이 멀다.
    /// </summary>
    void SetCraftUiActive(bool active)
    {
        if (!active && craftPanel != null && craftPanel.gameObject.activeInHierarchy) craftPanel.Close();

        if (craftUiRoots != null)
        {
            foreach (var root in craftUiRoots)
            {
                if (root != null) root.SetActive(active);
            }
        }

        if (craftPanel != null) craftPanel.gameObject.SetActive(active);
    }
}