using DG.Tweening;
using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;



/// <summary>
/// 실외 씬의 엘리베이터. 상호작용 시 플레이어를 엘리베이터에 종속시켜 함께 이동시키고,
/// 카메라 모드 전환, 로고 페이드, 라디오 재생/종료 연출을 동기화한다.
/// </summary>
public class OutsideElevator : InteractiveEntity
{
    [Header("Logo")]
    [SerializeField] LogoFade logoEvent;
    [SerializeField] float logoFadeTiming = 0.2f;

    [Header("Radio")]
    [SerializeField] OutsideElevatorRadio radio;

    [Header("Event")]
    [SerializeField] Vector2Event externalDeltaEvent;
    [SerializeField] IntEvent changeCameraModeEvent;
    [SerializeField] float cameraReturnTiming = 0.8f;

    [Header("Stat")]
    [SerializeField] Transform topPoint;
    [SerializeField] Transform bottomPoint;

    [SerializeField] GameObject wallColider;

    [SerializeField] float duration = 1f;

    [Tooltip("Camera Controller Duration이랑 맞추기")]
    [SerializeField] float delayDuration = 3f;
    [SerializeField] Ease ease;


    Transform targetPoint;

    bool isMoving = false;
    bool isTop = false;

    /// <summary>씬 진입 시 엘리베이터를 상단/하단 고정 위치로 즉시 배치한다.</summary>
    public void SetPosition(bool isTop)
    {
        this.isTop = isTop;
        transform.position = isTop ? topPoint.transform.position : bottomPoint.transform.position;
    }
    private void Start()
    {
        SetPosition(GameStateManager.Instance.GameFlow == EGameFlow.CommuteIn);
    }

    /// <summary>
    /// 엘리베이터 탑승 연출: 플레이어를 강제 이동 상태로 잠그고 엘리베이터의 자식으로 붙여 함께 이동시키며,
    /// 이동 중 카메라 모드 전환/로고 표시/라디오 시작 콜백을 타이밍에 맞춰 DOTween 시퀀스에 끼워 넣는다.
    /// </summary>
    public override void Interact(IInteractor player)
    {
        if (isMoving) return;

        // 1. 상태 및 플래그 설정
        isMoving = true;
        isInteracting = true;
        player.State = EInteractorState.ForceMove;


        Vector3 startPos = this.transform.position;
        startPos.y = player.Transform.position.y;

        player.Transform.position = startPos;
        player.Transform.SetParent(this.transform, worldPositionStays: true);


        wallColider.gameObject.SetActive(true);
        targetPoint = isTop ? bottomPoint : topPoint;


        changeCameraModeEvent?.Raise(1);
        OnInteracted?.Raise(this);
        radio.Interact(null);

        Sequence moveSeq = DOTween.Sequence();

        moveSeq.Append(transform.DOMove(targetPoint.position, duration).SetEase(ease));

        if (!GameStateManager.Instance.IsOutsideLogo)
        {
            GameStateManager.Instance.IsOutsideLogo = true;
            float logoEventTime = duration * logoFadeTiming;
            moveSeq.InsertCallback(logoEventTime, () =>
            {
                logoEvent.ShowLogo();
            });
        }

        float cameraEventTiming = duration * cameraReturnTiming;
        moveSeq.InsertCallback(cameraEventTiming, () =>
        {
            changeCameraModeEvent?.Raise(0);
            //changeCameraModeEvent?.Raise(2);
        });

        moveSeq.OnComplete(() =>
        {
            // 최종 위치 보정
            transform.position = targetPoint.position;

            // 플레이어 종속 해제 및 상태 원복
            player.Transform.SetParent(null, worldPositionStays: true);
            wallColider.gameObject.SetActive(false);
            //changeCameraModeEvent?.Raise(0);

            radio.EndInteract();

            StartCoroutine(DelayToChangeState(player));
        });
    }
    /// <summary>도착 후 delayDuration만큼 대기했다가 이동/상호작용 상태를 풀고 플레이어 조작을 되돌려준다.</summary>
    public IEnumerator DelayToChangeState(IInteractor player)
    {
        yield return new WaitForSeconds(delayDuration);
        
        isMoving = false;
        isInteracting = false;
        isTop = !isTop;

        OnFocusEnter();
        player.State = EInteractorState.None;
    }
}