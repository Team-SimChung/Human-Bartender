using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>제조 매니저가 사용하는 화면 준비·단계 실행·정리 계약. 작업 상태는 매니저가 소유한다.</summary>
public interface ICraftExecutor
{
    void Begin(CraftTimer timer, CraftRunDisplay display);
    UniTask<GimmickResult> ExecuteAsync(GimmickStep step, CraftContext context,
                                       CraftTimer timer, CancellationToken token);
    void End();
}
