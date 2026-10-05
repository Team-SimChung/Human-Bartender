using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 바 운영 시간을 재는 시계.
///
/// 제조 화면에 들어가 있는 동안 손님은 기다리지 않는다. 잔을 만드는 데 쓴 시간까지 인내심에서
/// 깎이면 제조를 시작하는 것 자체가 손님을 잃는 선택이 되고, 여러 손님이 앉아 있을 때는 한 명에게
/// 잔을 만들어 주는 사이에 나머지가 전부 나가 버린다. 그래서 코스터·서빙·손님 생성 같은 바 쪽
/// 시간은 이 시계로 재고, 제조 중에는 시계를 멈춘다.
///
/// 멈춘 시간은 나중에 몰아서 흘리지 않는다. 멈춰 있던 동안은 아무 일도 없었던 것으로 하고,
/// 풀리면 멈추기 직전 남은 시간부터 이어 센다(balance.json의 craft_pause_start / craft_pause_end).
///
/// Time.timeScale로 하지 않는 이유: 그러면 제조 기믹도 같이 멈춘다. 멈춰야 하는 것은 바 쪽 시간뿐이다.
/// </summary>
public class BarOperationClock
{
    /// <summary>멈춰 있는지. 멈춰 있는 동안 ElapsedSec은 늘지 않고 WaitAsync의 남은 시간도 줄지 않는다.</summary>
    public bool IsPaused { get; private set; }

    /// <summary>
    /// 멈춰 있던 시간을 뺀, 바 운영이 지금까지 흐른 시간(초).
    /// 두 대기의 시점을 견주어야 할 때(만석 대기와 생성 지연) 같은 기준으로 쓴다.
    /// </summary>
    public float ElapsedSec { get; private set; }

    public void Pause() => IsPaused = true;

    public void Resume() => IsPaused = false;

    /// <summary>시계를 한 프레임 진행시킨다. 이 시계를 쓰는 쪽이 매 프레임 한 번 불러 준다.</summary>
    public void Tick(float deltaSec)
    {
        if (!IsPaused) ElapsedSec += deltaSec;
    }

    /// <summary>
    /// 바 운영 시간으로 seconds만큼 기다린다. 멈춰 있는 프레임은 세지 않는다.
    /// 기다리는 동안 취소되면 false를 돌려준다.
    /// </summary>
    public async UniTask<bool> WaitAsync(float seconds, CancellationToken token)
    {
        float endSec = ElapsedSec + seconds;

        while (ElapsedSec < endSec)
        {
            try
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return !token.IsCancellationRequested;
    }
}
