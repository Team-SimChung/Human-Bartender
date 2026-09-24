using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;

/// <summary>
/// 타이쿤(1부) 국면의 손님 슬롯(GuestSlot) 전체를 관리한다. 랜덤 손님(random_waves.json)과
/// 단골 손님(regular_slots.json)을 같은 day의 seq 순서로 합쳐 대기열을 구성하고, 빈 슬롯에 배정한다.
/// 손님이 자리를 떠나면 TycoonFlow에 응대 완료를 알려 남은 손님 수를 갱신시킨다.
/// targetCocktailId는 cocktails.json에서 같은 tier의 칵테일 중 무작위로 배정한다.
/// TODO: personality/max_rounds/branch_choice 반영 로직 연결.
/// </summary>
public class GuestManager : MonoBehaviour
{
    [Inject] IPlayerDataWriter playerDataWriter;
    [SerializeField] NewBalanceDataSO configData;
    [SerializeField] NewRandomWaveDataSO randomWaveData;
    [SerializeField] NewRegularSlotDataSO regularSlotData;
    [SerializeField] NewCocktailDataSO cocktailData;
    [SerializeField] NewPersonalityDataSO personalityData;
    [SerializeField] NewGuestBodyDataSO guestBodyData;
    [SerializeField] NewBarkDataSO barkData;
    [SerializeField] NewTextTagDataSO textTagData;
    [SerializeField] NewCharacterDataSO characterData;
    [SerializeField] GuestSlot[] slots;

    [Header("Craft")]
    [Tooltip("제조 흐름에 들어갔는지를 알려주는 곳. 제조 중에는 손님 쪽 시간을 멈춘다. 비우면 멈추지 않는다.")]
    [SerializeField] CraftFlowController craftFlow;
    [SerializeField] OrderRequestController orderController;

    [Header("Dialogue")]
    [SerializeField] UIDialogueTextView dialogueTextView; // ask_order 등 플레이어(바텐더) 대사를 띄우는 다이얼로그 말풍선

    [Header("Serve")]
    [Tooltip("잔을 받아 든 손님이 마시기까지 기다리는 시간(초). 받자마자 맛 반응이 나오면 마시는 장면 자체가 없다. " +
             "balance.json에 해당 값이 생기면 그쪽으로 옮긴다.")]
    [SerializeField] float drinkWaitSec = 4f;

    [Tooltip("서빙 반응 대사 하나가 말풍선에 떠 있는 시간(초).")]
    [SerializeField] float serveBarkGapSec = 2.5f;

    [Header("Test")]
    [Tooltip("파츠 addressable 로딩을 생략하고 GuestSlot의 임시 오브젝트만 On/Off한다. " +
             "손님 외형에만 관여한다 — 진행 일차는 PlayPhaseController의 Test Day가 정한다.")]
    [SerializeField] bool useTempAppearance;

    const string PlayerCharacterId = "luna";

    /// <summary>serve_timeout_same_frame_priority가 이 값일 때, 같은 프레임 충돌에서 이탈을 먼저 확정한다.</summary>
    const string ServeTimeoutPriorityTimeout = "timeout";

    /// <summary>단골에게 처음 붙이는 표정. barks.json의 expression을 읽게 되면 그쪽이 이 값을 대신한다.</summary>
    const string DefaultExpression = "default";

    const float PlayerBarkDurationSec = 3f;

    /// <summary>
    /// balance.json 전체. 절대 필드에 받아 두지 않는다.
    ///
    /// 데이터 로더는 파일을 다 읽으면 SO의 balanceData에 **새 객체를 통째로 갈아 끼운다**. 그래서 로딩이
    /// 끝나기 전에 한 번 받아 둔 참조는 값이 비어 있는 옛 객체를 계속 가리키게 된다. 특히 settlement_rules는
    /// Dictionary라 Unity가 에셋에 저장하지 못해, 옛 객체에서는 통째로 null이다.
    /// </summary>
    NewBalanceDataBase Balance => configData.balanceData;

    NewBalanceConfig Config => Balance.Config;

    /// <summary>당일 매출 누계. 잔별 팝업 없이 값만 쌓아 두고, 화면 표시는 2부 종료 뒤 정산 화면 몫이다.</summary>
    [Inject] DailySales dailySales;

    BarReputation reputation;

    /// <summary>손님 쪽 시간을 재는 시계. 제조 중에는 멈춰서 코스터·서빙·생성 대기가 흐르지 않는다.</summary>
    readonly BarOperationClock barClock = new();

    GuestAppearanceBuilder appearanceBuilder;

    readonly Queue<Guest> guestQueue = new();
    readonly HashSet<Guest> releasedGuests = new();
    readonly Dictionary<GuestSlot, CancellationTokenSource> patienceCtsBySlot = new();

    /// <summary>자리별 다음 잡담(idle) 시각. BarOperationClock 기준이라 제조 중에는 차례가 오지 않는다.</summary>
    readonly Dictionary<GuestSlot, float> nextIdleChatterSecBySlot = new();

    /// <summary>상황·목소리별로 직전에 고른 대사. 같은 줄이 연달아 나오는 것을 줄이는 데 쓴다.</summary>
    readonly Dictionary<string, string> lastBarkTextByKey = new();
    CancellationTokenSource lunaBarkCts;
    CancellationTokenSource appearanceLoads = new();

    public int QueuedGuestCount => guestQueue.Count;

    /// <summary>당일 매출 누계. 매출 현황 패널이 읽기용으로 쓴다.</summary>
    public DailySales Sales => dailySales ??= new DailySales();

    /// <summary>
    /// 현재 바 평판. 처음 쓸 때 만든다 — 시작값이 balance.json에서 오는데 Awake 시점에는 아직 로딩 전일 수 있다.
    /// </summary>
    public BarReputation Reputation => reputation ??= new BarReputation(Config.ReputationStart);

    /// <summary>손님 한 명의 응대(정상 퇴장) 또는 이탈이 끝나 슬롯이 비워졌을 때 발생한다.</summary>
    public event Action<Guest> GuestReleased;

    void OnEnable()
    {
        if (appearanceLoads.IsCancellationRequested)
        {
            appearanceLoads.Dispose();
            appearanceLoads = new CancellationTokenSource();
        }
        if (craftFlow == null)
        {
            Debug.LogWarning("[Guest] craftFlow가 비어 있어 제조 중에도 손님 대기 시간이 계속 흐릅니다.");
            return;
        }

        craftFlow.CraftFlowActiveChanged += OnCraftFlowActiveChanged;
        craftFlow.AddCraftBlocker(DescribeNoOrderBlock);
    }

