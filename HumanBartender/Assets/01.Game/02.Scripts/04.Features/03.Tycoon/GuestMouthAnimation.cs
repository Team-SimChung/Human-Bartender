using UnityEngine;

/// <summary>
/// 선택된 입의 말하기 클립만 재생한다. 대사의 수명은 GuestSlot이 소유한다.
/// 클립의 위치 곡선은 정지 입 기준의 상대 오프셋이며, 표정별 잘린 프레임의 기준점을 보존한다.
/// </summary>
public sealed class GuestMouthAnimation
{
    SpriteRenderer renderer;
    AnimationClip clip;
    Sprite restingSprite;
    Vector3 restingPosition;
    float elapsed;

    public bool IsSpeaking { get; private set; }

    public void Bind(SpriteRenderer target, AnimationClip animation)
    {
        Stop();
        renderer = target;
        clip = animation;
        restingSprite = target != null ? target.sprite : null;
        restingPosition = target != null ? target.transform.localPosition : Vector3.zero;
        // Unity needs an Animator to sample object-reference curves (SpriteRenderer.sprite).
        // This renderer belongs to the random guest; no controller or prefab wiring is needed.
        if (target != null && animation != null && !target.TryGetComponent<Animator>(out _))
            target.gameObject.AddComponent<Animator>();
    }

    public void Start()
    {
        if (renderer == null || clip == null || clip.length <= 0) return;
        elapsed = 0;
        IsSpeaking = true;
        Tick(0);
    }

    public void Tick(float deltaTime)
    {
        if (!IsSpeaking || renderer == null || clip == null) return;
        elapsed = Mathf.Repeat(elapsed + Mathf.Max(0, deltaTime), clip.length);
        // Sample only the authored sprite/position curves; the existing fade retains ownership of color.
        renderer.transform.localPosition = Vector3.zero;
        clip.SampleAnimation(renderer.gameObject, elapsed);
        renderer.transform.localPosition += restingPosition;
    }

    public void Stop()
    {
        if (IsSpeaking && renderer != null)
        {
            renderer.sprite = restingSprite;
            renderer.transform.localPosition = restingPosition;
        }
        IsSpeaking = false;
        elapsed = 0;
    }
}
