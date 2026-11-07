using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>대본 시작 전 위치·카메라를 준비하는 선택적 공통 단계.</summary>
public interface IWorldInteractionPreparationParticipant
{
    UniTask PrepareInteractionAsync(CancellationToken token);
}
