using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HumanBartender.CutsceneStudio;
using UnityEngine;
using VContainer;

/// <summary>TV 전원과 방송 컷씬을 처리한다. 선택지 로딩·표시는 공통 상호작용, 주변 방송은 Ambient 대상이 맡는다.</summary>
[DisallowMultipleComponent]
public sealed class HomeTVAction : MonoBehaviour, IWorldInteractionAction
{
    [Header("TV 전원")]
    [SerializeField, Tooltip("TV 아래 IntractObjects_TV_on. 전원이 켜질 때만 활성화합니다.")]
    private GameObject powerVisual;
    [SerializeField, Tooltip("TV의 주변 방송 대상. 전원이 꺼지면 대사도 중단합니다.")]
    private WorldInteractionTarget ambientBroadcast;
    [Header("CSV · Studio 연결")]
    [SerializeField, Tooltip("본다·끈다·취소 선택지가 들어 있는 CSV 씬 ID입니다.")]
    private string menuSceneId = "home_tv_menu";
    [SerializeField, Tooltip("뒤에 현재 일차를 붙인 ID로 Studio Catalog를 조회합니다. 예: tv_news_d0")]
    private string programmeIdPrefix = "tv_news_d";
    [Inject] private IConditionUtil conditions;
    [Inject] private WorldAmbientDialogueController ambient;
    private CancellationTokenSource execution;
    private int currentDay;
    public bool IsPoweredOn { get; private set; }
    public bool CanExecute => isActiveAndEnabled && execution == null && powerVisual != null &&
        ambientBroadcast != null && conditions != null;

    private void OnEnable()
    {
        currentDay = GameStateManager.Instance.CurrentDay;
        SetPower(false);
    }

    private void Update() => SynchronizeDay();

    internal void SynchronizeDay()
    {
        int day = GameStateManager.Instance.CurrentDay;
        if (currentDay == day) return;
        currentDay = day;
        execution?.Cancel();
        SetPower(false);
    }

    private void OnDisable()
    {
        execution?.Cancel();
        SetPower(false);
    }

    internal void SetPower(bool value)
    {
        IsPoweredOn = value;
        if (powerVisual != null) powerVisual.SetActive(value);
        if (ambientBroadcast != null) ambientBroadcast.gameObject.SetActive(value);
    }

    public async UniTask ExecuteAsync(WorldInteractionActionContext context, CancellationToken token)
    {
        if (!CanExecute || context.ChooseOutsideChoices == null || context.PlayStudio == null)
            throw new InvalidOperationException("[HomeTV] 전원·방송 대상 또는 공통 상호작용 연결을 확인하세요.");
        SynchronizeDay();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        execution = lifetime;
        token = lifetime.Token;
        ambient?.Restart(ambientBroadcast);
        SetPower(true);
        try
        {
            string id = programmeIdPrefix + currentDay;
            using var prepared = await StudioPlaybackService.PrepareAsync(id, token);
            token.ThrowIfCancellationRequested();
            var selected = await context.ChooseOutsideChoices(menuSceneId, choice => choice != "watch" || prepared != null, token);
            token.ThrowIfCancellationRequested();
            switch (selected.Id)
            {
                case "watch":
                    if (prepared == null) throw new InvalidOperationException("[HomeTV] 준비되지 않은 방송을 선택했습니다.");
                    await context.PlayStudio(prepared.Sequence, token);
                    token.ThrowIfCancellationRequested();
                    conditions.ApplyRequired("flag." + id + "_seen = true");
                    break;
                case "off":
                    SetPower(false);
                    break;
                case "cancel":
                    break;
                default:
                    throw new InvalidOperationException("[HomeTV] 알 수 없는 선택지: " + selected.Id);
            }
        }
        finally
        {
            execution = null;
            SynchronizeDay();
        }
    }

}
