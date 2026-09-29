using UnityEngine;
using UnityEngine.Playables;

[System.Serializable]
/// <summary>SpriteReveal 클립 한 구간의 시작/끝 _Progress 값을 정의하는 PlayableBehaviour.</summary>
public class SpriteRevealBehaviour : PlayableBehaviour
{
    public float startProgress = 0.8f;
    public float endProgress   = 1f;
}