    void OnDisable()
    {
        appearanceLoads.Cancel();
        if (slots != null && orderController != null)
            foreach (var slot in slots)
                if (slot != null && slot.CurrentGuest?.CurrentOrder != null)
                    orderController.CancelOrder(slot.CurrentGuest.CurrentOrder.Id);

        if (craftFlow == null) return;
        craftFlow.CraftFlowActiveChanged -= OnCraftFlowActiveChanged;
        craftFlow.RemoveCraftBlocker(DescribeNoOrderBlock);
    }

    /// <summary>
    /// 잔을 받을 주문이 하나라도 있는지. 주문 대사를 말한 뒤 아직 잔을 받지 않은 손님이 곧 대기 주문이다.
    /// </summary>
    public bool HasPendingOrder => slots != null && slots.Any(slot => slot.CanReceiveDrink);

    /// <summary>
    /// 받을 주문이 없으면 새 제조를 막는 이유를 댄다(구현·검증 계약 §11.2 ORDER_CONTEXT_MISSING).
    ///
    /// 주문 없이 만든 잔은 낼 곳이 없다. 게다가 칵테일 메뉴에 들어가는 순간 손님 시간이 멈추기 때문에,
    /// 아무도 주문하지 않은 상태에서 메뉴를 열어 두면 아무 일도 일어나지 않는 채로 바가 멈춘다.
    /// </summary>
    string DescribeNoOrderBlock()
    {
        return HasPendingOrder ? null : "받을 주문이 없어 제조를 시작할 수 없습니다.";
    }

    /// <summary>대기 주문이 생기거나 사라졌을 때, 제조를 시작할 수 있는지 다시 보게 한다.</summary>
    void RefreshCraftAvailability()
    {
        craftFlow?.RefreshCraftAvailability();
    }

    void Update()
    {
        barClock.Tick(Time.deltaTime);
        TickIdleChatter();
    }

    /// <summary>
    /// 제조 흐름에 들어가면 손님 쪽 시간을 멈추고, 나오면 멈추기 직전 남은 시간부터 다시 흘린다.
    /// balance.json의 craft_pause_start = cocktail_menu_enter / craft_pause_end = return_to_table_after_serve_choice.
    /// </summary>
    void OnCraftFlowActiveChanged(bool craftFlowActive)
    {
        if (craftFlowActive) barClock.Pause();
        else barClock.Resume();

        Logger.Log($"[Guest] 바 운영 시간 {(craftFlowActive ? "정지" : "재개")} (제조 흐름 {(craftFlowActive ? "진입" : "종료")})");
    }

    /// <summary>
    /// 데이터와 슬롯을 준비한다. 당일 대기열은 여기서 만들지 않는다 — 1부가 시작될 때
    /// TycoonFlow가 BuildGuestQueue를 부른다. Start끼리는 순서가 정해져 있지 않아서, 큐를 여기서
    /// 만들면 1부가 아직 비어 있는 큐를 보고 "오늘 손님 없음"으로 판단할 수 있다.
    /// </summary>
    /// <summary>
    /// 슬롯을 준비한다. balance.json 값은 여기서 받아 두지 않는다 — 로딩이 끝나면서 객체가 새것으로
    /// 바뀌기 때문에, 쓸 때마다 Config·Balance 프로퍼티로 다시 읽는다.
    /// </summary>
    void Awake()
    {
        // 진행 일차는 여기서 손대지 않는다. 외형 스위치가 일차까지 바꾸던 탓에 "외형만 임시로 볼까"가
        // 등장 손님과 해금 칵테일을 통째로 바꾸는 일이 됐다. 일차는 국면을 여는 PlayPhaseController가 정한다.
        foreach (var slot in slots)
            slot.SetTempAppearanceMode(useTempAppearance);
    }

    /// <summary>
    /// 오늘(GameStateManager.CurrentDay) 등장할 랜덤 손님과 단골 손님을 seq 순서로 합쳐 대기열을 구성한다.
    /// </summary>
    public void BuildGuestQueue()
    {
        int day = GameStateManager.Instance.CurrentDay;
        guestQueue.Clear();
        releasedGuests.Clear();
        Sales.Reset(); // 하루가 새로 시작되므로 매출 누계도 비운다.

        var randomGuests = randomWaveData.randomWaveData
            .Where(wave => wave.Day == day)
            .Select(wave => (wave.Seq, Guest: CreateFromRandomWave(wave)));

        var regularGuests = regularSlotData.regularSlotData
            .Where(slot => slot.Day == day)
            .Select(slot => (slot.Seq, Guest: CreateFromRegularSlot(slot)));

        foreach (var entry in randomGuests.Concat(regularGuests).OrderBy(entry => entry.Seq))
            guestQueue.Enqueue(entry.Guest);
    }

    /// <summary>대기열에서 다음 손님을 꺼낸다. 대기열이 비어있으면 false.</summary>
    public bool TryDequeueNextGuest(out Guest guest)
    {
        return guestQueue.TryDequeue(out guest);
    }

    /// <summary>
    /// 대기열의 손님을 순서대로 등장시킨다(1부 일반 손님 운영 런타임 명세 §3.2·3.3).
    ///
    /// 첫 손님은 first_spawn_delay_sec를 기다린 뒤, 자기 delay_sec가 더 있으면 그만큼 더 기다린다.
    /// 둘째부터는 앞 손님이 들어온 시점을 기준으로 각자의 delay_sec를 센다.
    ///
    /// 지연이 끝났는데 자리가 없으면 그 자리에서 기다린다(SPAWN_BLOCKED). 자리가 비어도 곧바로
    /// 앉히지 않고 reseat_delay_sec를 한 번 더 기다린다 — 앞 손님이 나가는 연출과 다음 손님이
    /// 들어오는 연출이 겹치지 않게 하는 간격이다. 대기 순서는 바꾸지 않는다.
    /// </summary>
    public async UniTask RunSpawnLoopAsync(CancellationToken token)
    {
        bool isFirstGuest = true;

        while (TryDequeueNextGuest(out Guest guest))
        {
            // 생성 지연도 바 운영 시간으로 센다. 제조 중에 흘려 보내면 잔 하나를 만들고 나올 때마다
            // 기다리던 손님이 한꺼번에 들어온다.
            if (isFirstGuest && !await barClock.WaitAsync(Config.FirstSpawnDelaySec, token)) return;
            isFirstGuest = false;

            if (!await barClock.WaitAsync(guest.delaySec, token)) return;

            // 자리가 없어 기다린 시간은 지연시간에 넣지 않는다. 넣으면 자리가 나는 순간
            // 밀려 있던 손님이 한꺼번에 들어온다.
            if (IsAllSlotsOccupied())
            {
                await UniTask.WaitUntil(() => !IsAllSlotsOccupied(), cancellationToken: token);

                if (!await barClock.WaitAsync(Config.ReseatDelaySec, token)) return;
            }

            TrySeatGuest(guest);
        }
    }

