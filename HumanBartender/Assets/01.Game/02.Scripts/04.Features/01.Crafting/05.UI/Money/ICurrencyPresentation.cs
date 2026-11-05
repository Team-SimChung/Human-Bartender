using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>진행 중인 소지금 숫자 변경과 알림 페이드아웃이 끝날 때까지 기다린다.</summary>
public interface ICurrencyPresentation
{
    UniTask WaitForAnimationsAsync(CancellationToken cancellationToken = default);
}
