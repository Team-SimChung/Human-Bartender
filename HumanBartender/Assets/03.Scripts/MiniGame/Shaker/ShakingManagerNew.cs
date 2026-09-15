using Cysharp.Threading.Tasks;
using Spine;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// 쉐이킹 미니게임의 메인 매니저.
/// BPM 동기화된 StrikeNode가 경로를 이동하며, 플레이어 클릭 타이밍에 따라 성공/실패 판정을 낸다.
///
/// 인터페이스를 둘 구현한다. ICraftGimmick은 1부의 기믹 큐가 부르는 새 경로이고,
/// IMiniGameController는 2부 대화에서 컷씬과 함께 돌던 기존 경로다. 조작 코드는 양쪽이 공유한다.
///
/// 두 경로의 종료 조건이 다르다. 기존 경로는 총 판정 수를 채우거나 실패 한계를 넘기면 끝났지만,
/// 기믹 큐가 돌릴 때는 성공·실패를 합친 목표 스택(shake_target_stacks)에 도달할 때만 끝난다 —
/// 못 채운 스택은 그대로 완성도에 반영되므로 실패로 조기 종료할 이유가 없다.
/// </summary>
public class ShakingManagerNew : MonoBehaviour, IMiniGameController, ICraftGimmick,
                                 ICraftGimmickProgress, ICraftGimmickManualEnd
{
    [SerializeField] bool isTest = false;

    [Header("Data")]
    [SerializeField] CraftStationData data;
    [SerializeField] CategoryColorData colorData;
    [SerializeField] NewCocktailDataSO cocktailDataSO;
    [Tooltip("shake_target_stacks(목표 스택)를 읽어온다. 기믹 큐가 돌릴 때의 종료 조건이자 점수 분모다.")]
    [SerializeField] NewBalanceDataSO balanceData;

    [Tooltip("balanceData가 비었거나 아직 로드되지 않았을 때만 쓰는 값. " +
             "실제 플레이에서는 balance.json의 shake_target_stacks가 이긴다.")]
    [SerializeField] int fallbackTargetStacks = 20;

    [Header("Manager")]
    [SerializeField] ShakeLineCreator shakeLineCreator;
    [SerializeField] ShakingStrikeNode shakingStrikeNode;
    [SerializeField] ShakingCatergoryNodeCreator nodeCreator;
    [SerializeField] AnimSpeedController characterAnim;
    [SerializeField] AudioSource bgmSource;
    [SerializeField] GradientRatioController gageBar;

    [Header("UI")]
    [SerializeField] List<Image> dots;
    [SerializeField] Camera canvasCamera;
    [SerializeField] Canvas gameCanvas;
    [SerializeField] Canvas buttonCanvas;

    [Header("Judge")]
    [SerializeField] float judgeRange = 1;
    [SerializeField] int totalJudge;
    [SerializeField] int successJudge;
    [SerializeField] int failJudge;
    [SerializeField] int limitFailJudge;

    [Header("Craft Event")]
    [SerializeField] VoidEvent craftServe;
    [SerializeField] VoidEvent craftRetry;

    [Inject] ISoundManager soundManager;

    Vector3[] dotPositions;

    bool isPlay = false;

    void Start()
    {
        // 기믹 무대는 바에서 멀리 떨어진 자리에 있다. 메인 카메라를 쓰면 노드 좌표가 바 쪽에 잡혀
        // 무대 밖에 놓이므로, 이 화면을 실제로 비추는 카메라를 찾아 쓴다.
        canvasCamera = CraftGimmickStage.Find(this);

        gameCanvas.worldCamera = canvasCamera;
        buttonCanvas.worldCamera = canvasCamera;

        if (isTest)
        {
            if (cocktailDataSO.TryGet(data.targetCocktailId, out NewCocktailData cocktail))
                data.targetCocktailData = cocktail;
            else
                Debug.LogWarning($"[Shake] 테스트 칵테일 '{data.targetCocktailId}'를 찾지 못했습니다.");

            data.targetCraft_tolerance = 15;
        }

        shakeLineCreator.CreateLine();

        for (int i = 0; i < dots.Count; i++)
        {
            shakeLineCreator.SetLinePosition(i , GetDotWorldPosition(i));
        }

        dotPositions = new Vector3[dots.Count];
        for (int i = 0; i < dotPositions.Length; i++)
        {
            dotPositions[i] = GetDotWorldPosition(i);
        }


        shakingStrikeNode.transform.position = dotPositions[0];

        nodeCreator.Init();
        nodeCreator.InitToStart(dotPositions, ResolveNodeColors());

        successJudge = 0;
        failJudge = 0;

        if (drivenByRunner)
        {
            // 목표 스택은 balance.json이 정본이다. 성공·실패를 합쳐 이 수가 되면 끝난다.
            totalJudge = Mathf.Max(1, ResolveTargetStacks());

            // 큐가 돌릴 때는 실패로 조기 종료하지 않는다. 채우지 못한 성공 스택이 그대로 점수가 된다.
            limitFailJudge = int.MaxValue;
        }
        else
        {
            totalJudge = Mathf.RoundToInt(data.targetCraft_tolerance * 1.3f);
            limitFailJudge = Mathf.RoundToInt(data.targetCraft_tolerance * 0.3f);
        }

        gageBar.UpdateValues(totalJudge, 0, totalJudge, 0);

        isPlay = true;
    }

    
    void Update()
    {
        shakingStrikeNode.Handle();
        nodeCreator.Handle();
    }

    /// <summary>
    /// 패턴 노드에 쓸 색. 기존 경로는 칵테일 키워드를 카테고리 색으로 바꿔 쓴다.
    ///
    /// 기믹 큐가 돌릴 때는 그 키워드 데이터가 없다 — 칵테일 태그를 카테고리 색으로 잇는 표가
    /// 아직 정해지지 않았기 때문이다. 정해지기 전까지는 기본 색 하나로 돌린다.
    /// </summary>
    Color[] ResolveNodeColors()
    {
        NewCocktailTag[] tags = data != null ? data.targetCocktailData.Tags : null;

        if (tags == null || tags.Length == 0 || colorData == null)
            return new[] { Color.white };

        var colors = new Color[tags.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            int n = colorData.categorys.FindIndex(a => a.Contains(tags[i].Ko));
            colors[i] = n >= 0 ? colorData.colors[n] : Color.white;
        }

        return colors;
    }

    /// <summary>
    /// 목표 스택 수를 balance.json에서 읽는다.
    ///
    /// 읽지 못하면 폴백으로 떨어지고 그 사실을 알린다. 예전에는 인스펙터 값을 그대로 썼는데,
    /// 그 값이 0이면 목표가 1로 내려앉아 셰이크가 한 번 만에 끝나 버린다 — 눈에는 "그냥 넘어갔다"로만
    /// 보여서 원인을 찾기 어렵다.
    /// </summary>
    int ResolveTargetStacks()
    {
        int fromData = balanceData != null && balanceData.balanceData != null
            ? balanceData.balanceData.Config.ShakeTargetStacks
            : 0;

        if (fromData > 0) return fromData;

        Logger.Log("[Shake] shake_target_stacks를 읽지 못했습니다. " +
                   $"balanceData가 연결되지 않았을 수 있습니다. 폴백 {fallbackTargetStacks}스택으로 진행합니다.");

        return fallbackTargetStacks;
    }




    private UniTaskCompletionSource tcs;
    public void InitGame(UniTaskCompletionSource tcs)
    {
        this.tcs = tcs;
        data.craftingResult.actionFailCount = 0;
    }

    public void CompleteMade()
    {
        bgmSource.Stop();

        // 기믹 큐가 돌릴 때는 결과를 GimmickResult로 돌려주므로 이 저장소를 쓰지 않는다.
        if (data != null)
        {
            data.craftingResult.isResult = true;
            data.craftingResult.actionFailCount = failJudge;
            data.craftingResult.limitFailCount = limitFailJudge;
        }

        runnerCompletion?.TrySetResult();

        if (tcs != null)
        {
            Logger.Log("shakeManager tcs not null");
            tcs.TrySetResult();
        }
    }

    public void OnNextButton()
    {
        buttonCanvas.gameObject.SetActive(true);
    }

    public void Serve()
    {
        craftServe?.Raise(new Void());
    }

    public void Retry()
    {
        craftRetry?.Raise(new Void());
    }


    public void StartGame()
    {
        if (!isPlay) return;

        Logger.Log("Start Game");

        bgmSource.PlayScheduled(AudioSettings.dspTime + 0.1f);
        shakingStrikeNode.InitToStart(dotPositions, 60);
        characterAnim.Init();
    }

    public void ClickEvent()
    {
        if (!isPlay) return;

        Logger.Log("Click Event");
        CategoryNode node =  nodeCreator.GetNearestNode(shakingStrikeNode.transform.position, judgeRange);

        if (node != null)
        {          
            Logger.Log("Judge");

            if (!isTest)
                soundManager.PlaySE("SFX_shaking");

            characterAnim.PlayAnim();
            nodeCreator.CreateEffectNode(node.transform.position, node.curColor);
            successJudge++;
        }
        else
        {
            Logger.Log("Judge Fail");
            nodeCreator.CreateEffectNode(shakingStrikeNode.transform.position, Color.white);
            failJudge++;
        }

        gageBar.UpdateValues(totalJudge, successJudge, totalJudge-successJudge-failJudge, failJudge);

        if(successJudge + failJudge >= totalJudge)
        {
            isPlay = false;
            CompleteMade();
        }
        else if(failJudge > limitFailJudge)
        {
            isPlay = false;
            CompleteMade();
        }
    }







    public Vector3 GetDotWorldPosition(int index)
    {
        Vector3 local = dots[index].rectTransform.position;

        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint
            (canvasCamera, local);
        screenPos.z = 10f;
        Vector3 worldPos = canvasCamera.ScreenToWorldPoint(screenPos);


        return worldPos;
    }

    // ── ICraftGimmick ───────────────────────────────────────────────────

    /// <summary>기믹 큐가 이 기믹을 돌리고 있는지. 종료 조건과 목표 스택의 출처를 가른다.</summary>
    bool drivenByRunner;

    UniTaskCompletionSource runnerCompletion;
    CraftTimer craftTimer;

    /// <summary>
    /// 이번 제조 시도의 사정(고른 잔·도구·재료). 실행기가 PlayAsync로 넘겨준다.
    /// 아직 그리는 데 쓰지는 않는다 — 리소스가 붙을 때 여기서 가져가면 된다.
    /// </summary>
    CraftContext craftContext;

    public async UniTask<GimmickResult> PlayAsync(GimmickStep step, CraftContext context,
                                                 CraftTimer timer, CancellationToken token)
    {
        // 셰이커는 늘 같지만 안에 든 것과 옮겨 담을 잔은 다르다. 노드 색을 지금은 옛 데이터에서
        // 가져오는데, context.IngredientIds로 옮길 때 여기서 읽으면 된다.
        craftContext = context;

        // 아직 그리는 데 쓰지 않으므로, 값이 제대로 도착했는지는 이 줄로만 확인한다.
        Debug.Log($"[Shake] 문맥 — 잔 {craftContext.GlassId ?? "없음"} / " +
                  $"도구 {craftContext.ToolId ?? "없음"} / 재료 {string.Join(", ", craftContext.IngredientIds)}");

        drivenByRunner = true;
        craftTimer = timer;
        runnerCompletion = new UniTaskCompletionSource();

        // 셰이크는 재료를 쓰지 않는다. 무엇을 섞든 흔드는 조작은 같다.
        //
        // 여기서 "이미 끝났는지" 미리 보지 않는다. 이 함수는 프리팹을 띄운 직후, 아직 Start()가
        // 돌기 전에 불린다. 그 시점의 스택 수는 전부 0이라 어떤 비교를 해도 끝난 것처럼 읽힌다.
        // 기다림을 푸는 신호는 CompleteMade() 한 곳에서만 나온다.

        using (token.Register(() => runnerCompletion.TrySetCanceled()))
        {
            await runnerCompletion.Task;
        }

        return GimmickResult.Mix(ECraftGimmick.Shake, successJudge, failJudge, totalJudge,
                                 endedManually ? ECraftEndType.ManualNext : ECraftEndType.AutoTarget,
                                 craftTimer != null ? craftTimer.ElapsedSec : 0f);
    }

    /// <summary>
    /// 판정을 받는 동안. 시작 대기도 포함한다 — 그때 누르는 첫 입력이 곧 시작 신호라
    /// 플레이어가 할 일이 없는 구간이 아니다.
    /// </summary>
    public bool IsManualInputActive => isPlay;

    // ── ICraftGimmickProgress ───────────────────────────────────────────

    /// <summary>쌓인 스택과 목표. 셰이크는 수량이 아니라 판정 횟수를 센다.</summary>
    public string ProgressText => $"{successJudge + failJudge} / {totalJudge}";

    /// <summary>셰이크에는 도달을 알릴 목표 수량이 없다. 목표 스택을 채우면 그대로 끝난다.</summary>
    public bool ShowOkMark => false;

    // ── ICraftGimmickManualEnd ──────────────────────────────────────────

    /// <summary>다음 버튼으로 끝냈는지. 결과의 종료 방식을 가른다.</summary>
    bool endedManually;

    public bool CanEndNow => isPlay;

    /// <summary>
    /// 지금까지 쌓은 스택으로 끝낸다. 남은 스택을 성공으로 채워 주지 않으므로
    /// 일찍 끝낼수록 완성도가 그만큼 낮게 남는다.
    /// </summary>
    public void EndNow()
    {
        if (!CanEndNow) return;

        endedManually = true;
        isPlay = false;
        CompleteMade();
    }
}
