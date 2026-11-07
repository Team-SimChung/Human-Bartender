using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>CSV의 npc-sequence/arg를 Inspector에 작성한 이름별 연출로 연결한다.</summary>
public interface IWorldInteractionSequenceParticipant
{
    UniTask PlaySequenceAsync(string sequenceId, CancellationToken token);
}
