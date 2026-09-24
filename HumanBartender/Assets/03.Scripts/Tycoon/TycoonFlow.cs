using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using VContainer;

/// <summary>
/// Play 씬 1부: 타이쿤형 영업 국면. 당일 슬롯의 손님을 모두 응대하거나 돌려보내면 완료된다.
///
/// 응대할 손님 수는 random_waves와 regular_slots를 오늘 날짜로 병합한 결과에서 온다. 오늘 올 손님이
/// 없으면(개점 전인 Day 0, 영업일이 아닌 Day 3) 1부를 열지 않고 바로 넘어간다 — 빈 바 화면을
/// 잠시 띄웠다가 넘어가지 않게 하는 것이 목적이다(1부 일반 손님 운영 런타임 명세 §2).
/// </summary>
public class TycoonFlow : MonoBehaviour, IPlayPhaseFlow
{
    [SerializeField] GuestManager guestManager;

    [Tooltip("2부(대화) 전용 전역 클릭 캐처. Screen Space Overlay라 항상 World Space UI(코스터 드롭존 등)보다 레이캐스트 우선순위가 높아, 타이쿤 국면 동안은 꺼둬야 한다.")]
    [SerializeField] GameObject dialogueClickCatcher;

    int remainingCustomers;
    int todayCustomerCount;
    UniTaskCompletionSource completionSource;

    public async UniTask RunAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
            this.GetCancellationTokenOnDestroy());
        var token = linked.Token;
        // json 로딩이 끝나기를 먼저 기다린다. 로딩은 프레임을 넘겨 가며 도는데 씬의 Start는 그와
        // 무관하게 먼저 끝나서, 기다리지 않으면 SO에 구워져 있는 빈 배열을 오늘 데이터로 읽는다.
        // 그러면 예외도 없이 "오늘 손님 0명"이 되어 1부를 통째로 건너뛴다.
        await NewDataLoadManager.WaitUntilLoadedAsync(token);

        // 당일 대기열은 1부가 시작할 때 만든다. 여기서 만들어야 큐를 세는 시점이 큐를 채우는 시점보다
        // 확실히 뒤가 된다 — Start끼리는 실행 순서가 정해져 있지 않다.
        guestManager.BuildGuestQueue();

        int day = GameStateManager.Instance.CurrentDay;
        todayCustomerCount = guestManager.QueuedGuestCount;
        remainingCustomers = todayCustomerCount;

        if (remainingCustomers <= 0)
        {
            Debug.Log($"[Tycoon] Day {day} — 배정된 손님 슬롯이 없어 1부를 건너뜁니다.");
            return;
        }

        Debug.Log($"[Tycoon] Day {day} 1부 시작 — 오늘 응대할 손님 {todayCustomerCount}명");

        completionSource = new UniTaskCompletionSource();

        if (dialogueClickCatcher != null)
            dialogueClickCatcher.SetActive(false);

        guestManager.GuestReleased += OnGuestReleased;
        var spawn = guestManager.RunSpawnLoopAsync(token).AsTask();
        try
        {
            int first = await UniTask.WhenAny(completionSource.Task.AttachExternalCancellation(token), spawn.AsUniTask());
            if (first == 1)
            {
                await spawn;
                await completionSource.Task.AttachExternalCancellation(token);
            }
        }
        finally
        {
            linked.Cancel();
            try { await spawn; }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            finally
            {
                guestManager.GuestReleased -= OnGuestReleased;
                if (dialogueClickCatcher != null)
                    dialogueClickCatcher.SetActive(true);
            }
        }
    }

    /// <summary>
    /// 손님 한 명의 응대(또는 이탈)가 끝났을 때 호출한다. 남은 손님이 0이 되면 1부를 종료한다.
    /// 한 손님이 여러 잔을 시켜도 자리를 뜰 때 한 번만 불린다.
    /// </summary>
    public void OnCustomerHandled()
    {
        if (remainingCustomers <= 0) return;
        remainingCustomers--;

        if (remainingCustomers > 0)
        {
            Debug.Log($"[Tycoon] 손님 응대 완료 — 남은 손님 {remainingCustomers}/{todayCustomerCount}명");
            return;
        }

        LogPart1Complete();
        completionSource.TrySetResult();
    }

    /// <summary>
    /// 1부 종료 조건(오늘 손님을 모두 응대·이탈 처리)이 채워졌을 때 그날의 결과를 한 줄로 남긴다.
    ///
    /// 매출과 평판은 잔별로 화면에 띄우지 않기로 되어 있어서, 값이 제대로 쌓였는지 확인할 통로가
    /// 지금은 이 로그뿐이다. 일일 매출 정산 화면이 붙으면 그쪽이 정본이 된다.
    /// </summary>
    void LogPart1Complete()
    {
        int day = GameStateManager.Instance.CurrentDay;
        DailySales sales = guestManager.Sales;

        Debug.Log($"[Tycoon] === Day {day} 1부 종료 === 손님 {todayCustomerCount}명 응대 완료 / " +
                  $"당일 매출 {sales.Total} (정산 {sales.Items.Count}건) / 평판 {guestManager.Reputation.Value}");

        foreach (var item in sales.Items)
            Debug.Log($"[Tycoon]   {item.SettlementId} {item.FinalGrade} — {item}");
    }

    void OnGuestReleased(Guest guest)
    {
        if (guest != null) OnCustomerHandled();
    }
}
