using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// PlayPhaseController가 순차적으로 실행하는 Play 씬 국면(1부/2부)의 공통 인터페이스.
/// 완료될 때까지 대기하는 RunAsync만 노출하며, 각 Flow는 스스로 시작하지 않는다.
/// </summary>
public interface IPlayPhaseFlow
{
    UniTask RunAsync(CancellationToken cancellationToken = default);
}
