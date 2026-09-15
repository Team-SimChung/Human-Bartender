using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Outside 씬 카메라의 줌/전환을 담당하는 인터페이스.</summary>
public interface ICameraControlNew
{
    public void ActionZoomAndBack(ECameraZoomType zoomType = ECameraZoomType.Base, UniTaskCompletionSource tcs = null);
    public void ActionZoom(ECameraZoomType zoomType = ECameraZoomType.Base);
    public void TransitionCameraZoom(ECameraZoomType zoomType = ECameraZoomType.Base, float dur = 1f, AnimationCurve curve = null);
    
    // 준서는 바보다.
}
