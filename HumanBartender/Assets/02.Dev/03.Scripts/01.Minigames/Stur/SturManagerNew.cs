using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스터링 미니게임의 메인 매니저. IMiniGameController를 구현한다.
/// BPM 동기화된 SturStrikeNode가 원형 경로를 공전하며, 플레이어 클릭에 따라 판정한다.
/// 총 판정 수 또는 실패 한계 초과 시 CompleteMade()를 호출해 결과를 반환한다.
/// </summary>
public class SturManagerNew : MonoBehaviour, IMiniGameController
{
    [SerializeField] bool isTest = false;

    [Header("Data")]
    [SerializeField] CraftStationData data;
    [SerializeField] CategoryColorData colorData;
    [SerializeField] NewCocktailDataSO cocktailDataSO;

    [Header("Manager")]
    [SerializeField] SturStrikeNode sturStrikeNode;
    [SerializeField] CircleLineCreator circleLineCreator;
    [SerializeField] CircleNodeCreator nodeCreator;
    [SerializeField] AudioSource bgmSource;
    [SerializeField] GradientRatioController gageBar;

    [Header("UI")]
    [SerializeField] Image center;
    [SerializeField] float radiusX = 2.4f;
    [SerializeField] float radiusY = 2.0f;
    [SerializeField] Camera canvasCamera;
    [SerializeField] Canvas gameCanvas;
    [SerializeField] Canvas buttonCanvas;
    [SerializeField] int bpm = 60;
    [SerializeField] int beatCount = 4;


    [Header("Judge")]
    [SerializeField] float judgeRange = 1;
    [SerializeField] int totalJudge;
    [SerializeField] int successJudge;
    [SerializeField] int failJudge;
    [SerializeField] int limitFailJudge;

    [Header("Craft Event")]
    [SerializeField] VoidEvent craftServe;
    [SerializeField] VoidEvent craftRetry;

    bool isPlay = false;
    Color[] colors;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        canvasCamera = Camera.main;

        gameCanvas.worldCamera = canvasCamera;
        buttonCanvas.worldCamera = canvasCamera;


        if (isTest)
        {
            if (cocktailDataSO.TryGet(data.targetCocktailId, out NewCocktailData cocktail))
                data.targetCocktailData = cocktail;
            else
                Logger.LogWarning($"[Stur] 테스트 칵테일 '{data.targetCocktailId}'를 찾지 못했습니다.");

            data.targetCraft_tolerance = 15;
        }

        // 태그가 없으면 색 하나로 돌린다. 기믹 큐가 돌릴 때는 목표 칵테일이 여기 꽂히지 않으므로
        // 비어 있는 것이 정상이다 — 예전에는 그대로 Length를 읽어 터졌다.
        NewCocktailTag[] tags = data != null ? data.targetCocktailData.Tags : null;

        if (tags == null || tags.Length == 0 || colorData == null)
        {
            colors = new[] { Color.white };
        }
        else
        {
            colors = new Color[tags.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                int n = colorData.categorys.FindIndex(a => a.Contains(tags[i].Ko));
                colors[i] = n >= 0 ? colorData.colors[n] : Color.white;
            }
        }


        circleLineCreator.BuildCircle(radiusX, radiusY, GetCenterWorldPosition());
        nodeCreator.Init(
            radiusX, radiusY,
            GetCenterWorldPosition(),
            colors);


        sturStrikeNode.Init(GetCenterWorldPosition(), bpm, beatCount, radiusX, radiusY);


        totalJudge = Mathf.RoundToInt(data.targetCraft_tolerance * 1.3f);
        successJudge = 0;
        failJudge = 0;
        limitFailJudge = Mathf.RoundToInt(data.targetCraft_tolerance * 0.3f);

        gageBar.UpdateValues(totalJudge, 0, totalJudge, 0);

        isPlay = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (!isPlay) return;

        sturStrikeNode.Handle();
        nodeCreator.Handle();
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
        nodeCreator.StopAndReturnNodes();
        data.craftingResult.isResult = true;
        data.craftingResult.actionFailCount = failJudge;
        data.craftingResult.limitFailCount = limitFailJudge;

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
        sturStrikeNode.InitToStart();
        nodeCreator.InitToStart();
    }

    public void OnPressEvent()
    {
        if (!isPlay) return;

        Logger.Log("Click Event");
        Vector3 hitPosition;
        Color hitColor;
        bool hit = nodeCreator.TryHitNearestNode(
            sturStrikeNode.transform.position,
            judgeRange,
            out hitPosition,
            out hitColor);

        if (hit)
        {
            Logger.Log("Judge");
            nodeCreator.CreateEffectNode(hitPosition, hitColor);
            successJudge++;
        }
        else
        {
            Logger.Log("Judge Fail");
            nodeCreator.CreateEffectNode(sturStrikeNode.transform.position, Color.white);
            failJudge++;
        }

        gageBar.UpdateValues(totalJudge, successJudge, totalJudge - successJudge - failJudge, failJudge);

        if (successJudge + failJudge >= totalJudge)
        {
            isPlay = false;
            CompleteMade();
        }
        else if (failJudge > limitFailJudge)
        {
            isPlay = false;
            CompleteMade();
        }
    }

    public void OnReleaseEvent()
    {
        if (!isPlay) return;
    }



    public Vector3 GetCenterWorldPosition()
    {
        Vector3 local = center.rectTransform.position;

        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint
            (canvasCamera, local);
        screenPos.z = 10f;
        Vector3 worldPos = canvasCamera.ScreenToWorldPoint(screenPos);


        return worldPos;
    }
}