    /// <summary>
    /// 단골(카메오)이 앉으면 2부 대화와 같은 캐릭터 체계로 그림을 붙인다(운영 명세 §4.1).
    ///
    /// 랜덤 손님처럼 파츠를 조합하지 않는다. 그 캐릭터는 표정과 애니메이션까지 딸린 한 벌이고,
    /// 그 한 벌을 다루는 곳이 이미 2부에 있다 — 여기서 다시 만들면 같은 캐릭터가 1부와 2부에서
    /// 다르게 보이기 시작한다.
    ///
    /// 표정은 우선 기본값으로 둔다. barks.json의 expression을 읽어 상황마다 바꾸는 것은 아직 미구현이다.
    /// </summary>
    void SetSlotCharacter(GuestSlot slot, Guest guest)
    {
        if (!guest.isRegular) return;

        if (slot.CharacterView == null)
        {
            Debug.LogError($"[Guest] {slot.name}에 GuestCharacterView가 없어 단골 '{guest.characterId}'의 " +
                           "그림을 붙이지 못했습니다.");
            return;
        }

        slot.CharacterView.SetCharacterAsync(guest.characterId, DefaultExpression).Forget();
    }

    /// <summary>단골이 앉아 있던 자리를 비운다. 랜덤 손님 자리는 파츠 렌더러 쪽에서 정리한다.</summary>
    void ClearSlotCharacter(GuestSlot slot, Guest guest)
    {
        if (guest == null || !guest.isRegular || slot.CharacterView == null) return;

        slot.CharacterView.Clear();
    }

    /// <summary>슬롯이 하나도 비어있지 않은지 확인한다.</summary>
    bool IsAllSlotsOccupied()
    {
        return slots.All(s => !s.IsEmpty);
    }

    Guest CreateFromRandomWave(NewRandomWaveData wave)
    {
        var (tipMult, patienceMult, thinkChance) = GetPersonalityValues(wave.Personality);

        var guest = new Guest
        {
            id = $"random_{wave.Day}_{wave.Seq}",
            targetCocktailId = ResolveOrderCocktailId(wave.Order),
            specifiedOrder = wave.Order,
            personality = wave.Personality,
            tipMultiplier = tipMult,
            patienceMultiplier = patienceMult,
            thinkChance = thinkChance,
            maxRounds = wave.MaxRounds,
            delaySec = wave.DelaySec,
            isRegular = false,
        };

        // 외형은 여기서 정하지 않는다. 좌석이 확보되는 시점에 정해야(운영 명세 §3.3) 그때 앉아 있는
        // 손님과 겹치는 조합을 피할 수 있다. 대기열에 들어갈 때 미리 뽑으면 비교할 대상이 아직 없다.
        return guest;
    }

    /// <summary>
    /// 자리에 앉는 순간 외형을 정하고 불러오기를 시작한다(운영 명세 §3.3, 외형 명세 §3.2).
    /// 단골은 대상이 아니다 — 그쪽은 캐릭터 전용 리소스를 쓴다.
    /// </summary>
    void PrepareAppearance(Guest guest)
    {
        if (guest.isRegular || guest.appearance != null) return;

        guest.appearance = PickRandomAppearance(guest.personality);

        if (guest.appearance == null)
        {
            Debug.LogError($"[Guest] {guest.id}의 외형을 만들지 못했습니다. guest_bodies.json을 확인하세요.");
            return;
        }

        Logger.Log($"[Guest] {guest.id} 외형 ({guest.appearance.Gender}): {guest.appearance}");

        if (!useTempAppearance)
            LoadAppearanceAsync(guest).Forget();
    }

    /// <summary>
    /// 랜덤 손님의 외형을 뽑는다(일반 손님 외형 생성 시스템 §3).
    ///
    /// 성별을 먼저 반반으로 정하고, 그 성별·성격으로 쓸 수 있는 완성 조합에서 하나를 고른다.
    /// 조합을 만들고 금지 규칙을 거르는 일은 GuestAppearanceBuilder가 한다.
    ///
    /// 지금 앉아 있는 손님과 똑같은 조합은 피한다. 세 자리뿐이라 같은 얼굴이 나란히 앉으면
    /// 바로 눈에 띈다.
    /// </summary>
    GuestBodyAppearance PickRandomAppearance(string personality)
    {
        // 성별 비율은 데모 기준 반반이다. 일차나 시간대로 달라져야 하면 Config로 뺀다(§8).
        string gender = UnityEngine.Random.value < 0.5f ? "m" : "f";

        GuestBodyAppearance appearance = AppearanceBuilder.Pick(gender, personality, SeatedAppearances());

        if (appearance != null) return appearance;

        // 조합을 고르지 못해도 손님은 앉아야 한다. 기본 조합으로 되돌리고 넘어간다(§5).
        Debug.LogWarning($"[Guest] 외형을 고르지 못해 성별 {gender}의 기본 조합을 씁니다.");

        return AppearanceBuilder.BuildDefault(gender);
    }

    /// <summary>지금 앉아 있는 랜덤 손님들의 외형. 겹치는 조합을 피하는 데 쓴다(§3.2).</summary>
    IEnumerable<GuestBodyAppearance> SeatedAppearances()
    {
        foreach (var slot in slots)
        {
            var appearance = slot.CurrentGuest?.appearance;
            if (appearance != null) yield return appearance;
        }
    }

    /// <summary>
    /// 조합을 만들어 두는 곳. 처음 쓸 때 만든다 — guest_bodies.json과 balance.json이 모두
    /// 로드된 뒤여야 하는데, Awake 시점에는 아직 아닐 수 있다.
    /// </summary>
    GuestAppearanceBuilder AppearanceBuilder =>
        appearanceBuilder ??= new GuestAppearanceBuilder(guestBodyData.guestBodyData, Config.GuestAccNoneWeight);

