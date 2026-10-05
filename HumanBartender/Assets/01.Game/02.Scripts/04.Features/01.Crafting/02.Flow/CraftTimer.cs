/// <summary>
/// 한 잔의 제조시간을 재는 시계. 기믹마다 따로 재지 않고 하나로 이어 붙인다.
///
/// 흐르는 구간을 스스로 정하지 않는다. 지금 돌고 있는 기믹이 "플레이어의 입력을 받는 중"이라고
/// 답하는 동안만 실행기가 Tick을 넣어 주므로, 성공 연출·기믹 전환·결과 화면 대기는 저절로 빠진다.
/// 손이 늦은 것과 연출이 긴 것을 같이 세면 실력을 재는 값이 아니게 되기 때문이다.
///
/// 그래서 여기에는 멈춤 기능이 없다. 넣지 않으면 흐르지 않는 것이 곧 멈춤이고, 언제 셀지를 정하는
/// 곳이 실행기 한 군데로 모인다. 시계에도 멈춤이 있으면 둘 중 어느 쪽이 이겼는지 되짚어야 한다.
///
/// Time.time을 직접 읽지 않는 이유이기도 하다. 씬을 띄우지 않아도 시간 흐름을 검사할 수 있다.
/// </summary>
public class CraftTimer
{
    public float ElapsedSec { get; private set; }

    /// <summary>시작했고 아직 멈추지 않았는지.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>첫 기믹을 조작할 수 있게 된 순간 부른다. 누적값을 0으로 되돌린다.</summary>
    public void Start()
    {
        ElapsedSec = 0f;
        IsRunning = true;
    }

    /// <summary>
    /// 플레이어가 조작할 수 있었던 만큼 시간을 더한다.
    /// 멈춘 뒤에 들어온 것은 무시한다 — 마지막 기믹이 확정된 값이 그대로 결과가 되어야 한다.
    /// </summary>
    public void Tick(float deltaSec)
    {
        if (!IsRunning) return;

        ElapsedSec += deltaSec;
    }

    /// <summary>마지막 기믹이 확정된 순간 부른다. 누적값은 남기고 더 이상 늘지 않게 한다.</summary>
    public void Stop()
    {
        IsRunning = false;
    }
}
