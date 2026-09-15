using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

public class OutsideElevatorRadio : InteractiveNPCEntity
{
    private CancellationTokenSource playCts;

    public override void Interact(IInteractor player)
    {
        if (isTalking) return;

        if (runner == null || presenter == null)
        {
            Debug.LogError("[Radio] Runner 또는 Presenter가 연결되지 않았습니다.");
            return;
        }

        if (steps == null || steps.Length == 0)
        {
            Debug.LogWarning("[Radio] 현재 재생할 대본이 없습니다.");
            return;
        }

        // 진행 중인 다른 대화를 덮어쓰지 않는다.
        if (runner.IsRunning) return;

        isTalking = true;
        isInteracting = true;

        playCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());

        PlayRadioAsync(playCts).Forget();
    }

    private async UniTask PlayRadioAsync(
        CancellationTokenSource playback)
    {
        try
        {
            presenter.playMode = EActivationMode.Proximity;
            runner.Bind(presenter);

            // 추적 대상은 움직이는 라디오 엔티티
            OnInteracted?.Raise(this);
            OnTrackedText?.Raise(this);

            await runner.PlayOutsideAsync(steps, playback.Token);
        }
        catch (OperationCanceledException)
        {
            // 도착 또는 오브젝트 파괴로 종료
        }
        finally
        {
            if (ReferenceEquals(playCts, playback))
            {
                playCts = null;
                isTalking = false;
                isInteracting = false;

                OnTrackedText?.Raise(null);
            }

            playback.Dispose();
        }
    }

    public void EndInteract()
    {
        // 라디오 실행에 전달한 토큰만 취소한다.
        playCts?.Cancel();
    }

    private void OnDisable()
    {
        EndInteract();
    }
}