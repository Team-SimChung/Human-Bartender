using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/// <summary>Outside 씬 카메라의 줌/전환을 담당하는 인터페이스.</summary>
public interface ICameraControlNew
{
    public void ActionZoomAndBack(ECameraZoomType zoomType = ECameraZoomType.Base, UniTaskCompletionSource tcs = null);
    public void ActionZoom(ECameraZoomType zoomType = ECameraZoomType.Base);
    public void TransitionCameraZoom(ECameraZoomType zoomType = ECameraZoomType.Base, float dur = 1f, AnimationCurve curve = null);
    UniTask TransitionCameraZoomAsync(ECameraZoomType zoomType = ECameraZoomType.Base,
        float duration = 1f, AnimationCurve curve = null, CancellationToken token = default);

    /// <summary>그 줌 프리셋에서 렌즈가 갖는 Orthographic Size. 화면은 바꾸지 않는다.</summary>
    float GetOrthographicSize(ECameraZoomType zoomType);

}