    /// <summary>
    /// 배정된 파츠를 addressable로 불러온다.
    ///
    /// 필수 슬롯이 하나라도 비면 조합 전체를 성별 기본 조합으로 바꾼다(§5). 빠진 슬롯 하나만
    /// 갈아 끼우지 않는 이유는, 새로 끼운 파츠가 나머지와 금지 조합을 이룰 수 있어서다.
    /// </summary>
    async UniTaskVoid LoadAppearanceAsync(Guest guest)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            appearanceLoads.Token, this.GetCancellationTokenOnDestroy());
        var token = linked.Token;
        GuestBodySprites sprites = null;
        try
        {
            sprites = await LoadSpritesAsync(guest.appearance, token);
            if (!sprites.HasRequiredSlots(guest.appearance))
            {
                Debug.LogWarning($"[Guest] {guest.id}의 파츠를 불러오지 못해 성별 {guest.appearance.Gender}의 " +
                                 "기본 조합으로 바꿉니다.");

                sprites.Release();
                sprites = null;

                GuestBodyAppearance fallback = AppearanceBuilder.BuildDefault(guest.appearance.Gender);

                if (fallback == null)
                {
                    Debug.LogError($"[Guest] {guest.id}의 기본 조합도 만들지 못해 자리에 아무 그림도 나오지 않습니다.");
                    return;
                }

                guest.appearance = fallback;
                sprites = await LoadSpritesAsync(fallback, token);
            }
            token.ThrowIfCancellationRequested();
            if (!slots.Any(slot => slot != null && slot.CurrentGuest == guest)) return;
            guest.bodySprites = sprites;
            sprites = null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally { sprites?.Release(); }
    }

    /// <summary>조합의 모든 슬롯을 한꺼번에 불러온다.</summary>
    async UniTask<GuestBodySprites> LoadSpritesAsync(GuestBodyAppearance appearance, CancellationToken token)
    {
        var slotOrder = new List<EGuestBodySlot>();
        var loads = new List<UniTask<AsyncOperationHandle<Sprite>?>>();

        Exception loadError = null;
        foreach (var slot in NewGuestBodyDataBase.AllSlots)
        {
            if (!appearance.TryGet(slot, out var part)) continue;

            slotOrder.Add(slot);
            loads.Add(LoadPartAsync(part.Sprite));
        }

        var handles = await UniTask.WhenAll(loads);
        if (token.IsCancellationRequested || loadError != null)
        {
            foreach (var candidate in handles) { var handle = candidate; ResourceLoader.ReleaseHandle(ref handle); }
            if (loadError != null) throw loadError;
            throw new OperationCanceledException(token);
        }

        var sprites = new GuestBodySprites();
        for (int i = 0; i < slotOrder.Count; i++)
            sprites.Set(slotOrder[i], handles[i]);

        return sprites;

        async UniTask<AsyncOperationHandle<Sprite>?> LoadPartAsync(string address)
        {
            try { return await ResourceLoader.TryLoadAsync<Sprite>(address, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return null; }
            catch (Exception error) { loadError ??= error; return null; }
        }
    }

    Guest CreateFromRegularSlot(NewRegularSlotData slot)
    {
        // 단골은 personalities.json을 참조하지 않아 팁 배율과 인내심 배율이 모두 1이다.
        // 배율만 없을 뿐 기다리는 것은 랜덤 손님과 같아서, 코스터·서빙 타이머는 그대로 돈다.
        var guest = new Guest
        {
            id = slot.Character,
            characterId = slot.Character,
            targetCocktailId = ResolveOrderCocktailId(slot.Order),
            specifiedOrder = slot.Order,
            tipMultiplier = 1f,
            maxRounds = slot.MaxRounds,
            delaySec = slot.DelaySec,
            isRegular = true,
        };

        return guest;
    }

    /// <summary>
    /// 이 손님이 주문할 칵테일을 정한다.
    ///
    /// 웨이브 데이터에 주문이 적혀 있으면 그대로 쓴다. 스토리상 특정 칵테일을 주문해야 하는 손님이
    /// 그렇게 지정되어 있다. 비어 있으면 오늘 만들 수 있는 칵테일 중에서 하나를 무작위로 고른다.
    /// </summary>
    string ResolveOrderCocktailId(string order)
    {
        if (!string.IsNullOrEmpty(order))
        {
            // 지정된 주문은 오늘 해금되지 않았더라도 그대로 따른다. 스토리가 요구한 주문이라
            // 시스템이 임의로 다른 칵테일로 바꾸면 그 장면이 성립하지 않는다.
            if (cocktailData.TryGet(order, out _)) return order;

            Logger.Log($"[Guest] 지정된 주문 '{order}'을 cocktails.json에서 찾지 못했습니다.");
        }

        return PickRandomAvailableCocktailId();
    }

    /// <summary>
    /// 오늘 기준으로 제조 가능한 칵테일 중 하나를 무작위로 고른다.
    /// 데이터가 아직 확정되지 않은(status=tbd) 칵테일은 주문에 넣지 않는다 — 레시피가 비어 있어
    /// 만들 방법이 없기 때문이다.
    /// </summary>
    string PickRandomAvailableCocktailId()
    {
        int day = GameStateManager.Instance.CurrentDay;

        var candidates = cocktailData.cocktailData
            .Where(c => c.UnlockDay <= day && c.Status == ENewDataStatus.Confirmed)
            .ToArray();

        if (candidates.Length == 0)
        {
            Logger.Log($"[Guest] {day}일차에 주문할 수 있는 칵테일이 없습니다.");
            return null;
        }

        return candidates[UnityEngine.Random.Range(0, candidates.Length)].Id;
    }

    /// <summary>
    /// personalities.json에서 personalityId와 일치하는 tip_mult/patience_mult/think_chance를 찾는다.
    /// 찾지 못하면 전부 1.0 — 팁·인내심을 그대로 두고 주문 고민 대사는 늘 나오는 쪽으로 둔다.
    /// </summary>
    (float tipMult, float patienceMult, float thinkChance) GetPersonalityValues(string personalityId)
    {
        if (!string.IsNullOrEmpty(personalityId))
        {
            foreach (var p in personalityData.personalityData)
            {
                if (p.Id == personalityId)
                    return (p.TipMult, p.PatienceMult, p.ThinkChance);
            }
        }

        return (1f, 1f, 1f);
    }

    /// <summary>비어있는 슬롯을 찾아 손님을 배정한다. 자리가 없으면 false를 반환한다.</summary>
    public bool TrySeatGuest(Guest guest)
    {
        GuestSlot slot = slots.FirstOrDefault(s => s.IsEmpty);
        if (slot == null) return false;

        PrepareAppearance(guest);

        slot.Seat(guest);
        SetSlotCharacter(slot, guest);
        ShowBark(slot, "call");

        StartPatienceTimer(slot, guest);

        return true;
    }

    /// <summary>
    /// 슬롯의 인내 타이머를 시작한다. 이미 돌고 있던 타이머가 있으면(재착석 등) 먼저 멈추고 새로 시작한다.
    /// </summary>
    void StartPatienceTimer(GuestSlot slot, Guest guest)
    {
        WatchPatienceAsync(slot, guest, RegisterSlotTimer(slot)).Forget();
    }

    /// <summary>
    /// 이 슬롯에 대해 진행 중이던 타이머(인내심/서빙 대기 등)를 멈추고, 새 타이머 하나가 쓸 CancellationToken을 등록한다.
    /// GuestManager가 파괴될 때(this.GetCancellationTokenOnDestroy)도 함께 취소된다.
    /// </summary>
    CancellationToken RegisterSlotTimer(GuestSlot slot)
    {
        StopPatienceTimer(slot);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        patienceCtsBySlot[slot] = cts;
        return cts.Token;
    }

    /// <summary>
    /// 진행 중인 슬롯 타이머(인내심 대기/서빙 대기)를 중간에 멈춘다. 예: 코스터를 받아 다음 단계로 넘어가거나,
    /// 응대가 끝나 더 이상 이탈 카운트다운이 필요 없어졌을 때 호출한다. 돌고 있는 타이머가 없으면 아무것도 하지 않는다.
    /// </summary>
    public void StopPatienceTimer(GuestSlot slot)
    {
        if (!patienceCtsBySlot.Remove(slot, out var cts)) return;

        cts.Cancel();
        cts.Dispose();
    }

    /// <summary>
    /// 손님이 착석한 시점을 기준으로 coaster_base_sec에 손님의 patience_mult를 곱한 시간(전체 인내 시간)
    /// 동안 call_urge/call_final/leave_coaster 흐름을 진행한다.
    ///
    /// 성격 배율을 곱한 뒤 coaster_min_sec ~ coaster_max_sec로 자른다. 배율이 아무리 커도 한 손님이
    /// 무한정 기다리지 않고, 아무리 작아도 반응할 틈은 남는다.
    /// </summary>
    async UniTaskVoid WatchPatienceAsync(GuestSlot slot, Guest guest, CancellationToken token)
    {
        float patienceSec = Mathf.Clamp(Config.CoasterBaseSec * guest.patienceMultiplier,
                                        Config.CoasterMinSec, Config.CoasterMaxSec);
        await WatchLeaveTimerAsync(slot, guest, patienceSec, "call_urge", "call_final", "leave_coaster",
                                   Config.LeaveCoasterRep, token);
    }

    /// <summary>
    /// totalSec을 warn_yellow_ratio/warn_red_ratio 지점으로 나눠 각각 urgeBark/finalBark 대사를 띄우고, 전체 시간이
    /// 다 지날 때까지도 같은 손님이 그대로면(=처리되지 않았으면) leaveBark 대사와 함께 이탈시킨다.
    /// 대기 도중 슬롯이 비워지거나, 다른 손님으로 교체됐거나, StopPatienceTimer로 취소되면 그 시점에서 멈춘다.
    /// </summary>
    async UniTask WatchLeaveTimerAsync(GuestSlot slot, Guest guest, float totalSec, string urgeBark, string finalBark,
                                      string leaveBark, int reputationDelta, CancellationToken token)
    {
        float yellowSec = totalSec * Config.WarnYellowRatio;
        float redSec = totalSec * Config.WarnRedRatio;

        if (!await WaitWhileSeatedAsync(slot, guest, yellowSec, token)) return;
        ShowBark(slot, urgeBark);

        if (!await WaitWhileSeatedAsync(slot, guest, redSec - yellowSec, token)) return;
        ShowBark(slot, finalBark);

        if (!await WaitWhileSeatedAsync(slot, guest, totalSec - redSec, token)) return;

        const float leaveBarkDurationSec = 3f;
        ShowBark(slot, leaveBark, leaveBarkDurationSec);
        slot.MarkLeaving();

        // 응대하지 못하고 돌려보낸 손님이라 평판이 깎인다(balance.json의 leave_coaster_rep / leave_serve_rep).
        Reputation.Add(reputationDelta, $"{guest.id} {leaveBark}");

        // 손님이 바로 사라지지 않고, leave 말풍선이 떠있는 동안(leaveBarkDurationSec)은 자리에 남아있다가 그 뒤에 퇴장한다.
        await UniTask.Delay(TimeSpan.FromSeconds(leaveBarkDurationSec), cancellationToken: this.GetCancellationTokenOnDestroy());

        ReleaseGuest(slot);
    }

    /// <summary>
    /// 바 운영 시간으로 delaySec만큼 기다린 뒤, 도중에 취소되지 않았고 슬롯에 여전히 같은 손님이
    /// 앉아있으면 true. 대기 중 StopPatienceTimer 등으로 취소되면 false를 반환한다.
    ///
    /// 손님 쪽 기다림은 전부 이 창구를 지난다. 제조 중에 시계가 멈추면 여기 걸려 있는 모든 대기가
    /// 같이 멈춘다 — 코스터 인내심, 서빙 제한시간, 주문 대사 간격이 한꺼번에 정지한다.
    /// </summary>
    async UniTask<bool> WaitWhileSeatedAsync(GuestSlot slot, Guest guest, float delaySec, CancellationToken token)
    {
        if (delaySec > 0f && !await barClock.WaitAsync(delaySec, token)) return false;

        return !token.IsCancellationRequested && slot.CurrentGuest == guest;
    }

    /// <summary>
    /// barks.json에서 situation과 손님의 personality(voice_id)가 모두 일치하는 대사 중 weight를 가중치로 하나를 뽑아
    /// 슬롯의 말풍선에 띄운다. personality가 일치하는 대사가 없으면 voice_id가 없는(공용) 대사 중에서 고른다.
    /// 그마저 없으면 아무것도 하지 않는다.
    /// </summary>
    public void ShowBark(GuestSlot slot, string situation, float durationSec = 3f)
    {
        Guest guest = slot.CurrentGuest;
        string voiceId = guest == null ? null : guest.isRegular ? guest.characterId : guest.personality;

        // 말을 하려 한 것만으로 잡담은 뒤로 밀린다. 대사가 없어 아무 말도 못 하더라도 마찬가지다 —
        // 그러지 않으면 대사가 비어 있는 상황에서 매 프레임 잡담 차례가 돌아온다.
        ScheduleNextIdleChatter(slot);

        var situationBarks = barkData.barkData.Where(b => b.Situation == situation).ToArray();
        if (situationBarks.Length == 0) return;

        var candidates = situationBarks.Where(b => b.VoiceId == voiceId).ToArray();

        // 카메오(단골)는 공용 폴백을 쓰지 않는다(운영 명세 §6.1). 그 캐릭터의 말투가 아닌 대사를
        // 그 캐릭터가 말하면 누구인지가 흐려진다. 전용 대사가 없으면 아무 말도 하지 않는다.
        if (candidates.Length == 0 && guest != null && guest.isRegular) return;

        if (candidates.Length == 0)
            candidates = situationBarks.Where(b => string.IsNullOrEmpty(b.VoiceId)).ToArray();

        if (candidates.Length == 0) return;

        NewBarkData bark = PickBarkAvoidingRepeat($"{situation}/{voiceId}", candidates);

        string cocktailName = GetCocktailName(guest?.targetCocktailId);
        string text = DialogueTypingService.ApplyCustomTags(bark.Text.Ko, textTagData, cocktailName);
        slot.ShowBark(text, durationSec);
    }

    // ── 잡담 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 잡담(idle) 차례가 된 손님 한 명에게 말을 시킨다(운영 명세 §7.3).
    ///
    /// 한 프레임에 한 명만 내보낸다. 여러 자리의 차례가 겹치면 좌석 순서(L→M→R)로 하나씩 나가고
    /// 나머지는 다음 프레임을 기다린다 — 세 자리에서 말풍선이 동시에 뜨면 무엇을 읽어야 할지 알 수 없다.
    ///
    /// 대사·연출 중에는 차례가 오지 않는다. 어떤 대사든 ShowBark를 지나면서 다음 잡담 시각을 뒤로
    /// 밀기 때문이다. 제조 중에는 시계 자체가 멈춰 있다.
    /// </summary>
    void TickIdleChatter()
    {
        if (barClock.IsPaused || slots == null) return;

        foreach (var slot in slots)
        {
            if (!CanChatter(slot)) continue;
            if (!nextIdleChatterSecBySlot.TryGetValue(slot, out float nextSec)) continue;
            if (barClock.ElapsedSec < nextSec) continue;

            ShowBark(slot, "idle"); // 여기서 다음 잡담 시각도 다시 뽑힌다.
            return;
        }
    }

    /// <summary>
    /// 잡담을 할 수 있는 자리인지. 앉아서 기다리는 중일 때만 한다 —
    /// 들어오는 중(Coming)이거나 떠나는 중(Leaving)인 손님은 그 연출을 하고 있다.
    /// </summary>
    static bool CanChatter(GuestSlot slot)
    {
        return slot.CurrentGuest != null &&
               (slot.CurrentState == EGuestState.Sit || slot.CurrentState == EGuestState.WaitinOrder);
    }

    /// <summary>이 자리의 다음 잡담 시각을 idle_min_sec ~ idle_max_sec 사이에서 새로 뽑는다.</summary>
    void ScheduleNextIdleChatter(GuestSlot slot)
    {
        nextIdleChatterSecBySlot[slot] =
            barClock.ElapsedSec + UnityEngine.Random.Range(Config.IdleMinSec, Config.IdleMaxSec);
    }

    /// <summary>
    /// 후보 중 하나를 가중 추첨하되, 같은 상황·같은 목소리에서 직전에 나온 줄은 한 번 빼고 뽑는다
    /// (운영 명세 §6.1). 뺀 뒤 남는 후보가 없으면 — 후보가 하나뿐이면 — 중복을 그대로 허용한다.
    /// </summary>
    NewBarkData PickBarkAvoidingRepeat(string key, NewBarkData[] candidates)
    {
        if (candidates.Length > 1 && lastBarkTextByKey.TryGetValue(key, out string lastText))
        {
            var fresh = candidates.Where(b => b.Text.Ko != lastText).ToArray();
            if (fresh.Length > 0) candidates = fresh;
        }

        NewBarkData picked = PickWeightedBark(candidates);
        lastBarkTextByKey[key] = picked.Text.Ko;

        return picked;
    }

    /// <summary>cocktails.json에서 cocktailId에 해당하는 이름(한글)을 찾는다. 없으면 null.</summary>
    string GetCocktailName(string cocktailId)
    {
        if (string.IsNullOrEmpty(cocktailId)) return null;

        var cocktail = cocktailData.cocktailData.FirstOrDefault(c => c.Id == cocktailId);
        return cocktail.Id != null ? cocktail.Name.Ko : null;
    }

    static NewBarkData PickWeightedBark(NewBarkData[] barks)
    {
        int totalWeight = barks.Sum(b => b.Weight);
        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (var bark in barks)
        {
            cumulative += bark.Weight;
            if (roll < cumulative) return bark;
        }

        return barks[^1];
    }

    /// <summary>
    /// 슬롯 앞에 코스터가 놓였을 때 호출한다(CoasterDropZone에서 드롭 판정 후 호출).
    /// 손님이 코스터를 기다리는 상태(CanReceiveCoaster)일 때만 인내심 타이머를 멈추고 주문 대기 상태로 전환한다.
    /// 이미 주문 대기/이탈 상태이거나 손님이 없으면 아무것도 하지 않고 false를 반환한다.
    /// </summary>
    public bool TryPlaceCoaster(GuestSlot slot)
    {
        if (!slot.CanReceiveCoaster) return false;

        Logger.Log("Set Coaster");

        slot.MarkWaitingOrder();

        var token = RegisterSlotTimer(slot);
        ShowPlayerAskOrderBark(slot.CurrentGuest);
        WatchOrderAsync(slot, slot.CurrentGuest, token).Forget();
        return true;
    }

    /// <summary>
    /// 주문 대기 상태가 된 뒤 3초 후 order_think, 그로부터 다시 3초 후 order 대사를 띄운다.
    /// order 대사 출력 후에는 곧바로 서빙 대기(WatchServeAsync)로 이어진다.
    /// 대기 도중 슬롯이 비워지거나 다른 손님으로 바뀌면(=먼저 응대/이탈됨) 멈춘다.
    /// </summary>
    async UniTaskVoid WatchOrderAsync(GuestSlot slot, Guest guest, CancellationToken token)
    {
        const float orderThinkDelaySec = 3f;
        const float orderDelaySec = 3f;

        if (!await WaitWhileSeatedAsync(slot, guest, orderThinkDelaySec, token)) return;

        // 주문을 고민하는 대사는 성격이 정한 확률로만 나온다(구현·검증 계약 §8.2). 추첨에 실패하면
        // 고민 없이 바로 시키는 손님이 된다. 대사만 건너뛰고 주문까지의 간격은 그대로 둔다.
        if (UnityEngine.Random.value < guest.thinkChance)
            ShowBark(slot, "order_think");

        if (!await WaitWhileSeatedAsync(slot, guest, orderDelaySec, token)) return;
        ShowBark(slot, "order");
        guest.orderRound = 1;
        RegisterGuestOrder(slot, guest);
        RefreshCraftAvailability();

        await WatchServeAsync(slot, guest, token);
    }

    /// <summary>
    /// 주문(order) 대사가 나간 뒤 시작되는 서빙 대기 타이머. 손님이 주문한 칵테일의 제조 제한시간에
    /// 여유를 더한 시간 동안 serve_urge/serve_final 경고를 띄우고, 시간이 다 지나면 leave_serve 대사와
    /// 함께 이탈시킨다.
    /// </summary>
    async UniTask WatchServeAsync(GuestSlot slot, Guest guest, CancellationToken token)
    {
        float serveSec = ComputeServeTimeoutSec(guest);

        // 마감 시각을 남겨 둔다. 드롭이 같은 프레임에 들어왔을 때 이 값으로 선후를 가른다(TryServeDrink).
        guest.serveDeadlineSec = barClock.ElapsedSec + serveSec;

        await WatchLeaveTimerAsync(slot, guest, serveSec, "serve_urge", "serve_final", "leave_serve",
                                   Config.LeaveServeRep, token);
    }

    /// <summary>
    /// 서빙 제한 시간 = 주문한 칵테일의 time_limit_sec + 여유.
    ///
    /// 여유의 기준은 진행 일차가 아니라 그 칵테일의 해금 일차(unlock_day)다. 나중에 풀리는 칵테일일수록
    /// 여유가 적고, 같은 칵테일은 며칠째든 늘 같은 여유를 받는다. serve_bonus_sec에서 해금 일차만큼
    /// serve_grace_per_day_sec를 깎고, serve_min_bonus_sec 아래로는 내려가지 않는다.
    ///
    /// 1부 일반 손님 운영 런타임 명세 §7.2:
    ///   serve_bonus = max(serve_min_bonus_sec, serve_bonus_sec - cocktail.unlock_day × serve_grace_per_day_sec)
    ///
    /// 진행 일차(CurrentDay)로 계산하면 "날이 갈수록 모든 칵테일이 빡빡해진다"가 되어 의미가 달라진다.
    /// 성격의 patience_mult는 코스터 대기에만 적용하고 서빙 대기에는 넣지 않는다(같은 절).
    /// </summary>
    float ComputeServeTimeoutSec(Guest guest)
    {
        var cocktail = cocktailData.cocktailData.FirstOrDefault(c => c.Id == guest.targetCocktailId);
        float timeLimitSec = cocktail.Id != null ? cocktail.TimeLimitSec : 0f;
        int unlockDay = cocktail.Id != null ? cocktail.UnlockDay : 0;

        float serveBonus = Config.ServeBonusSec - unlockDay * Config.ServeGracePerDaySec;

        return timeLimitSec + Mathf.Max(Config.ServeMinBonusSec, serveBonus);
    }

    // ── 서빙 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 완성한 잔을 이 자리의 손님에게 낸다(CoasterDropZone에서 드롭 판정 후 호출).
    /// 주문 대사를 말한 손님만 잔을 받는다. 받아들여지면 서빙 대기 타이머를 멈추고 반응 연출로 넘어간다.
    /// </summary>
    public bool TryServeDrink(GuestSlot slot, CraftedDrink drink)
    {
        if (slot == null || !slot.CanReceiveDrink || orderController == null) return false;
        var guest = slot.CurrentGuest;
        if (HasServeTimedOut(guest)) return false;
        if (!orderController.TryServe(guest.CurrentOrder.Id, drink)) return false;
        guest.serveDeadlineSec = 0f;
        StopPatienceTimer(slot);
        slot.MarkLeaving();
        RefreshCraftAvailability();
        return true;
    }

    // 주문 대사 확정/재주문 시 호출한다. 상태는 guest.CurrentOrder에서만 읽는다.
    void RegisterGuestOrder(GuestSlot slot, Guest guest)
    {
        if (orderController == null) throw new InvalidOperationException("OrderRequestController 연결이 필요합니다.");
        var details = new OrderDetails($"{guest.id}_r{guest.orderRound}", guest.id,
            guest.targetCocktailId, guest.tipMultiplier);
        guest.CurrentOrder = orderController.Request(details,
            result =>
            {
                if (this == null || !isActiveAndEnabled || slot.CurrentGuest != guest) return;
                RefreshCraftAvailability();
                if (result.Completed)
                {
                    guest.settlements.Add(result.Serve.Settlement);
                    ContinueAfterServeAsync(slot, guest, result.Serve, RegisterSlotTimer(slot)).Forget();
                }
                else if (result.State == OrderState.Failed)
                    Debug.LogError(result.Error);
            },
            new GuestOrderPresentation(this),
            (result, token) => ReactToDrinkAsync(slot, guest, result, token),
            () => this != null && isActiveAndEnabled && slot.CurrentGuest == guest &&
                  slot.CanReceiveDrink && !HasServeTimedOut(guest));
    }

    sealed class GuestOrderPresentation : IOrderPresentation
    {
        readonly GuestManager owner;
        public GuestOrderPresentation(GuestManager owner) => this.owner = owner;
        public void Open(Func<CraftedDrink, bool> receive) { }
        public void Unbind() { }
        public UniTask CloseAsync()
        {
            if (owner != null) owner.RefreshCraftAvailability();
            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// 이 손님의 서빙 제한시간이 이미 지났는지.
    ///
    /// 유효한 드롭과 인내심 종료가 같은 프레임에 겹치면 balance.json의
    /// serve_timeout_same_frame_priority = timeout 에 따라 이탈을 먼저 확정한다(운영 명세 §7.2).
    /// 타이머 쪽 대기가 아직 깨어나지 않아 손님이 자리에 남아 있어도, 시각으로 보면 지난 뒤이므로
    /// 여기서 막는다. 잔은 서빙으로 쓰이지 않고 트레이로 되돌아가며 정산도 만들지 않는다.
    ///
    /// 설정이 timeout이 아니면 이 판정을 하지 않는다 — 우선순위를 코드가 정하지 않는다.
    /// </summary>
    bool HasServeTimedOut(Guest guest)
    {
        if (Config.ServeTimeoutSameFramePriority != ServeTimeoutPriorityTimeout) return false;

        return guest.serveDeadlineSec > 0f && barClock.ElapsedSec >= guest.serveDeadlineSec;
    }

    /// <summary>
    /// 잔을 받은 손님의 반응. 받아드는 대사 → (마시는 동안 기다림) → 맛 반응 → 작별 순으로 띄우고 자리를 비운다.
    ///
    /// 받아드는 대사와 맛 반응 사이에 drinkWaitSec만큼 아무 말도 없는 시간을 둔다. 잔을 놓자마자 맛 반응이
    /// 나오면 손님이 마시는 장면 없이 결과만 뜨고, 서빙이 잔을 옮기는 일이 아니라 버튼 하나가 된다.
    ///
    /// 주문과 다른 잔이면 받아드는 대사부터 갈라진다(wrong_receive → wrong_drink).
    /// 채점하지 못한 잔은 맛 반응을 건너뛴다 — 등급을 임의로 만들어 반응을 고르지 않는다.
    /// </summary>
    async UniTask ReactToDrinkAsync(GuestSlot slot, Guest guest, ServeResult result, CancellationToken token)
    {
        ShowBark(slot, result.OrderMatch ? "serve_thanks" : "wrong_receive", serveBarkGapSec);
        await WaitForReactionAsync(slot, guest, serveBarkGapSec, token);
        await WaitForReactionAsync(slot, guest, drinkWaitSec, token);
        ShowBark(slot, result.OrderMatch ? ServeJudge.ReactSituation(result.FinalGrade) : "wrong_drink",
            serveBarkGapSec);
        await WaitForReactionAsync(slot, guest, serveBarkGapSec, token);
        // 반환 후 OrderRequestController가 정산하고 최종 콜백을 호출한다.
    }

    async UniTask WaitForReactionAsync(GuestSlot slot, Guest guest, float seconds, CancellationToken token)
    {
        if (!await WaitWhileSeatedAsync(slot, guest, seconds, token))
            throw new OperationCanceledException(token);
    }

    // 최종 콜백의 호출 대상. 장부 반영은 이미 끝났으며 후속 손님 처리만 한다.
    async UniTask ContinueAfterServeAsync(GuestSlot slot, Guest guest, ServeResult result, CancellationToken token)
    {
        if (CanReorder(guest, result.Settlement))
        {
            await StartNextOrderAsync(slot, guest, token);
            return;
        }
        ShowBark(slot, result.OrderMatch && ServeJudge.IsSatisfied(result.FinalGrade) ? "bye_good" : "bye_bad",
            serveBarkGapSec);
        if (await WaitWhileSeatedAsync(slot, guest, serveBarkGapSec, token)) ReleaseGuest(slot);
    }

    /// <summary>
    /// 다음 잔을 더 시킬 수 있는지(§6.4.1).
    ///
    ///   can_reorder = remaining_order_count > 0 AND refund_amount == 0
    ///
    /// 지금 규칙에서 배상이 나오는 것은 Sewage뿐이라, 하수구 같은 잔을 받은 손님은 남은 주문을
    /// 취소하고 나간다. 정산하지 못한 잔은 판단 근거가 없으므로 더 시키지 않는다.
    /// </summary>
    static bool CanReorder(Guest guest, OrderSettlement settlement)
    {
        return settlement != null && settlement.RefundAmount == 0 && guest.RemainingOrderCount > 0;
    }

    /// <summary>
    /// 같은 자리에서 다음 잔을 주문한다(§6.4.3~6.4.6).
    ///
    /// 코스터는 치우지 않는다 — 이미 코스터를 받은 손님에게 다시 놓게 하지 않는다. 회차를 올리고
    /// 새 Order Cocktail을 정한 뒤 reorder 대사를 띄우면 그때부터 다시 잔을 받고, 서빙 제한시간도
    /// 새 칵테일 기준으로 다시 시작한다. 지난 회차의 정산 기록은 손님에게 그대로 쌓아 둔다.
    /// </summary>
    async UniTask StartNextOrderAsync(GuestSlot slot, Guest guest, CancellationToken token)
    {
        guest.orderRound++;
        guest.CurrentOrder = null;
        guest.targetCocktailId = ResolveOrderCocktailId(guest.specifiedOrder);

        slot.MarkWaitingOrder();

        ShowBark(slot, "reorder", serveBarkGapSec);
        RegisterGuestOrder(slot, guest);
        RefreshCraftAvailability();

        Logger.Log($"[Guest] {guest.id} {guest.orderRound}회차 주문 {guest.targetCocktailId} " +
                   $"(남은 주문 {guest.RemainingOrderCount})");

        await WatchServeAsync(slot, guest, token);
    }

    /// <summary>
    /// 코스터가 놓였을 때 플레이어(바텐더, luna)의 ask_order 대사를 손님 자리의 말풍선이 아니라
    /// 다이얼로그 말풍선(UIDialogueTextView.lunaSpeechBubble)에 띄운다. barks.json에서 situation=ask_order,
    /// voice_id=luna인 대사 중 weight로 하나를 골라 표시하며, {cocktail}은 손님의 targetCocktailId로 치환된다.
    /// </summary>
    void ShowPlayerAskOrderBark(Guest guest)
    {
        var candidates = barkData.barkData.Where(b => b.Situation == "ask_order" && b.VoiceId == PlayerCharacterId).ToArray();
        if (candidates.Length == 0) return;

        string rawText = PickWeightedBark(candidates).Text.Ko;
        string cocktailName = GetCocktailName(guest?.targetCocktailId);

        var player = characterData.characterData.FirstOrDefault(c => c.Id == PlayerCharacterId);
        string speakerName = player.Id != null ? player.Name.Ko : PlayerCharacterId;

        Color nameColor = Color.white;
        if (player.Id != null)
            ColorUtility.TryParseHtmlString(player.NameColor, out nameColor);

        var typingData = new TypingData(rawText, speakerName, Vector3.zero, nameColor, isLunaSpeak: true);

        lunaBarkCts?.Cancel();
        lunaBarkCts?.Dispose();
        lunaBarkCts = new CancellationTokenSource();
        HideLunaBarkAfterAsync(typingData, cocktailName, lunaBarkCts.Token).Forget();
    }

    /// <summary>
    /// 타이핑이 끝난 뒤 PlayerBarkDurationSec만큼 더 보여주다가 다이얼로그 말풍선을 끈다.
    /// 그 사이 새 ask_order 대사가 다시 뜨면(lunaBarkCts 교체) 조용히 중단한다.
    /// </summary>
    async UniTaskVoid HideLunaBarkAfterAsync(TypingData typingData, string cocktailName, CancellationToken token)
    {
        await dialogueTextView.StartType(typingData, cocktailName);

        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(PlayerBarkDurationSec), cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        dialogueTextView.lunaSpeechBubble.gameObject.SetActive(false);
    }

    /// <summary>지정 슬롯의 손님 응대가 끝났을 때 호출한다. 인내 타이머를 멈추고 슬롯을 비운 뒤 TycoonFlow에 알린다.</summary>
    public void ReleaseGuest(GuestSlot slot)
    {
        Guest guest = slot != null ? slot.CurrentGuest : null;
        if (guest == null || !releasedGuests.Add(guest)) return;
        string orderId = guest.CurrentOrder?.Id;
        StopPatienceTimer(slot);
        nextIdleChatterSecBySlot.Remove(slot);
        ClearSlotCharacter(slot, guest);
        slot.Clear();
        if (orderId != null) orderController?.CancelOrder(orderId);

        // 손님 단위 합계는 회차별 정산이 이미 DailySales에 들어간 값이므로 당일 누계에는 다시 더하지 않는다.
        // 다만 PlayerData의 보유 재화에는 아직 반영되지 않았으므로 주문 세션이 끝날 때 한 번 지급한다(§6.4.7).
        if (guest.settlements.Count > 0)
        {
            int sessionTotal = guest.SessionTotal;
            playerDataWriter.AddMoney(sessionTotal);

            Logger.Log($"[Settle] {guest.id} 주문 세션 종료 — {guest.settlements.Count}회차 합계 " +
                       $"{sessionTotal} (당일 누계 {Sales.Total}, PlayerData 반영 완료)");
        }

        RefreshCraftAvailability();
        GuestReleased?.Invoke(guest);
    }

    /// <summary>손님 id로 현재 앉아있는 슬롯을 찾는다. 없으면 null.</summary>
    public GuestSlot FindSlotByGuestId(string guestId)
    {
        return slots.FirstOrDefault(s => s.CurrentGuest?.id == guestId);
    }
}
