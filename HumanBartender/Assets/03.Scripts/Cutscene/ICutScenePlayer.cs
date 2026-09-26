using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>컷씬 재생/클리어/타임라인 계속 진행을 담당하는 인터페이스.</summary>
public interface ICutScenePlayer
{
    UniTask PlayCutScene(
        string id,
        UniTaskCompletionSource tcs = null, CancellationToken token = default);

    public void ClearCutScene();
    public void OnContinueTimeline();
}
