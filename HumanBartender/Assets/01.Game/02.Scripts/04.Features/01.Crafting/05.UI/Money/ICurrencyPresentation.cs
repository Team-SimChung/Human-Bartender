using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>진행 중인 재화 팝업과 잔액 강조 효과가 끝날 때까지 기다린다.</summary>
public interface ICurrencyPresentation
{
    UniTask WaitForAnimationsAsync(CancellationToken cancellationToken = default);
}
