using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Timeline;

/// <summary>
/// Outside 씬에서 Unity Timeline 컷씬 재생을 담당하는 인터페이스.
/// </summary>
public interface IOutsideTimeliner
{
    /// <summary>등록된 id에 해당하는 Timeline 에셋을 재생한다.</summary>
    public void PlayTimelineCutScene(string id);
    /// <summary>전달받은 TimelineAsset을 직접 재생한다.</summary>
    public void PlayTimelineCutScene(TimelineAsset timeline);
    UniTask PlayTimelineCutSceneAsync(string id, CancellationToken token = default);
}
