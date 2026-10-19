using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>주변 대화의 일시정지와 자동 읽기 시간을 세션별로 제공함.</summary>
public interface IDialogueAutomaticPlayback
{
    UniTask WaitUntilReadyAsync(CancellationToken token);
    UniTask WaitAfterLineAsync(CancellationToken token);
}
